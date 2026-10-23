using System;

namespace Isko.Protection
{
    public static class UrlSafety
    {
        internal static bool TryHttpUri(string input, out Uri uri)
        {
            uri = null;
            if (String.IsNullOrWhiteSpace(input) || input.IndexOf('\\') >= 0) return false;
            foreach (char c in input) if (Char.IsControl(c)) return false;
            return Uri.TryCreate(input, UriKind.Absolute, out uri) &&
                (uri.Scheme == "https" || uri.Scheme == "http") && uri.UserInfo.Length == 0 && uri.Host.Length != 0;
        }
        public static bool TryNormalizeAddress(string input, out string url)
        {
            url = null;
            if (String.IsNullOrWhiteSpace(input)) return false;
            input = input.Trim();
            if (!input.Contains("://")) input = "https://" + input;
            Uri uri;
            if (!TryHttpUri(input, out uri)) return false;
            url = uri.AbsoluteUri;
            return true;
        }
        // Persist origins only. Secrets may also occur in path segments, not only query strings.
        public static string ForPersistence(string url)
        {
            Uri uri;
            if (!TryHttpUri(url, out uri) || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.AbsolutePath != "/") return null;
            return uri.GetLeftPart(UriPartial.Authority) + "/";
        }
        public static string ForDiagnostics(string url)
        { Uri uri; return TryHttpUri(url, out uri) ? uri.GetLeftPart(UriPartial.Authority) : "[redacted-invalid-url]"; }
        internal static string NormalizeHost(string host)
        {
            if (String.IsNullOrWhiteSpace(host) || host.Contains("/") || host.Contains(":") || host.Contains("@") || host.Contains("?") || host.Contains("#"))
                throw new ArgumentException("Exact DNS host or IPv4 address required.", nameof(host));
            Uri uri;
            if (!TryHttpUri("https://" + host + "/", out uri) || uri.HostNameType == UriHostNameType.Unknown)
                throw new ArgumentException("Invalid host.", nameof(host));
            return uri.IdnHost;
        }
    }
}
