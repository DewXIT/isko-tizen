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
                throw new ArgumentException("An absolute path without query or fragment is required.", "path");
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
        private static readonly PolicyDecision RequestAllowed = new PolicyDecision(true, "no-matching-rule");
        private readonly HashSet<string> exceptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<RequestRule> requestRules = new List<RequestRule>();
        private readonly HashSet<string> requestHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public bool Enabled { get; set; } = true;
        public void SetHostException(string host, bool exempt)
        { host = UrlSafety.NormalizeHost(host); if (exempt) exceptions.Add(host); else exceptions.Remove(host); }
        public void AddRequestRule(RequestRule rule)
        { if (rule == null) throw new ArgumentNullException("rule"); requestRules.Add(rule); }
        public void AddRequestHost(string host) { requestHosts.Add(UrlSafety.NormalizeHost(host)); }
        private bool Exempt(string url)
        { Uri uri; return exceptions.Count != 0 && UrlSafety.TryHttpUri(url, out uri) && exceptions.Contains(uri.IdnHost); }

        public bool NeedsRequestInterception(string currentUrl) =>
            Enabled && (requestHosts.Count != 0 || requestRules.Count != 0) && !Exempt(currentUrl);

        public PolicyDecision EvaluatePopup(string targetUrl)
        { return EvaluatePopup(null, targetUrl); }
        // Pass current top-level page: site exceptions belong to the initiating site.
        public PolicyDecision EvaluatePopup(string currentUrl, string targetUrl)
        {
            if (!Enabled || Exempt(currentUrl)) return new PolicyDecision(true, "protection-disabled-or-site-exempt");
            return new PolicyDecision(false, "new-window-blocked");
        }

        public PolicyDecision EvaluateNavigation(NavigationRequest request)
        {
            if (request == null) throw new ArgumentNullException("request");
            Uri target;
            if (!UrlSafety.TryHttpUri(request.TargetUrl, out target)) return new PolicyDecision(false, "unsafe-or-unsupported-url");
            if (!Enabled || Exempt(request.CurrentUrl)) return new PolicyDecision(true, "protection-disabled-or-site-exempt");
            if (requestHosts.Contains(target.IdnHost)) return new PolicyDecision(false, "exact-ad-host-navigation");
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
            if (requestHosts.Contains(uri.IdnHost)) return RequestHostBlocked;
            foreach (var rule in requestRules) if (rule.Matches(uri)) return RequestPathBlocked;
            return RequestAllowed;
        }

        public string GetCosmeticScript(string url)
        {
            if (!Enabled || Exempt(url)) return "(function(){var s=document.getElementById('isko-cosmetic-v1');if(s)s.remove();})();";
            return "(function(){if(document.getElementById('isko-cosmetic-v1'))return;var s=document.createElement('style');s.id='isko-cosmetic-v1';s.textContent='[data-isko-ad]{display:none!important}';(document.head||document.documentElement).appendChild(s);})();";
        }
    }
}
