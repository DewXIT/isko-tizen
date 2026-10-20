using System.Collections.Concurrent;
using System.Diagnostics;
using Isko.Protection;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace Isko.Tv;

// Popup/redirect policy, request filtering, history and HTML fullscreen. These API12 members are
// EditorBrowsable(Never) but need no privilege beyond internet; reviewed in the core assembly audit.
internal sealed class ProtectedPageEngine : PageEngine
{
    private readonly ConcurrentQueue<Action> completions = new();
    private readonly HashSet<string> exceptions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private ProtectionPolicy policy = new(); // Replaced, never mutated after publication.
    private WebView? view;
    private WebContext? context;
    private Tizen.NUI.Timer? timer;
    private Action close = () => { };
    private Action<string> navigate = _ => { }, status = _ => { };
    private Action<bool> fullscreen = _ => { };
    private Measurements metrics = null!;
    private string site = "";
    private int generation, popupPending, backSequence;
    private bool enabled = true, fixture, mediaPending, backUnavailable, requestIntercepting, requestHookFailed;
    private long backDeadline, sampleAt;
    private WebContext.HttpRequestInterceptedCallback? requestCallback;

    public override string Status => "Ochrona " + (enabled ? "włączona" : "wyłączona") +
        (HasException() ? " · wyjątek dla tej strony" : "") + (fixture ? " · test /ad-probe wł." : "") +
        (requestHookFailed ? " · filtr żądań niedostępny" : "");
    public override string Instructions => "Menu: pasek · Wstecz: fullscreen → historia → pasek";

    public override void Attach(WebView page, Action closePage, Action<string> open,
        Action<bool> setFullscreen, Action<string> setStatus, Measurements measurements)
    {
        if (view != null) throw new InvalidOperationException("Previous engine is still attached.");
        view = page; close = closePage; navigate = open; fullscreen = setFullscreen; status = setStatus; metrics = measurements;
        if (timer == null) { timer = new Tizen.NUI.Timer(100); timer.Tick += Tick; timer.Start(); }
        page.NewWindowPolicyDecided += Popup;
        page.NavigationPolicyDecided += Navigation;
        page.FullscreenEntered += Entered;
        page.FullscreenExited += Exited;
        context = page.Context;
        // Context callback may run on a worker thread. Its body touches no NUI view.
        requestCallback = Intercept;
        requestIntercepting = false; requestHookFailed = false;
        Rebuild();
        if (requestHookFailed) throw new InvalidOperationException("Request interceptor unavailable.");
#if ISKO_RUNTIME_DIAGNOSTICS
        // Temporary hardware profiling build only; normal packages never open an inspector.
        try { metrics.Event("inspector-port", page.StartInspectorServer(7011)); }
        catch { metrics.Event("inspector-unavailable"); }
#endif
    }

    public override void Started(string url)
    {
        Interlocked.Increment(ref generation);
        Interlocked.Exchange(ref popupPending, 0);
        backDeadline = 0;
        backSequence++;
        mediaPending = false; backUnavailable = false;
        if (UrlSafety.TryNormalizeAddress(url, out var normalized)) Volatile.Write(ref site, normalized);
        Rebuild();
    }
    public override void Loaded(string url)
    {
        Volatile.Write(ref site, url); Rebuild();
        Execute(PageScripts.ObserveMedia);
        Execute(Volatile.Read(ref policy).GetCosmeticScript(url));
        sampleAt = clock.ElapsedMilliseconds + 30000;
    }
    private bool HasException() => Uri.TryCreate(Volatile.Read(ref site), UriKind.Absolute, out var uri) && exceptions.Contains(uri.IdnHost);
    private void Rebuild()
    {
        var next = new ProtectionPolicy { Enabled = enabled };
        next.AddRequestHost("pubads.g.doubleclick.net");
        next.AddRequestHost("pagead2.googlesyndication.com");
        foreach (var host in exceptions) next.SetHostException(host, true);
        if (fixture && Uri.TryCreate(Volatile.Read(ref site), UriKind.Absolute, out var uri) &&
            (uri.Scheme == "http" || uri.Scheme == "https")) next.AddRequestRule(new RequestRule(uri.IdnHost, "/ad-probe"));
        Volatile.Write(ref policy, next);
        UpdateRequestInterception();
    }
    private void UpdateRequestInterception()
    {
        if (context == null) return;
        var needed = Volatile.Read(ref policy).NeedsRequestInterception(Volatile.Read(ref site));
        if (needed == requestIntercepting) {
            requestHookFailed = false;
            metrics.RequestBlocking = needed ? "experimental-hook-unverified" : "disabled-or-site-exempt";
            return;
        }
        try {
            context.RegisterHttpRequestInterceptedCallback(needed ? requestCallback! : null!);
            requestIntercepting = needed;
            requestHookFailed = false;
            metrics.RequestBlocking = needed ? "experimental-hook-unverified" : "disabled-or-site-exempt";
        } catch { requestHookFailed = true; metrics.RequestBlocking = "hook-toggle-failed"; metrics.Event("request-hook-toggle-failed"); }
    }
    public override void ToggleProtection() { enabled = !enabled; Changed(); }
    public override void ToggleFixture() { fixture = !fixture; Changed(); }
    public override void ToggleException()
    {
        if (!Uri.TryCreate(Volatile.Read(ref site), UriKind.Absolute, out var uri)) return;
        if (!exceptions.Remove(uri.IdnHost)) exceptions.Add(uri.IdnHost);
        Changed();
    }
    private void Changed()
    {
        Rebuild();
        Execute(Volatile.Read(ref policy).GetCosmeticScript(Volatile.Read(ref site)));
        status(Status + ". Zmiana reguł żądań dotyczy kolejnych żądań.");
    }

    private readonly struct NativeRequest : IRequestCompletion
    {
        private readonly WebHttpRequestInterceptor request;
        public NativeRequest(WebHttpRequestInterceptor request) => this.request = request;
        public string Url => request.Url;
        public bool Complete(bool block)
        {
            if (!block) { request.Ignore(); return true; }
            return request.SetResponse("HTTP/1.1 403 Forbidden\r\nContent-Type: text/plain\r\nContent-Length: 0\r\nCache-Control: no-store\r\n\r\n", Array.Empty<byte>());
        }
    }
    private void Intercept(WebHttpRequestInterceptor request)
    {
        var result = RequestCompletion.Apply(new NativeRequest(request), Volatile.Read(ref policy), Volatile.Read(ref site));
        if (result.BlockRequested) {
            Interlocked.Increment(ref metrics.RequestBlockAttempts);
            if (result.Completed) Interlocked.Increment(ref metrics.RequestsBlocked);
        }
        if (!result.Completed) Interlocked.Increment(ref metrics.RequestCompletionErrors);
    }
    private void Popup(object? sender, WebViewPolicyDecidedEventArgs args)
    {
        var maker = args.PolicyDecisionMaker;
        var target = maker.Url;
        var allowed = Volatile.Read(ref policy).EvaluatePopup(Volatile.Read(ref site), target).Allowed;
        // Never create another view. Even allowed targets reuse the one owned view.
        var ignored = maker.Ignore();
        if (!ignored) { metrics.Event("lab-popup-cancel-failed"); return; }
        if (!allowed) { Interlocked.Increment(ref metrics.PopupsBlocked); return; }
        if (!UrlSafety.TryNormalizeAddress(target, out var url) || Interlocked.Exchange(ref popupPending, 1) != 0) return;
        var ticket = Volatile.Read(ref generation);
        completions.Enqueue(() => {
            if (view != null && ticket == generation) { Interlocked.Exchange(ref popupPending, 0); navigate(url); }
        });
    }
    private void Navigation(object? sender, WebViewPolicyDecidedEventArgs args)
    {
        var maker = args.PolicyDecisionMaker;
        // API12 supplies a navigation type, but no reliable gesture/redirect metadata.
        var decision = Volatile.Read(ref policy).EvaluateNavigation(new NavigationRequest(
            Volatile.Read(ref site), maker.Url, NavigationKind.Unknown, null));
        if (decision.Allowed) maker.Use();
        else {
            if (maker.Ignore()) Interlocked.Increment(ref metrics.NavigationsBlocked);
            else metrics.Event("lab-navigation-cancel-failed");
        }
    }
    private void Entered(object? sender, EventArgs args) => QueueFullscreen(true);
    private void Exited(object? sender, EventArgs args) => QueueFullscreen(false);
    private void QueueFullscreen(bool value)
    {
        var ticket = Volatile.Read(ref generation);
        completions.Enqueue(() => { if (view != null && ticket == generation) fullscreen(value); });
    }
    private void Evaluate(string script, Action<string> action)
    {
        var current = view;
        if (current == null) return;
        var ticket = generation;
        try {
            current.EvaluateJavaScript(script, result => completions.Enqueue(() => {
                if (ReferenceEquals(view, current) && ticket == generation) action(result);
            }));
        } catch { metrics.Event("lab-script-unavailable"); }
    }
    private void Execute(string script)
    {
        try { view?.EvaluateJavaScript(script); }
        catch { metrics.Event("lab-script-unavailable"); }
    }
    public override bool Back()
    {
        if (view == null) return false;
        if (backUnavailable) { status("Wstecz nie odpowiada. Użyj Zamknij."); return true; }
        if (backDeadline != 0) return true;
        backDeadline = clock.ElapsedMilliseconds + 2000;
        var backTicket = ++backSequence;
        Evaluate("Boolean(document.fullscreenElement||document.webkitFullscreenElement).toString()", result => {
            if (backTicket != backSequence || backDeadline == 0 || clock.ElapsedMilliseconds >= backDeadline) return;
            backDeadline = 0;
            if (!bool.TryParse(result.Trim('"'), out var active)) { status("Nie odczytano stanu fullscreen. Użyj Zamknij."); return; }
            try {
                if (active) view!.ExitFullscreen();
                else if (view!.CanGoBack()) view.GoBack();
                else close();
            } catch { status("Wstecz jest niedostępne w tym silniku. Użyj Zamknij."); }
        });
        return true;
    }
    private bool Tick(object sender, Tizen.NUI.Timer.TickEventArgs args)
    {
        for (var i = 0; i < 32 && completions.TryDequeue(out var completion); i++) {
            try { completion(); } catch { metrics.Event("lab-ui-operation-failed"); }
        }
        if (backDeadline != 0 && clock.ElapsedMilliseconds >= backDeadline) {
            backSequence++; backDeadline = 0; backUnavailable = true;
            status("Silnik nie odpowiedział na Wstecz. Użyj Zamknij.");
        }
        if (view != null && !mediaPending && clock.ElapsedMilliseconds >= sampleAt) {
            // A missing JS response must not accumulate one native callback per interval.
            mediaPending = true;
            sampleAt = clock.ElapsedMilliseconds + 30000;
            Evaluate(PageScripts.ReadMedia, result => {
                mediaPending = false;
                try { metrics.Media(result); } catch { metrics.Event("lab-media-sample-invalid"); }
            });
        }
        return true;
    }
    public override void Detach(WebView page)
    {
        if (!ReferenceEquals(view, page)) return;
#if ISKO_RUNTIME_DIAGNOSTICS
        try { page.StopInspectorServer(); } catch { metrics.Event("inspector-stop-failed"); }
#endif
        // A failed unregister propagates: SingleViewOwner retains the view for retry.
        if (requestIntercepting) context?.RegisterHttpRequestInterceptedCallback(null!);
        requestIntercepting = false;
        page.NewWindowPolicyDecided -= Popup;
        page.NavigationPolicyDecided -= Navigation;
        page.FullscreenEntered -= Entered;
        page.FullscreenExited -= Exited;
        Interlocked.Increment(ref generation);
        try { page.EvaluateJavaScript(PageScripts.ReleaseMedia); } catch { metrics.Event("lab-media-release-script-failed"); }
        view = null; context = null; requestCallback = null; backDeadline = 0;
        while (completions.TryDequeue(out _)) { }
    }
    public override void Dispose()
    {
        if (timer != null) { timer.Stop(); timer.Tick -= Tick; timer.Dispose(); timer = null; }
    }
}
