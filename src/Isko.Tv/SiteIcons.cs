using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Isko.Tv;

// Fetches a site's own touch icon/favicon for launcher tiles. Requested only from
// the home screen (never while a page plays), cookie-less, size- and time-limited.
// Files are named by a hash of the origin; no URL path or page content is stored.
internal sealed class SiteIcons : IDisposable
{
    private const int MaxHtml = 256 * 1024, MaxImage = 512 * 1024;
    private static readonly string[] Extensions = { ".png", ".jpg", ".webp", ".svg", ".ico" };
    private static readonly Regex LinkTag = new("<link\\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex Attribute = new("([a-z-]+)\\s*=\\s*(\"[^\"]*\"|'[^']*'|[^\\s>]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private readonly HttpClient http;
    private readonly string directory;
    private readonly HashSet<string> requested = new();
    private readonly ConcurrentQueue<string> ready = new();

    public SiteIcons(string dataDirectory)
    {
        directory = Path.Combine(dataDirectory, "site-icons");
        try { Directory.CreateDirectory(directory); } catch { }
        http = new HttpClient(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = true, MaxAutomaticRedirections = 4 }) {
            Timeout = TimeSpan.FromSeconds(8)
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (SMART-TV; Linux; Tizen 9.0) Isko/0.3");
    }

    public string? Find(string origin)
    {
        var stem = Path.Combine(directory, Name(origin));
        foreach (var extension in Extensions) if (File.Exists(stem + extension)) return stem + extension;
        return null;
    }
    // UI thread. One attempt per origin per run.
    public void Request(string origin)
    {
        if (Find(origin) != null || !requested.Add(origin)) return;
        _ = Task.Run(() => Fetch(origin));
    }
    // UI thread: origins whose icon arrived since the last call.
    public bool TryTakeReady(out string origin) => ready.TryDequeue(out origin!);
    public void Forget(IEnumerable<string> keep)
    {
        var names = new HashSet<string>(keep.Select(Name));
        try {
            foreach (var file in Directory.EnumerateFiles(directory))
                if (!names.Contains(Path.GetFileNameWithoutExtension(file))) File.Delete(file);
        } catch { }
    }

    private async Task Fetch(string origin)
    {
        try {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var root) || (root.Scheme != "https" && root.Scheme != "http")) return;
            var candidates = new List<(int Score, Uri Url)>();
            try { candidates.AddRange(await FromHtml(root)); } catch { }
            candidates.Add((5, new Uri(root, "/apple-touch-icon.png")));
            candidates.Add((1, new Uri(root, "/favicon.ico")));
            foreach (var candidate in candidates.OrderByDescending(c => c.Score).Select(c => c.Url).Distinct().Take(5))
                if (await TryDownload(origin, candidate)) { ready.Enqueue(origin); return; }
        } catch { /* Tiles keep their monogram. */ }
    }
    private async Task<IEnumerable<(int, Uri)>> FromHtml(Uri root)
    {
        using var response = await http.GetAsync(root, HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentType?.MediaType != "text/html") return Array.Empty<(int, Uri)>();
        var html = await ReadLimited(response, MaxHtml);
        var head = Encoding.UTF8.GetString(html);
        var found = new List<(int, Uri)>();
        var baseUri = response.RequestMessage?.RequestUri ?? root;
        foreach (Match tag in LinkTag.Matches(head)) {
            string rel = "", href = "", sizes = "", type = "";
            foreach (Match a in Attribute.Matches(tag.Value)) {
                var value = a.Groups[2].Value.Trim('"', '\'');
                switch (a.Groups[1].Value.ToLowerInvariant()) {
                    case "rel": rel = value.ToLowerInvariant(); break;
                    case "href": href = value; break;
                    case "sizes": sizes = value; break;
                    case "type": type = value; break;
                }
            }
            if (!rel.Contains("icon") || href.Length == 0 || href.StartsWith("data:", StringComparison.Ordinal)) continue;
            if (!Uri.TryCreate(baseUri, href, out var url) || (url.Scheme != "https" && url.Scheme != "http")) continue;
            var size = 0;
            foreach (var part in sizes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(part.Split('x', 'X')[0], out var n)) size = Math.Max(size, n);
            var score = rel.Contains("apple-touch-icon") ? 6 : type.Contains("svg") || url.AbsolutePath.EndsWith(".svg", StringComparison.Ordinal) ? 5
                : size >= 96 ? 4 : size >= 48 ? 3 : 2;
            found.Add((score, url));
        }
        return found;
    }
    private async Task<bool> TryDownload(string origin, Uri url)
    {
        try {
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            var type = response.Content.Headers.ContentType?.MediaType ?? "";
            if (!response.IsSuccessStatusCode || !type.StartsWith("image/", StringComparison.Ordinal)) return false;
            var extension = Extension(type, url);
            if (extension == null) return false;
            var bytes = await ReadLimited(response, MaxImage);
            if (bytes.Length < 64 || bytes.Length >= MaxImage) return false;
            var target = Path.Combine(directory, Name(origin) + extension);
            File.WriteAllBytes(target + ".tmp", bytes);
            File.Move(target + ".tmp", target, true);
            return true;
        } catch { return false; }
    }
    private static string? Extension(string type, Uri url) => type switch {
        "image/png" => ".png", "image/jpeg" => ".jpg", "image/webp" => ".webp", "image/svg+xml" => ".svg",
        "image/x-icon" or "image/vnd.microsoft.icon" => ".ico",
        _ => url.AbsolutePath.EndsWith(".ico", StringComparison.OrdinalIgnoreCase) ? ".ico" : null
    };
    private static async Task<byte[]> ReadLimited(HttpResponseMessage response, int limit)
    {
        if (response.Content.Headers.ContentLength > limit) return new byte[limit];
        using var stream = await response.Content.ReadAsStreamAsync();
        using var buffer = new MemoryStream();
        var chunk = new byte[16384];
        int read;
        while ((read = await stream.ReadAsync(chunk.AsMemory())) > 0) {
            buffer.Write(chunk, 0, read);
            if (buffer.Length >= limit) break;
        }
        return buffer.ToArray();
    }
    public void Dispose() => http.Dispose();
    private static string Name(string origin)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(origin))).Substring(0, 24).ToLowerInvariant();
    }
}
