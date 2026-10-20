using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Isko.Protection
{
    public struct FilterMatch
    {
        public bool Blocked { get; }
        public bool Excepted { get; }
        // 1-based line number in the source list; 0 when nothing matched.
        public int RuleId { get; }
        public FilterMatch(bool blocked, bool excepted, int ruleId) { Blocked = blocked; Excepted = excepted; RuleId = ruleId; }
    }

    public struct RuleHit
    {
        public int RuleId { get; }
        public int Count { get; }
        public RuleHit(int ruleId, int count) { RuleId = ruleId; Count = count; }
    }

    // A deliberately small, original rule language. Lines (anything else is rejected, never guessed):
    //   ! comment
    //   ||example.com^              block a host and its subdomains
    //   ||example.com/path*         block a host-anchored path prefix ('*' any text, '^' separator or end)
    //   @@||example.com^            exception: overrides block rules for the same request
    //   rule$third-party            accepted; every network rule already applies to third-party requests only
    //   ##.selector / host##.sel    hide elements by a simple class/id/attribute selector
    // Safety invariants, because breaking the page is worse than showing an ad:
    //   * a request to the page's own site is never blocked (launchers and players live there);
    //   * unknown options, regexes, bare TLD/public-suffix hosts and tag-only or player-like
    //     cosmetic selectors are rejected at parse time.
    // The request type is not available from the native interceptor, so type options are not supported.
    public sealed class FilterSet
    {
        private sealed class NetRule
        {
            public int Index;
            public int LineNumber;
            public bool Exception;
            public string Path; // null: whole host; otherwise a glob matched as a prefix of path+query
        }

        private const int MaxCosmeticSelectors = 200;
        private const int MaxCosmeticChars = 16384;
        private static readonly string[] PlayerWords =
            { "video", "player", "plyr", "vjs", "jw", "audio", "embed", "object", "iframe", "stream", "controls", "frame" };

        private readonly Dictionary<string, List<NetRule>> byHost = new Dictionary<string, List<NetRule>>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> genericCosmetic = new List<string>();
        private readonly Dictionary<string, List<string>> hostCosmetic = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly List<int> lineByIndex = new List<int>();
        private int[] hits = new int[0];

        public static readonly FilterSet Empty = new FilterSet();

        public int BlockRuleCount { get; private set; }
        public int ExceptionRuleCount { get; private set; }
        public int CosmeticRuleCount { get; private set; }
        public int RejectedLineCount { get; private set; }
        public bool HasRequestRules { get { return byHost.Count != 0; } }

        public static FilterSet Parse(string text)
        {
            var set = new FilterSet();
            if (text == null) return set;
            var lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0 || line[0] == '!') continue;
                bool ok;
                try { ok = set.AddLine(line, i + 1); } catch (ArgumentException) { ok = false; }
                if (!ok) set.RejectedLineCount++;
            }
            set.hits = new int[set.lineByIndex.Count];
            return set;
        }

        private bool AddLine(string line, int lineNumber)
        {
            int cosmetic = line.IndexOf("##", StringComparison.Ordinal);
            if (cosmetic >= 0) return AddCosmetic(line.Substring(0, cosmetic), line.Substring(cosmetic + 2));
            bool exception = line.StartsWith("@@", StringComparison.Ordinal);
            if (exception) line = line.Substring(2);
            if (!line.StartsWith("||", StringComparison.Ordinal)) return false;
            line = line.Substring(2);
            int dollar = line.LastIndexOf('$');
            if (dollar >= 0)
            {
                foreach (var option in line.Substring(dollar + 1).Split(','))
                    if (option.Trim() != "third-party") return false;
                line = line.Substring(0, dollar);
            }
            int hostEnd = 0;
            while (hostEnd < line.Length && line[hostEnd] != '^' && line[hostEnd] != '/' && line[hostEnd] != '*') hostEnd++;
            string host = line.Substring(0, hostEnd).ToLowerInvariant();
            if (!ValidRuleHost(host)) return false;
            string rest = line.Substring(hostEnd);
            string path = null;
            if (rest.Length != 0 && rest != "^")
            {
                if (rest[0] != '/') return false;
                foreach (char c in rest) if (c <= ' ' || c > '~') return false;
                path = rest;
            }
            var rule = new NetRule { Index = lineByIndex.Count, LineNumber = lineNumber, Exception = exception, Path = path };
            lineByIndex.Add(lineNumber);
            List<NetRule> list;
            if (!byHost.TryGetValue(host, out list)) { list = new List<NetRule>(); byHost[host] = list; }
            list.Add(rule);
            if (exception) ExceptionRuleCount++; else BlockRuleCount++;
            return true;
        }

        private bool AddCosmetic(string hostPart, string selector)
        {
            selector = selector.Trim();
            if (!SafeSelector(selector)) return false;
            hostPart = hostPart.Trim().ToLowerInvariant();
            if (hostPart.Length == 0) genericCosmetic.Add(selector);
            else
            {
                if (!ValidRuleHost(hostPart)) return false;
                List<string> list;
                if (!hostCosmetic.TryGetValue(hostPart, out list)) { list = new List<string>(); hostCosmetic[hostPart] = list; }
                list.Add(selector);
            }
            CosmeticRuleCount++;
            return true;
        }

        private static bool ValidRuleHost(string host)
        {
            if (host.Length < 4 || host.Length > 253 || host.IndexOf('.') < 0) return false;
            foreach (char c in host)
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '.')) return false;
            if (host[0] == '.' || host[0] == '-' || host[host.Length - 1] == '.' || host.Contains("..")) return false;
            var labels = host.Split('.');
            return labels.Length >= (IsTwoPartSuffix(labels) ? 3 : 2);
        }

        // Simple, original selectors only: no pseudo-classes, functions, quotes of the other kind or escapes.
        private static bool SafeSelector(string s)
        {
            if (s.Length < 3 || s.Length > 120) return false;
            bool anchor = false;
            foreach (char c in s)
            {
                bool letter = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
                if (c == '.' || c == '#' || c == '[') anchor = true;
                if (!(letter || c == '-' || c == '_' || c == '.' || c == '#' || c == '[' || c == ']' || c == '=' ||
                      c == '"' || c == '*' || c == '^' || c == '$' || c == '~' || c == '|' || c == ' ' || c == '>')) return false;
            }
            if (!anchor) return false;
            var lower = s.ToLowerInvariant();
            foreach (var word in PlayerWords) if (lower.Contains(word)) return false;
            return true;
        }

        // ---- network matching ----

        public FilterMatch Match(string pageUrl, string requestUrl)
        {
            if (byHost.Count == 0) return default(FilterMatch);
            Uri request;
            if (!UrlSafety.TryHttpUri(requestUrl, out request)) return default(FilterMatch);
            string requestHost = request.IdnHost;
            Uri page;
            if (UrlSafety.TryHttpUri(pageUrl, out page) && SameSite(page.IdnHost, requestHost)) return default(FilterMatch);

            string target = request.PathAndQuery;
            NetRule block = null;
            string host = requestHost;
            bool ip = IsIpLiteral(host);
            while (true)
            {
                List<NetRule> list;
                if (byHost.TryGetValue(host, out list))
                    foreach (var rule in list)
                    {
                        if (rule.Path != null && !GlobPrefix(rule.Path, target)) continue;
                        if (rule.Exception) return new FilterMatch(false, true, rule.LineNumber);
                        if (block == null) block = rule;
                    }
                if (ip) break;
                int dot = host.IndexOf('.');
                if (dot < 0) break;
                host = host.Substring(dot + 1);
                if (host.IndexOf('.') < 0) break; // never look up a bare TLD
            }
            if (block == null) return default(FilterMatch);
            Interlocked.Increment(ref hits[block.Index]);
            return new FilterMatch(true, false, block.LineNumber);
        }

        public IList<RuleHit> TopHits(int count)
        {
            var all = new List<RuleHit>();
            for (int i = 0; i < hits.Length; i++)
            {
                int n = Volatile.Read(ref hits[i]);
                if (n > 0) all.Add(new RuleHit(lineByIndex[i], n));
            }
            all.Sort((a, b) => b.Count.CompareTo(a.Count));
            if (all.Count > count) all.RemoveRange(count, all.Count - count);
            return all;
        }

        // '*' matches any text, '^' one separator character or the end. The pattern must match from the
        // start of path+query; whatever follows the end of the pattern is irrelevant.
        public static bool GlobPrefix(string pattern, string text)
        {
            int p = 0, t = 0, star = -1, mark = 0;
            while (true)
            {
                if (p == pattern.Length) return true;
                char pc = pattern[p];
                if (pc == '*') { star = p++; mark = t; continue; }
                bool ok;
                if (pc == '^')
                {
                    if (t == text.Length) { p++; continue; }
                    char c = text[t];
                    ok = !((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-' || c == '.' || c == '%');
                }
                else ok = t < text.Length && Char.ToLowerInvariant(text[t]) == Char.ToLowerInvariant(pc);
                if (ok) { p++; t++; continue; }
                if (star < 0 || mark >= text.Length) return false;
                p = star + 1; t = ++mark;
            }
        }

        // ---- site comparison (heuristic: no public-suffix list; uncertain cases count as same site) ----

        internal static bool SameSite(string a, string b)
        {
            if (String.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return true;
            if (IsIpLiteral(a) || IsIpLiteral(b)) return false;
            return String.Equals(SiteKey(a), SiteKey(b), StringComparison.OrdinalIgnoreCase);
        }

        internal static string SiteKey(string host)
        {
            var labels = host.Split('.');
            int keep = labels.Length >= 3 && IsTwoPartSuffix(labels) ? 3 : 2;
            if (labels.Length <= keep) return host;
            return String.Join(".", labels, labels.Length - keep, keep);
        }

        // co.uk, com.pl, org.au ...: a 2-letter country code under a generic second-level label.
        private static bool IsTwoPartSuffix(string[] labels)
        {
            if (labels.Length < 2) return false;
            string tld = labels[labels.Length - 1], sld = labels[labels.Length - 2];
            if (tld.Length != 2) return false;
            switch (sld)
            {
                case "co": case "com": case "org": case "net": case "gov": case "edu": case "ac": case "or": case "ne": case "go": return true;
                default: return false;
            }
        }

        private static bool IsIpLiteral(string host)
        {
            if (host.IndexOf(':') >= 0) return true;
            foreach (char c in host) if (!((c >= '0' && c <= '9') || c == '.')) return false;
            return true;
        }

        // ---- cosmetic ----

        // CSS for the top-level page host (generic rules plus rules for the host and its parent domains),
        // or null. Selectors are validated at parse time and cannot break out of the JS/CSS string.
        public string BuildCosmeticCss(string pageUrl)
        {
            if (genericCosmetic.Count == 0 && hostCosmetic.Count == 0) return null;
            var selectors = new List<string>(genericCosmetic);
            Uri page;
            if (UrlSafety.TryHttpUri(pageUrl, out page))
            {
                string host = page.IdnHost;
                while (true)
                {
                    List<string> list;
                    if (hostCosmetic.TryGetValue(host, out list)) selectors.AddRange(list);
                    int dot = host.IndexOf('.');
                    if (dot < 0 || IsIpLiteral(host)) break;
                    host = host.Substring(dot + 1);
                    if (host.IndexOf('.') < 0) break;
                }
            }
            if (selectors.Count == 0) return null;
            var css = new StringBuilder();
            for (int i = 0; i < selectors.Count && i < MaxCosmeticSelectors; i++)
            {
                if (css.Length + selectors[i].Length + 2 > MaxCosmeticChars) break;
                if (css.Length != 0) css.Append(',');
                css.Append(selectors[i]);
            }
            css.Append("{display:none!important}");
            return css.ToString();
        }
    }
}
