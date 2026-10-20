using System;
using Isko.Protection;

internal static class Program
{
    static int count;
    static void Check(bool condition, string name)
    { if (!condition) throw new Exception("FAIL: " + name); count++; }
    static void Main()
    {
        var p = new ProtectionPolicy();
        const string site = "http://127.0.0.1:8787/";
        const string external = "http://127.0.0.1:8788/";
        Check(!p.EvaluatePopup(site, external).Allowed, "popup denied");
        Check(p.EvaluateNavigation(new NavigationRequest(site, external, NavigationKind.UserLink, true)).Allowed, "ordinary link preserved");
        Check(!p.EvaluateNavigation(new NavigationRequest(site, external, NavigationKind.Script, false)).Allowed, "known ungestured cross-origin script denied");
        Check(p.EvaluateNavigation(new NavigationRequest(site, external, NavigationKind.Unknown, null)).Allowed, "unknown context not invented");
        Check(p.EvaluateNavigation(new NavigationRequest(site, external, NavigationKind.Redirect, null)).Allowed, "redirect gesture unavailable not inferred");
        Check(p.EvaluateNavigation(new NavigationRequest(site, site + "next", NavigationKind.Redirect, false)).Allowed, "same origin redirect allowed");
        Check(!p.EvaluateNavigation(new NavigationRequest(site, "javascript:alert(1)", NavigationKind.UserLink, true)).Allowed, "unsafe navigation denied");
        p.AddRequestRule(new RequestRule("127.0.0.1", "/ads/probe.png"));
        Check(!p.EvaluateRequest(site, site + "ads/probe.png?run=1", true).Allowed, "rule matches exact host and path irrespective query");
        Check(p.EvaluateRequest(site, "http://ads.127.0.0.1/ads/probe.png", true).Allowed, "host match is not suffix");
        Check(p.EvaluateRequest(site, site + "ads/probe.png.backup", true).Allowed, "path match is exact");
        var unsupported = p.EvaluateRequest(site, site + "ads/probe.png", false);
        Check(unsupported.Allowed && !unsupported.Supported, "unavailable interception is not a blocked request");
        p.SetHostException("127.0.0.1", true);
        Check(p.EvaluatePopup(site, external).Allowed, "initiator exception allows popup");
        Check(p.EvaluateRequest(site, site + "ads/probe.png", true).Allowed, "initiator exception allows request");
        Check(!p.EvaluatePopup("https://example.org/", site).Allowed, "target exemption does not exempt initiator");
        p.SetHostException("127.0.0.1", false);
        Check(!p.EvaluatePopup(site, external).Allowed, "exception removable");
        p.Enabled = false;
        Check(p.EvaluatePopup(site, external).Allowed, "global off allows popup");
        Check(p.EvaluateRequest(site, site + "ads/probe.png", true).Allowed, "global off allows request");
        Check(p.GetCosmeticScript(site).Contains("s.remove()"), "disable removes cosmetic CSS");
        string normalized;
        Check(UrlSafety.TryNormalizeAddress("example.org", out normalized) && normalized == "https://example.org/", "address normalization");
        Check(!UrlSafety.TryNormalizeAddress("https://user:secret@example.org/", out normalized), "credentials rejected");
        Check(!UrlSafety.TryNormalizeAddress("https://example.org/\nsecret", out normalized), "controls rejected");
        Check(UrlSafety.ForPersistence("https://example.org/?token=secret") == null, "signed query not persisted");
        Check(UrlSafety.ForPersistence("https://example.org/secret") == null, "path credentials not persisted");
        Check(UrlSafety.ForPersistence("https://example.org/#secret") == null, "fragment credentials not persisted");
        Check(UrlSafety.ForPersistence("https://example.org/") == "https://example.org/", "safe root can persist");
        Check(UrlSafety.ForDiagnostics("https://example.org/private?token=secret#secret") == "https://example.org", "diagnostic URL redacted");
        var lab = new ProtectionPolicy();
        Check(!lab.NeedsRequestInterception(site), "No request rules need no native interceptor");
        lab.AddRequestHost("pubads.g.doubleclick.net");
        Check(lab.NeedsRequestInterception(site), "An enabled host rule needs native interception");
        lab.AddRequestHost("pagead2.googlesyndication.com");
        Check(!lab.EvaluateRequest(site, "https://pubads.g.doubleclick.net/any/path?secret=1", true).Allowed, "exact ad host covers arbitrary path");
        Check(lab.EvaluateRequest(site, "https://other.pubads.g.doubleclick.net/", true).Allowed, "ad host is not suffix rule");
        Check(!lab.EvaluateNavigation(new NavigationRequest(site, "https://pagead2.googlesyndication.com/", NavigationKind.Unknown, null)).Allowed, "known ad host navigation blocked without inventing gesture");
        Check(lab.EvaluateNavigation(new NavigationRequest(site, external, NavigationKind.Unknown, null)).Allowed, "ordinary unknown navigation remains allowed");
        var native = new FakeRequest("https://pubads.g.doubleclick.net/ad");
        var completion = RequestCompletion.Apply(native, lab, site);
        Check(native.Calls == 1 && native.Block && completion.BlockRequested && completion.Completed, "blocked request completed exactly once");
        native = new FakeRequest(external);
        completion = RequestCompletion.Apply(native, lab, site);
        Check(native.Calls == 1 && !native.Block && completion.Completed, "ordinary request continued exactly once");
        native = new FakeRequest("https://pubads.g.doubleclick.net/ad") { CompleteResult = false };
        completion = RequestCompletion.Apply(native, lab, site);
        Check(native.Calls == 1 && completion.BlockRequested && !completion.Completed, "failed native block not retried or counted successful");
        native = new FakeRequest("https://pubads.g.doubleclick.net/ad") { ThrowOnComplete = true };
        completion = RequestCompletion.Apply(native, lab, site);
        Check(native.Calls == 1 && !completion.Completed, "throwing terminal operation not retried");
        native = new FakeRequest(external) { ThrowOnUrl = true };
        completion = RequestCompletion.Apply(native, lab, site);
        Check(native.Calls == 1 && !native.Block && completion.Completed, "URL read failure continues once");
        lab.SetHostException("127.0.0.1", true);
        Check(!lab.NeedsRequestInterception(site), "An initiating-site exception can detach the interceptor");
        native = new FakeRequest("https://pubads.g.doubleclick.net/ad");
        RequestCompletion.Apply(native, lab, site);
        Check(native.Calls == 1 && !native.Block, "site exception reaches native continuation");
        lab.SetHostException("127.0.0.1", false); lab.Enabled = false;
        Check(!lab.NeedsRequestInterception(site), "Global off can detach the interceptor");
        native = new FakeRequest("https://pubads.g.doubleclick.net/ad");
        RequestCompletion.Apply(native, lab, site);
        Check(native.Calls == 1 && !native.Block, "protection off reaches native continuation");
        Console.WriteLine("PASS: " + count + " protection assertions (pure policy only, not Tizen integration)");
    }
    sealed class FakeRequest : IRequestCompletion
    {
        readonly string url;
        public int Calls;
        public bool Block, ThrowOnUrl, ThrowOnComplete;
        public bool CompleteResult = true;
        public FakeRequest(string url) { this.url = url; }
        public string Url { get { if (Calls != 0 || ThrowOnUrl) throw new InvalidOperationException(); return url; } }
        public bool Complete(bool block)
        {
            Calls++; Block = block;
            if (Calls != 1 || ThrowOnComplete) throw new InvalidOperationException();
            return CompleteResult;
        }
    }
}
