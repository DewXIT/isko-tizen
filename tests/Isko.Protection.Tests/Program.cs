using System;
using System.Linq;
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
        FilterRuleTests();
        // The remembered page host must follow the page, not stick to the first one.
        var memo = new ProtectionPolicy(); memo.SetFilters(FilterSet.Parse("||ads.example.net^$third-party")); memo.SetHostException("a.example.org", true);
        const string adUrl = "https://ads.example.net/x.js";
        Check(memo.EvaluateRequest("https://a.example.org/", adUrl, true).Allowed, "exception page allows");
        Check(!memo.EvaluateRequest("https://b.example.org/", adUrl, true).Allowed, "next page is protected again");
        Check(memo.EvaluateRequest("https://a.example.org/live", adUrl, true).Allowed, "back on the exception page");
        Check(!memo.EvaluateRequest(null, adUrl, true).Allowed && !memo.EvaluateRequest("", adUrl, true).Allowed, "unknown page is not exempt");
        Check(!memo.EvaluateRequest("https://b.example.org/", adUrl, true).Allowed, "empty page URL did not poison the memo");
        // Differential check of the allocation-free same-site comparison against the plain label-array rule.
        var hosts = new[] { "example.com", "www.example.com", "a.b.example.com", "example.co.uk", "shop.example.co.uk", "other.co.uk", "example.com.pl",
            "x.example.com.pl", "co.uk", "localhost", "ads.example.net", "example.net", "a.example.org", "203.0.113.5", "198.51.100.7", "xn--e1afmkfd.xn--p1ai", "a.xn--e1afmkfd.xn--p1ai" };
        Func<string, string> key = h =>
        {
            var l = h.Split('.'); var generic = new[] { "co", "com", "org", "net", "gov", "edu", "ac", "or", "ne", "go" };
            int keep = l.Length >= 3 && l[l.Length - 1].Length == 2 && Array.IndexOf(generic, l[l.Length - 2]) >= 0 ? 3 : 2;
            return l.Length <= keep ? h : string.Join(".", l, l.Length - keep, keep);
        };
        Func<string, bool> isIp = h => h.IndexOf(':') >= 0 || h.All(c => (c >= '0' && c <= '9') || c == '.');
        var diff = FilterSet.Parse("||ads.example.net^\n||example.net^\n||example.org^\n||example.com^\n||example.co.uk^\n||other.co.uk^\n||example.com.pl^");
        int pairs = 0;
        foreach (var pageHost in hosts) foreach (var requestHost in hosts)
        {
            bool same = pageHost == requestHost || (!isIp(pageHost) && !isIp(requestHost) && key(pageHost) == key(requestHost));
            var listed = !isIp(requestHost) && new[] { "ads.example.net", "example.net", "a.example.org", "example.com", "www.example.com", "a.b.example.com", "example.co.uk", "shop.example.co.uk", "other.co.uk", "example.com.pl", "x.example.com.pl" }.Contains(requestHost);
            bool expectBlocked = listed && !same;
            bool actual = diff.Match("https://" + pageHost + "/", "https://" + requestHost + "/x").Blocked;
            Check(actual == expectBlocked, "same-site differential " + pageHost + " -> " + requestHost);
            pairs++;
        }
        Check(pairs == hosts.Length * hosts.Length, "differential covered every pair");
        Check(DownloadPolicy.Blocks("application/zip", false) && DownloadPolicy.Blocks("Application/ZIP; charset=binary", false), "archive MIME refused");
        Check(DownloadPolicy.Blocks("application/x-msdownload", false) && DownloadPolicy.Blocks("application/vnd.android.package-archive", false), "executable MIME refused");
        Check(DownloadPolicy.Blocks("text/html", true) && DownloadPolicy.Blocks(null, true), "engine-decided download refused whatever the type");
        Check(!DownloadPolicy.Blocks("text/html", false) && !DownloadPolicy.Blocks("application/octet-stream", false) && !DownloadPolicy.Blocks(null, false) && !DownloadPolicy.Blocks("application/vnd.apple.mpegurl", false), "documents, streams and bare octet-stream are not refused here");
        Check(DownloadPolicy.Blocks("application/zip", false), "download refusal does not depend on the protection switch");
        Console.WriteLine("PASS: " + count + " protection assertions (pure policy only, not Tizen integration)");
    }
    static void FilterRuleTests()
    {
        const string page = "https://watch.example.org/live";
        var f = FilterSet.Parse(string.Join("\n", new[] {
            "! comment", "", "||ads.example.net^$third-party", "||tracker.example.com/pixel*", "@@||ads.example.net/allowed^",
            "||cdn.example.co.uk/banners/", "||bad.example\\.org^", "||com^", "||co.uk^", "||x.example.net^$script", "/ads/*",
            "##.adsbygoogle", "watch.example.org###sponsor", "##div", "##.video-player", "##.a{b}", "##.x:has(.y)" }));
        Check(f.BlockRuleCount == 3 && f.ExceptionRuleCount == 1, "rule counts");
        Check(f.RejectedLineCount >= 7, "regex, bare TLD, unknown option, bare pattern and unsafe selectors rejected");
        Check(f.CosmeticRuleCount == 2, "only safe selectors accepted");
        Check(f.Match(page, "https://ads.example.net/x.js").Blocked, "host rule blocks");
        Check(f.Match(page, "https://a.b.ads.example.net/x.js").Blocked, "host rule covers subdomains");
        Check(!f.Match(page, "https://notads.example.net/x.js").Blocked, "host rule is label-bound");
        Check(f.Match(page, "https://ads.example.net/allowed/x.js").Excepted && !f.Match(page, "https://ads.example.net/allowed/x.js").Blocked, "exception wins");
        Check(f.Match(page, "https://ads.example.net/allowed/x.js").RuleId == 5, "exception reports its line number");
        Check(f.Match(page, "https://ads.example.net/other").RuleId == 3, "block reports its line number");
        Check(f.Match(page, "https://tracker.example.com/pixel.gif?x=1").Blocked, "path prefix rule blocks");
        Check(!f.Match(page, "https://tracker.example.com/app.js").Blocked, "path rule does not block other paths");
        Check(f.Match(page, "https://cdn.example.co.uk/banners/1.png").Blocked, "two-part suffix host rule accepted");
        Check(!f.Match("https://ads.example.net/", "https://ads.example.net/x.js").Blocked, "same-host request never blocked");
        Check(!f.Match("https://www.example.net/", "https://ads.example.net/x.js").Blocked, "same-site request never blocked");
        Check(!f.Match("https://shop.example.co.uk/", "https://cdn.example.co.uk/banners/1.png").Blocked, "same-site under two-part suffix never blocked");
        Check(f.Match("https://other.co.uk/", "https://cdn.example.co.uk/banners/1.png").Blocked, "different site under two-part suffix blocked");
        Check(f.Match("", "https://ads.example.net/x.js").Blocked, "unknown page treated as third-party for listed hosts");
        Check(!f.Match(page, "ftp://ads.example.net/x").Blocked && !f.Match(page, "not a url").Blocked, "non-http requests ignored");
        Check(f.Match(page, "https://ADS.Example.NET/X").Blocked, "host case-insensitive");
        var hits = f.TopHits(5);
        Check(hits.Count >= 1 && hits[0].RuleId == 3 && hits[0].Count >= 3, "hit counters report rule line numbers only");
        Check(FilterSet.GlobPrefix("/a/*/c^", "/a/b/c") && FilterSet.GlobPrefix("/a/*/c^", "/a/x/y/c?q=1") && !FilterSet.GlobPrefix("/a/*/c^", "/a/b/cd"), "glob with separator");
        Check(FilterSet.GlobPrefix("/Ads/", "/ads/x"), "glob case-insensitive prefix");
        var css = f.BuildCosmeticCss(page);
        Check(css != null && css.Contains(".adsbygoogle") && css.Contains("#sponsor") && css.EndsWith("{display:none!important}"), "cosmetic css for matching host");
        Check(!f.BuildCosmeticCss("https://other.example.org/").Contains("#sponsor"), "host cosmetic rule is host-scoped");
        Check(f.BuildCosmeticCss("https://www.watch.example.org/").Contains("#sponsor"), "host cosmetic rule covers subdomains");
        Check(!f.BuildCosmeticCss("https://notwatch.example.org/").Contains("#sponsor"), "host cosmetic rule is label-bound");

        var starter = FilterSet.Parse(StarterRules.Text);
        Check(starter.RejectedLineCount == 0 && starter.BlockRuleCount >= 25, "starter list parses with no rejected lines");
        Check(starter.Match(page, "https://pubads.g.doubleclick.net/gampad/ads").Blocked, "starter blocks ad network");
        Check(!starter.Match("https://doubleclick.net/", "https://pubads.g.doubleclick.net/x").Blocked, "starter never blocks the page's own site");
        Check(!starter.Match(page, "https://watch.example.org/player.js").Blocked && !starter.Match(page, "https://cdn.example.com/hls.js").Blocked, "starter leaves first-party and ordinary hosts alone");

        var p = new ProtectionPolicy();
        p.SetFilters(starter);
        Check(p.NeedsRequestInterception(page), "filter rules need native interception");
        Check(!p.EvaluateRequest(page, "https://exoclick.com/ad.js", true).Allowed, "policy blocks listed request");
        Check(p.EvaluateRequest(page, "https://watch.example.org/app.js", true).Allowed, "policy allows first-party request");
        Check(!p.EvaluateNavigation(new NavigationRequest(page, "https://popads.net/x", NavigationKind.UserLink, true)).Allowed, "navigation to listed ad host blocked");
        Check(p.EvaluatePopup(page, "https://watch.example.org/player", true).Allowed, "same-site popup after input allowed");
        Check(!p.EvaluatePopup(page, "https://watch.example.org/player", false).Allowed, "popup without input blocked");
        Check(!p.EvaluatePopup(page, "https://popads.net/x", true).Allowed, "listed popup blocked even after input");
        Check(!p.EvaluatePopup(page, "https://unlisted.example.net/x", true).Allowed, "cross-site popup blocked after input");
        Check(!p.EvaluatePopup(page, "javascript:alert(1)", true).Allowed, "unsafe popup target blocked");
        Check(p.GetCosmeticScript(page).Contains(".adsbygoogle") && !p.GetCosmeticScript(page).Contains("'.") , "cosmetic script embeds starter selectors safely");
        p.SetHostException("watch.example.org", true);
        Check(p.EvaluateRequest(page, "https://exoclick.com/ad.js", true).Allowed && p.EvaluatePopup(page, "https://popads.net/x", false).Allowed, "site exception disables filter rules and popup policy");
        Check(!p.NeedsRequestInterception(page), "exception detaches interceptor for filter rules");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var q = new ProtectionPolicy(); q.SetFilters(starter);
        for (int i = 0; i < 20000; i++) q.EvaluateRequest(page, "https://cdn" + (i % 50) + ".example.com/a/b/c" + i + ".js?x=1", true);
        Check(sw.ElapsedMilliseconds < 2000, "20000 lookups complete quickly (sanity, not a TV benchmark)");
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
