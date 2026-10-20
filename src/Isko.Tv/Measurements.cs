using System.Diagnostics;
using System.Text.Json;

namespace Isko.Tv;

// Never receives a URL, page text, user agent, cookie or exception message.
internal sealed class Measurements : IDisposable
{
    private readonly Stopwatch elapsed = Stopwatch.StartNew();
    private readonly Timer timer;
    public long PopupsBlocked, NavigationsBlocked, LoadErrors;
    public long RequestBlockAttempts, RequestsBlocked, RequestCompletionErrors;
    public string RequestBlocking = "unsupported";
    public Measurements() => timer = new Timer(_ => Sample("interval"), null, 30000, 30000);
    public void Event(string name, long? value = null) => Console.WriteLine(JsonSerializer.Serialize(new
    { kind = name, elapsedMs = elapsed.ElapsedMilliseconds, value }));
    public void Sample(string reason)
    {
        long? workingSet = null;
        try { using var p = Process.GetCurrentProcess(); workingSet = p.WorkingSet64; } catch { }
        Console.WriteLine(JsonSerializer.Serialize(new {
            kind = "memory", reason, elapsedMs = elapsed.ElapsedMilliseconds,
            managedHeapBytes = GC.GetTotalMemory(false), processWorkingSetBytes = workingSet,
            // Web content may live in another process; this is NOT total browser RAM.
            scope = "application-process-only", jsHeapBytes = (long?)null,
            popupsBlocked = Interlocked.Read(ref PopupsBlocked), navigationsBlocked = Interlocked.Read(ref NavigationsBlocked),
            loadErrors = Interlocked.Read(ref LoadErrors), requestBlocking = RequestBlocking,
            requestBlockAttempts = Interlocked.Read(ref RequestBlockAttempts),
            requestsBlocked = Interlocked.Read(ref RequestsBlocked),
            requestCompletionErrors = Interlocked.Read(ref RequestCompletionErrors)
        }));
    }
    public void Media(string result)
    {
        // Web content is untrusted. Only finite, nonnegative numeric fields leave this method.
        if (result.Length > 4096) return;
        using var document = JsonDocument.Parse(result);
        var root = document.RootElement;
        var values = new Dictionary<string, double?>();
        foreach (var key in new[] { "errors", "waiting", "stalls", "droppedFrames", "totalFrames", "jsHeapBytes",
            "sweptLayers", "videoCount", "activeVideos", "readyState", "networkState", "mediaError", "paused", "currentTime",
            "bufferAhead", "videoWidth", "videoHeight", "boxWidth", "boxHeight" }) {
            double? n = null;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(key, out var value) &&
                value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var d) && double.IsFinite(d) && d >= 0) n = d;
            values[key] = n;
        }
        Console.WriteLine(JsonSerializer.Serialize(new { kind = "media", elapsedMs = elapsed.ElapsedMilliseconds,
            scope = "page-reported-top-document-only-untrusted", values }));
    }
    public void Dispose() => timer.Dispose();
}
