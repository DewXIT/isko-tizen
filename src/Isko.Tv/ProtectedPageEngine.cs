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
    private WebContext? context, releasedContext;
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
    private WebContext.DownloadStartedCallback? downloadCallback;
    private static readonly FilterSet StarterFilters = FilterSet.Parse(StarterRules.Text);
    private const long GestureWindowMs = 2000;
    private long lastInputAt = -GestureWindowMs - 1;

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
        page.ResponsePolicyDecided += Response;
        page.FullscreenEntered += Entered;
        page.FullscreenExited += Exited;
        context = page.Context;
        // Context callback may run on a worker thread. Its body touches no NUI view.
        requestCallback = Intercept;
        // A download that starts anyway means the response refusal did not hold; only a counter is kept (no URL).
        downloadCallback = _ => Interlocked.Increment(ref metrics.DownloadsStarted);
        try { context.RegisterDownloadStartedCallback(downloadCallback); } catch { metrics.Event("download-callback-unavailable"); }
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
        Execute(PageScripts.SweepOverlays(enabled && !HasException()));
        Execute(PageScripts.BlockDownloadLinks);
        sampleAt = clock.ElapsedMilliseconds + 30000;
    }
    private bool HasException() => Uri.TryCreate(Volatile.Read(ref site), UriKind.Absolute, out var uri) && exceptions.Contains(uri.IdnHost);
    private void Rebuild()
    {
        var next = new ProtectionPolicy { Enabled = enabled };
        next.SetFilters(StarterFilters);
        foreach (var host in exceptions) next.SetHostException(host, true);
        if (fixture && Uri.TryCreate(Volatile.Read(ref site), UriKind.Absolute, out var uri) &&
            (uri.Scheme == "http" || uri.Scheme == "https")) next.AddRequestRule(new RequestRule(uri.IdnHost, "/ad-probe"));
        Volatile.Write(ref policy, next);
        UpdateRequestInterception();
    }
    // The native interceptor is registered once per page and removed only when the page closes. Unregistering it
    // while the page was live (protection off, site exception) left the TV page's requests stalled; a disabled or
    // exempt policy simply lets every request through (Ignore) instead.
    private void UpdateRequestInterception()
    {
        if (context == null) return;
        if (!requestIntercepting) {
            try {
                context.RegisterHttpRequestInterceptedCallback(requestCallback!);
                requestIntercepting = true; requestHookFailed = false;
            } catch { requestHookFailed = true; metrics.RequestBlocking = "hook-toggle-failed"; metrics.Event("request-hook-toggle-failed"); return; }
        }
        metrics.RequestBlocking = Volatile.Read(ref policy).NeedsRequestInterception(Volatile.Read(ref site))
            ? "hook-unverified" : "disabled-or-site-exempt";
    }
    public override void UserInput() => Volatile.Write(ref lastInputAt, clock.ElapsedMilliseconds);
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
        Execute(PageScripts.SweepOverlays(enabled && !HasException()));
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
        var recentInput = clock.ElapsedMilliseconds - Volatile.Read(ref lastInputAt) <= GestureWindowMs;
        var allowed = Volatile.Read(ref policy).EvaluatePopup(Volatile.Read(ref site), target, recentInput).Allowed;
        // Never create another view. Even allowed targets reuse the one owned view.
        var ignored = maker.Ignore();
        if (!ignored) { metrics.Event("engine-popup-cancel-failed"); return; }
        if (!allowed) {
            Interlocked.Increment(ref metrics.PopupsBlocked);
            var blockedTicket = Volatile.Read(ref generation);
            completions.Enqueue(() => { if (view != null && blockedTicket == generation) status("Zablokowano nowe okno. Wyjątek dla strony: menu → Ochrona"); });
            return;
        }
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
            else metrics.Event("engine-navigation-cancel-failed");
        }
    }
    // Downloads are refused unconditionally: an ad page can start one (an installer or archive) from a click.
    private void Response(object? sender, WebViewPolicyDecidedEventArgs args)
    {
        var maker = args.PolicyDecisionMaker;
        var engineDownload = maker.PolicyDecisionType == WebPolicyDecisionMaker.DecisionType.Download;
        if (!DownloadPolicy.Blocks(maker.ResponseMime, engineDownload)) { maker.Use(); return; }
        if (maker.Ignore()) {
            Interlocked.Increment(ref metrics.DownloadsBlocked);
            var ticket = Volatile.Read(ref generation);
            completions.Enqueue(() => { if (view != null && ticket == generation) status("Pobieranie plików jest zablokowane."); });
        } else metrics.Event("download-cancel-failed");
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
        } catch { metrics.Event("engine-script-unavailable"); }
    }
    private void Execute(string script)
    {
        try { view?.EvaluateJavaScript(script); }
        catch { metrics.Event("engine-script-unavailable"); }
    }
    public override bool CanGoBack { get { try { return view?.CanGoBack() ?? false; } catch { return false; } } }
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
            try { completion(); } catch { metrics.Event("engine-ui-operation-failed"); }
        }
        if (backDeadline != 0 && clock.ElapsedMilliseconds >= backDeadline) {
            backSequence++; backDeadline = 0; backUnavailable = true;
            status("Silnik nie odpowiedział na Wstecz. Użyj Zamknij.");
        }
        if (view != null && !mediaPending && clock.ElapsedMilliseconds >= sampleAt) {
            // A missing JS response must not accumulate one native callback per interval.
            mediaPending = true;
            sampleAt = clock.ElapsedMilliseconds + 30000;
            // Rule ids are line numbers of the bundled list; no URL leaves the policy.
            foreach (var hit in Volatile.Read(ref policy).Filters.TopHits(5)) metrics.Event("rule-hit-" + hit.RuleId, hit.Count);
            Evaluate(PageScripts.ReadMedia, result => {
                mediaPending = false;
                try { metrics.Media(result); } catch { metrics.Event("engine-media-sample-invalid"); }
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
        try { context?.RegisterDownloadStartedCallback(null!); } catch { metrics.Event("download-callback-unregister-failed"); }
        page.NewWindowPolicyDecided -= Popup;
        page.NavigationPolicyDecided -= Navigation;
        page.ResponsePolicyDecided -= Response;
        page.FullscreenEntered -= Entered;
        page.FullscreenExited -= Exited;
        Interlocked.Increment(ref generation);
        try { page.EvaluateJavaScript(PageScripts.ReleaseMedia); } catch { metrics.Event("engine-media-release-script-failed"); }
        // Drop rendered tile bitmaps before the view goes away; best effort, never blocks closing.
        try { page.ClearAllTilesResources(); } catch { metrics.Event("tiles-clear-failed"); }
        releasedContext = context;
        view = null; context = null; requestCallback = null; downloadCallback = null; backDeadline = 0;
        while (completions.TryDequeue(out _)) { }
    }
    public override void Released()
    {
        var ctx = releasedContext; releasedContext = null;
        try { ctx?.FreeUnusedMemory(); } catch { metrics.Event("free-unused-memory-failed"); }
        // Closing a page is the one moment nothing is playing: compact the managed heap here, never during playback.
        GC.Collect(2, GCCollectionMode.Forced, true, true);
    }
    public override void Dispose()
    {
        if (timer != null) { timer.Stop(); timer.Tick -= Tick; timer.Dispose(); timer = null; }
    }
}
