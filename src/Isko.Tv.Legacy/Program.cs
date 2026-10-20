using System.Diagnostics;
using ElmSharp;
using Isko.Protection;
using Tizen.Applications;
using Tizen.System;
using Tizen.WebView;

namespace Isko.Tv;

internal sealed class Program : CoreUIApplication
{
    private Window window = null!;
    private Entry address = null!;
    private Label status = null!;
    private WebView? web;
    private readonly List<Widget> controls = new();
    private readonly ProtectionPolicy protection = new();
    private readonly Measurements metrics = new();
    private readonly Stopwatch loading = new();
    private SessionStore session = null!;
    private bool theater;
    private readonly HashSet<string> exceptedHosts = new(StringComparer.OrdinalIgnoreCase);
    private int selected;
    private string currentUrl = "";
    private string? explicitNavigation;
    private string platform = "unknown";
    private IntPtr mediaTimer;
    private bool sampling, paused, backPending, cleanupFailed;
    private long documentGeneration;

    public static void Main(string[] args)
    {
        Elementary.Initialize();
        using var app = new Program();
        app.Run(args);
    }

    protected override void OnCreate()
    {
        base.OnCreate();
        session = new SessionStore(Application.Current.DirectoryInfo.Data);
        window = new Window("Isko") { Geometry = new Rect(0, 0, 1920, 1080) };
        window.Show();
        window.CloseRequested += (_, _) => Exit();
        address = new Entry(window) { IsSingleLine = true, IsAutoSave = false, PredictionAllowed = false, InputHint = InputHints.SensitiveData, Geometry = new Rect(24, 18, 1550, 58) };
        address.Text = System.Net.WebUtility.HtmlEncode(session.LastOrigin ?? "https://example.org/");
        address.Activated += (_, _) => Navigate(Entry.ConvertMarkupToUtf8(address.Text));
        address.KeyDown += HandleKey;
        address.Show(); controls.Add(address);
        AddButton("Go", 1590, 18, 140, () => Navigate(Entry.ConvertMarkupToUtf8(address.Text)));
        AddButton("Back", 24, 88, 140, Back);
        AddButton("Close page", 174, 88, 190, ClosePage);
        AddButton("Theater", 374, 88, 170, ToggleTheater);
        AddButton("Protection", 554, 88, 190, () => {
            protection.Enabled = !protection.Enabled;
            if (web != null) web.Eval(protection.GetCosmeticScript(currentUrl));
            SetStatus(protection.Enabled ? "Protection ON (request filtering unsupported)" : "Protection OFF");
        });
        AddButton("Site exception", 754, 88, 210, () => {
            if (!Uri.TryCreate(currentUrl, UriKind.Absolute, out var u)) return;
            var siteException = !exceptedHosts.Contains(u.Host);
            if (siteException) exceptedHosts.Add(u.Host); else exceptedHosts.Remove(u.Host);
            protection.SetHostException(u.Host, siteException);
            web?.Eval(protection.GetCosmeticScript(currentUrl));
            SetStatus(siteException ? "This host excepted" : "This host protected");
        });
        AddButton("Favorite", 974, 88, 170, () => { session.Favorite(currentUrl); SetStatus("Favorite origin saved; path omitted"); });
        AddButton("Favorites", 1154, 88, 170, () => ShowSaved(session.Favorites));
        AddButton("History", 1334, 88, 160, () => ShowSaved(session.History));
        AddButton("Resume origin", 1504, 88, 240, () => { if (session.LastOrigin != null) Navigate(session.LastOrigin); });
        status = new Label(window) { Geometry = new Rect(24, 150, 1870, 55) };
        status.Show();
        try { if (Information.TryGetValue("http://tizen.org/feature/platform.version", out string version)) platform = version; } catch { }
        // Never infer platform from model year or firmware marketing name.
        SetStatus("Runtime: " + platform + "; request interception: unsupported; Menu: toolbar; Back: exit theater/history/close");
        metrics.Event("shell-created");
        mediaTimer = EcoreMainloop.AddTimer(30, () => { SampleMedia(); return true; });
        FocusToolbar(0);
    }

    private void AddButton(string text, int x, int y, int width, Action action)
    {
        var b = new Button(window) { Text = text, Geometry = new Rect(x, y, width, 54) };
        b.Clicked += (_, _) => action(); b.KeyDown += HandleKey; b.Show(); controls.Add(b);
    }
    private void SetStatus(string text) => status.Text = System.Net.WebUtility.HtmlEncode(text);
    private void FocusToolbar(int index)
    {
        if (theater) ToggleTheater();
        web?.SetFocus(false); selected = (index + controls.Count) % controls.Count;
        controls[selected].SetFocus(true);
    }
    private void ShowSaved(List<string> origins)
    {
        // No secondary WebView. Cycle a small native text field; Go opens selected origin.
        if (origins.Count == 0) { SetStatus("No saved origins"); return; }
        var old = Entry.ConvertMarkupToUtf8(address.Text);
        address.Text = System.Net.WebUtility.HtmlEncode(origins[(origins.IndexOf(old) + 1) % origins.Count]);
        SetStatus("Selected saved origin. Go to open; press list button again to cycle.");
    }
    private bool IsSupportedRuntime() => System.Version.TryParse(platform, out var v) && v.Major == 8;
    private void Navigate(string input)
    {
        if (cleanupFailed) { SetStatus("Previous view destruction failed; close again or restart app before opening a page."); return; }
        if (!UrlSafety.TryNormalizeAddress(input, out var url)) { SetStatus("Enter an HTTP(S) address without credentials"); return; }
        if (!IsSupportedRuntime()) { SetStatus("Legacy spike requires measured Tizen 8.x; runtime " + platform + ". Run diagnostic probe first."); return; }
        try {
            EnsureWebView();
            explicitNavigation = url;
            web!.LoadUrl(url); web.SetFocus(true); address.SetFocus(false);
            // Do not keep entered credential-bearing paths in the address widget.
            address.Text = System.Net.WebUtility.HtmlEncode(UrlSafety.ForDiagnostics(url));
        } catch { SetStatus("WebView could not start. Runtime/API capability unverified."); metrics.Event("webview-start-failed"); ClosePage(); }
    }
    private void EnsureWebView()
    {
        if (web != null) return;
        web = new WebView(window) { Geometry = new Rect(0, 215, 1920, 865) };
        web.LoadStarted += OnLoadStarted;
        web.LoadFinished += OnLoadFinished;
        web.LoadError += OnLoadError;
        web.UrlChanged += OnUrlChanged;
        web.NewWindowPolicyDecideRequested += OnPopup;
        web.NavigationPolicyDecideRequested += OnNavigation;
        web.KeyDown += HandleKey;
        web.Show(); metrics.Event("webview-created");
    }
    private void OnLoadStarted(object? sender, EventArgs e) { documentGeneration++; loading.Restart(); metrics.Event("load-started"); }
    private void OnLoadError(object? sender, SmartCallbackLoadErrorArgs e) { Interlocked.Increment(ref metrics.LoadErrors); metrics.Event("load-error"); }
    private void OnUrlChanged(object? sender, SmartCallbackArgs e)
    {
        if (web == null) return;
        currentUrl = web.Url; session.Remember(currentUrl);
        address.Text = System.Net.WebUtility.HtmlEncode(UrlSafety.ForDiagnostics(currentUrl));
    }
    private void OnLoadFinished(object? sender, EventArgs e)
    {
        metrics.Event("load-finished-ms", loading.ElapsedMilliseconds);
        if (web == null) return;
        web.Eval(protection.GetCosmeticScript(web.Url)); // post-load only; not network blocking
        web.Eval(PageScripts.ObserveMedia);
        SetStatus("Loaded; popup/navigation callbacks installed; network filtering UNSUPPORTED");
    }
    private void OnPopup(object? sender, NewWindowPolicyEventArgs e)
    {
        var decision = protection.EvaluatePopup(currentUrl, e.Url);
        var target = e.Url;
        e.Ignore(); // Never allocate a second WebView.
        if (!decision.Allowed) { Interlocked.Increment(ref metrics.PopupsBlocked); metrics.Event("popup-blocked"); }
        else {
            var originView = web;
            EcoreMainloop.Post(() => { if (web == originView && !paused) Navigate(target); });
        } // Protection-off/exception reuses this view, outside the policy callback.
    }
    private void OnNavigation(object? sender, NavigationPolicyEventArgs e)
    {
        if (explicitNavigation != null && string.Equals(explicitNavigation, e.Url, StringComparison.Ordinal)) {
            explicitNavigation = null; e.Use(); return;
        }
        var kind = e.NavigationType == NavigationType.LinkClicked ? NavigationKind.UserLink : NavigationKind.Unknown;
        var decision = protection.EvaluateNavigation(new NavigationRequest(currentUrl, e.Url, kind, null));
        if (decision.Allowed) e.Use();
        else { e.Ignore(); Interlocked.Increment(ref metrics.NavigationsBlocked); metrics.Event("navigation-blocked"); }
    }
    private void HandleKey(object? sender, EvasKeyEventArgs e)
    {
        if ((e.Flags & EvasEventFlag.OnHold) != 0) return;
        var onPage = ReferenceEquals(sender, web);
        switch (e.KeyName) {
            case "XF86Back": case "Escape": e.Flags |= EvasEventFlag.OnHold; Back(); return;
            case "XF86Menu": case "Menu": e.Flags |= EvasEventFlag.OnHold; FocusToolbar(0); return;
            case "XF86Exit": e.Flags |= EvasEventFlag.OnHold; ClosePage(); return;
        }
        if (onPage) return; // Engine owns ordinary arrows/Enter; no synthetic JS clicks.
        if (!ReferenceEquals(sender, address) && (e.KeyName == "Left" || e.KeyName == "Right")) {
            e.Flags |= EvasEventFlag.OnHold;
            var i = controls.IndexOf((Widget)sender!); FocusToolbar(i + (e.KeyName == "Left" ? -1 : 1));
        } else if (e.KeyName == "Down" && web != null) {
            e.Flags |= EvasEventFlag.OnHold; controls[selected].SetFocus(false); web.SetFocus(true);
        }
    }
    private void ToggleTheater()
    {
        if (web == null) return;
        theater = !theater;
        foreach (var control in controls) { if (theater) control.Hide(); else control.Show(); }
        if (theater) status.Hide(); else status.Show();
        web.Geometry = theater ? new Rect(0, 0, 1920, 1080) : new Rect(0, 215, 1920, 865);
        web.SetFocus(true);
        // This is native viewport theater, NOT document/video requestFullscreen success.
        metrics.Event(theater ? "theater-enter" : "theater-exit");
    }
    private async void Back()
    {
        if (backPending) return;
        if (web == null) { Exit(); return; }
        if (cleanupFailed) { ClosePage(); return; }
        if (theater) { web.ExitFullscreen(); ToggleTheater(); return; }
        backPending = true;
        var active = web;
        var generation = documentGeneration;
        try {
            var result = await active.EvalAsync("Boolean(document.fullscreenElement || document.webkitFullscreenElement)");
            if (web != active || generation != documentGeneration || paused) return;
            if (result.Trim('"') == "true") { active.ExitFullscreen(); return; }
            if (active.CanGoBack()) active.GoBack(); else ClosePage();
        } catch {
            metrics.Event("fullscreen-state-unavailable");
            if (web == active && generation == documentGeneration) {
                try { active.ExitFullscreen(); } catch { metrics.Event("fullscreen-exit-failed"); }
            }
        } finally { backPending = false; }
    }
    private async void SampleMedia()
    {
        if (web == null || sampling || paused) return;
        sampling = true;
        try {
            var active = web;
            var result = await active.EvalAsync(PageScripts.ReadMedia);
            if (web == active && !paused) metrics.Media(result);
        } catch { metrics.Event("media-metrics-unavailable"); }
        finally { sampling = false; }
    }
    private void ClosePage()
    {
        if (web == null) return;
        if (theater) ToggleTheater();
        var closing = web; web = null;
        try { closing.Eval(PageScripts.ReleaseMedia); closing.StopLoading(); closing.SetFocus(false); }
        catch { metrics.Event("cleanup-script-unavailable"); }
        closing.LoadStarted -= OnLoadStarted; closing.LoadFinished -= OnLoadFinished;
        closing.LoadError -= OnLoadError; closing.UrlChanged -= OnUrlChanged;
        closing.NewWindowPolicyDecideRequested -= OnPopup; closing.NavigationPolicyDecideRequested -= OnNavigation;
        closing.KeyDown -= HandleKey;
        try { closing.Unrealize(); } catch {
            web = closing; cleanupFailed = true; // Retain ownership; forbid allocating another view.
            metrics.Event("webview-destruction-failed");
            SetStatus("Page destruction failed. Close again or restart app; opening another view is disabled.");
            return;
        }
        cleanupFailed = false;
        documentGeneration++;
        currentUrl = ""; explicitNavigation = null;
        metrics.Sample("after-close"); SetStatus("Page closed; engine destroyed. No cache purge."); FocusToolbar(0);
    }
    protected override void OnPause()
    {
        paused = true;
        try {
            if (web != null) {
                try { web.Eval(PageScripts.PauseMedia); } catch { metrics.Event("pause-script-unavailable"); }
                try { web.SetViewVisibility(false); } finally { web.Suspend(); }
            }
        } catch { metrics.Event("platform-pause-failed"); }
        finally { metrics.Sample("pause"); base.OnPause(); }
    }
    protected override void OnResume()
    {
        base.OnResume();
        paused = false;
        try { if (web != null) { web.Resume(); web.SetViewVisibility(true); } }
        catch { metrics.Event("platform-resume-failed"); }
        metrics.Sample("resume"); // Deliberately no autoplay after resume.
    }
    protected override void OnTerminate()
    {
        try {
            if (mediaTimer != IntPtr.Zero) EcoreMainloop.RemoveTimer(mediaTimer);
            ClosePage();
        } finally { metrics.Dispose(); base.OnTerminate(); }
    }
}
