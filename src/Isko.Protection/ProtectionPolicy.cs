using System;
using System.Collections.Generic;

namespace Isko.Protection
{
    public enum NavigationKind { Unknown, UserLink, Redirect, Script }

    public sealed class NavigationRequest
    {
        public string CurrentUrl { get; private set; }
        public string TargetUrl { get; private set; }
        public NavigationKind Kind { get; private set; }
        public bool? HasUserGesture { get; private set; }
        public NavigationRequest(string currentUrl, string targetUrl, NavigationKind kind, bool? hasUserGesture)
        { CurrentUrl = currentUrl; TargetUrl = targetUrl; Kind = kind; HasUserGesture = hasUserGesture; }
    }

    public sealed class PolicyDecision
    {
        public bool Allowed { get; private set; }
        public bool Supported { get; private set; }
        public string Reason { get; private set; }
        internal PolicyDecision(bool allowed, string reason, bool supported = true)
        { Allowed = allowed; Reason = reason; Supported = supported; }
    }

    // Original, deliberately tiny rule format: exact canonical host and exact escaped path.
    // No wildcards, regex, suffix matching, EasyList, scriptlets, or downloaded code.
    public sealed class RequestRule
    {
        public string Host { get; private set; }
        public string Path { get; private set; }
        public RequestRule(string host, string path)
        {
            Host = UrlSafety.NormalizeHost(host);
            if (String.IsNullOrEmpty(path) || !path.StartsWith("/", StringComparison.Ordinal) || path.Contains("?") || path.Contains("#"))
                throw new ArgumentException("An absolute path without query or fragment is required.", nameof(path));
            Path = path;
        }
        internal bool Matches(Uri uri) { return String.Equals(Host, uri.IdnHost, StringComparison.OrdinalIgnoreCase) && Path == uri.AbsolutePath; }
    }

    public sealed class ProtectionPolicy
    {
        private static readonly PolicyDecision RequestUnavailable = new PolicyDecision(true, "request-interception-unavailable", false);
        private static readonly PolicyDecision RequestDisabled = new PolicyDecision(true, "protection-disabled-or-site-exempt");
        private static readonly PolicyDecision RequestOutside = new PolicyDecision(true, "request-outside-http-rule-scope");
        private static readonly PolicyDecision RequestHostBlocked = new PolicyDecision(false, "exact-ad-host-rule");
        private static readonly PolicyDecision RequestPathBlocked = new PolicyDecision(false, "exact-host-path-rule");
        private static readonly PolicyDecision RequestFilterBlocked = new PolicyDecision(false, "filter-list-rule");
        private static readonly PolicyDecision RequestAllowed = new PolicyDecision(true, "no-matching-rule");
        private readonly HashSet<string> exceptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<RequestRule> requestRules = new List<RequestRule>();
        private FilterSet filters = FilterSet.Empty;
        private readonly HashSet<string> requestHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public bool Enabled { get; set; } = true;
        public void SetHostException(string host, bool exempt)
        { host = UrlSafety.NormalizeHost(host); if (exempt) exceptions.Add(host); else exceptions.Remove(host); }
        public void AddRequestRule(RequestRule rule)
        { if (rule == null) throw new ArgumentNullException(nameof(rule)); requestRules.Add(rule); }
        public void AddRequestHost(string host) { requestHosts.Add(UrlSafety.NormalizeHost(host)); }
        // Set before the policy is published; a published policy is never mutated.
        public void SetFilters(FilterSet set) { filters = set ?? FilterSet.Empty; }
        public FilterSet Filters { get { return filters; } }
        // The page URL is the same for every request of a page, so it is parsed once and remembered. The memo is a
        // single immutable object swapped by reference: a racing reader sees either the old or the new pair.
        private sealed class PageMemo
        {
            public readonly string Url, Host; // Host is null when Url is not a plain http(s) address
            public PageMemo(string url, string host) { Url = url; Host = host; }
        }
        private PageMemo pageMemo = new PageMemo(null, null);
        private string PageHostOf(string url)
        {
            var memo = System.Threading.Volatile.Read(ref pageMemo);
            if (memo.Url != null && String.Equals(memo.Url, url, StringComparison.Ordinal)) return memo.Host;
            Uri uri;
            string host = UrlSafety.TryHttpUri(url, out uri) ? uri.IdnHost : null;
            if (url != null) System.Threading.Volatile.Write(ref pageMemo, new PageMemo(url, host));
            return host;
        }
        private bool Exempt(string url)
        { string host; return exceptions.Count != 0 && (host = PageHostOf(url)) != null && exceptions.Contains(host); }

        public bool NeedsRequestInterception(string currentUrl) =>
            Enabled && (requestHosts.Count != 0 || requestRules.Count != 0 || filters.HasRequestRules) && !Exempt(currentUrl);

        // Pass current top-level page: site exceptions belong to the initiating site.
        public PolicyDecision EvaluatePopup(string currentUrl, string targetUrl)
        {
            if (!Enabled || Exempt(currentUrl)) return new PolicyDecision(true, "protection-disabled-or-site-exempt");
            return new PolicyDecision(false, "new-window-blocked");
        }

        // Popup decision with the app's own knowledge of recent user input (the native event has no gesture
        // metadata). Any accepted target still reuses the single view, so only same-site targets are allowed:
        // a cross-site window opened by a page would replace the page the user is watching.
        public PolicyDecision EvaluatePopup(string currentUrl, string targetUrl, bool recentUserInput)
        {
            if (!Enabled || Exempt(currentUrl)) return new PolicyDecision(true, "protection-disabled-or-site-exempt");
            Uri target, current;
            if (!UrlSafety.TryHttpUri(targetUrl, out target)) return new PolicyDecision(false, "unsafe-or-unsupported-url");
            if (requestHosts.Contains(target.IdnHost) || filters.Match(currentUrl, targetUrl).Blocked)
                return new PolicyDecision(false, "ad-host-popup");
            if (!recentUserInput) return new PolicyDecision(false, "popup-without-recent-user-input");
            if (UrlSafety.TryHttpUri(currentUrl, out current) && FilterSet.SameSite(current.IdnHost, target.IdnHost))
                return new PolicyDecision(true, "same-site-popup-after-user-input");
            return new PolicyDecision(false, "cross-site-popup-blocked");
        }

        public PolicyDecision EvaluateNavigation(NavigationRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            Uri target;
            if (!UrlSafety.TryHttpUri(request.TargetUrl, out target)) return new PolicyDecision(false, "unsafe-or-unsupported-url");
            if (!Enabled || Exempt(request.CurrentUrl)) return new PolicyDecision(true, "protection-disabled-or-site-exempt");
            if (requestHosts.Contains(target.IdnHost) || filters.Match(request.CurrentUrl, request.TargetUrl).Blocked)
                return new PolicyDecision(false, "exact-ad-host-navigation");
            Uri current;
            bool crossOrigin = UrlSafety.TryHttpUri(request.CurrentUrl, out current) &&
                (current.Scheme != target.Scheme || current.IdnHost != target.IdnHost || current.Port != target.Port);
            if (crossOrigin && request.HasUserGesture == false &&
                (request.Kind == NavigationKind.Script || request.Kind == NavigationKind.Redirect))
                return new PolicyDecision(false, "known-ungestured-cross-origin-navigation");
            // Unknown is not evidence of an unwanted redirect. Native event metadata may be incomplete.
            return new PolicyDecision(true, request.HasUserGesture == null ? "allowed-context-incomplete" : "allowed-navigation");
        }

        public PolicyDecision EvaluateRequest(string url, bool interceptionAvailable)
        { return EvaluateRequest(null, url, interceptionAvailable); }
        public PolicyDecision EvaluateRequest(string currentUrl, string url, bool interceptionAvailable)
        {
            if (!interceptionAvailable) return RequestUnavailable;
            if (!Enabled || Exempt(currentUrl)) return RequestDisabled;
            Uri uri;
            if (!UrlSafety.TryHttpUri(url, out uri)) return RequestOutside;
            if (requestHosts.Count != 0 && requestHosts.Contains(uri.IdnHost)) return RequestHostBlocked;
            foreach (var rule in requestRules) if (rule.Matches(uri)) return RequestPathBlocked;
            if (filters.HasRequestRules && filters.Match(PageHostOf(currentUrl), uri).Blocked) return RequestFilterBlocked;
            return RequestAllowed;
        }

        public string GetCosmeticScript(string url)
        {
            if (!Enabled || Exempt(url)) return "(function(){var s=document.getElementById('isko-cosmetic-v1');if(s)s.remove();})();";
            string css = "[data-isko-ad]{display:none!important}";
            string extra = filters.BuildCosmeticCss(url);
            if (extra != null) css += extra;
            // Selectors are validated at parse time: no quote or backslash that could end the JS string.
            return "(function(){if(document.getElementById('isko-cosmetic-v1'))return;var s=document.createElement('style');s.id='isko-cosmetic-v1';s.textContent='" + css + "';(document.head||document.documentElement).appendChild(s);})();";
        }
    }
}
