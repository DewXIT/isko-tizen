using Tizen.Multimedia;

namespace Isko.Tv;

// Plays one adaptive-streaming address with the TV's native player (hardware decode) into the application window.
// UI thread only, except the two flags, which the player's own threads set. The address is never logged.
internal sealed class NativeVideo : IDisposable
{
    private Player? player;
    public volatile bool Ended;
    public volatile string? Failure;

    public bool IsOpen => player != null;
    public bool Playing => player?.State == PlayerState.Playing;
    public int PositionMs => player?.GetPlayPosition() ?? 0;
    public int DurationMs { get { try { return player?.StreamInfo.GetDuration() ?? 0; } catch { return 0; } } }
    // A live stream reports no usable duration; seeking is then offered only inside what the player accepts.
    public bool CanSeek => DurationMs > 0;

    public async Task OpenAsync(Tizen.NUI.Window window, string url, string? cookie, TimeSpan timeout)
    {
        var next = new Player();
        try {
            next.ErrorOccurred += (_, e) => Failure = e.Error.ToString();
            next.PlaybackCompleted += (_, _) => Ended = true;
            next.Display = new Display(window);
            if (!string.IsNullOrEmpty(cookie)) next.Cookie = cookie;
            next.SetSource(new MediaUriSource(url));
            using var cancel = new CancellationTokenSource(timeout);
            await next.PrepareAsync(cancel.Token);
            player = next;
        } catch { next.Dispose(); throw; }
    }

    public void Start() => player?.Start();
    public void Pause() => player?.Pause();
    public void TogglePause() { if (Playing) Pause(); else Start(); }
    public async Task SeekByAsync(int deltaMs)
    {
        if (player == null || !CanSeek) return;
        var target = Math.Clamp(PositionMs + deltaMs, 0, Math.Max(0, DurationMs - 1000));
        await player.SetPlayPositionAsync(target, false);
    }

    public void Dispose()
    {
        var current = player; player = null;
        if (current == null) return;
        try { if (current.State is PlayerState.Playing or PlayerState.Paused) current.Stop(); } catch { }
        try { current.Unprepare(); } catch { }
        try { current.Dispose(); } catch { }
    }
}
