using System;

namespace Isko.Core;

// Recognises the address of an adaptive-streaming manifest among a page's requests. A manifest URL usually carries
// a session token, so it is only held in memory and never logged; Describe gives the host for diagnostics.
public static class StreamUrls
{
    private const int MaxLength = 4096;

    // HLS playlists (.m3u8) and DASH manifests (.mpd), judged by the path alone. Media segments are not manifests.
    public static bool IsManifest(string? url)
    {
        if (url == null || url.Length == 0 || url.Length > MaxLength) return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http")) return false;
        var path = uri.AbsolutePath;
        return path.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".mpd", StringComparison.OrdinalIgnoreCase);
    }

    // Media segments of an adaptive stream. Once a page has asked for a manifest the native player takes over, and the
    // WebView cannot decode H.264 (feeding it segments can crash the app), so these requests are not needed.
    public static bool IsMediaSegment(string? url)
    {
        if (url == null || url.Length == 0 || url.Length > MaxLength) return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http")) return false;
        var path = uri.AbsolutePath;
        foreach (var ext in SegmentExtensions) if (path.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
    private static readonly string[] SegmentExtensions = { ".ts", ".m4s", ".m4v", ".m4a", ".aac", ".cmfv", ".cmfa", ".mp4" };

    public static string Describe(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : "[invalid]";
}
