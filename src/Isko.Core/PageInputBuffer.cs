namespace Isko.Core;

public enum PageInputKind { Click, Scroll }

public readonly struct PageInputAction
{
    public PageInputKind Kind { get; }
    public float X { get; }
    public float Y { get; }
    public float ScrollX { get; }
    public float ScrollY { get; }
    public PageInputAction(PageInputKind kind, float x, float y, float scrollX = 0, float scrollY = 0)
    { Kind = kind; X = x; Y = y; ScrollX = scrollX; ScrollY = scrollY; }
}

// UI-thread state. One native script may be running, with at most one pending click and
// one coalesced scroll. A slow page cannot build a queue of old wheel events.
public sealed class PageInputBuffer
{
    private PageInputAction? click, scroll;
    private bool running;
    public long Ticket { get; private set; }
    public bool HasWork => running || click.HasValue || scroll.HasValue;
    public void Reset() { Ticket++; running = false; click = scroll = null; }
    // Opening browser chrome cancels queued input, but cannot cancel a native call.
    public void DiscardPending() { click = scroll = null; }
    public void Click(float x, float y)
    {
        scroll = null; // Old wheel input must not move the page under this new click.
        click = new PageInputAction(PageInputKind.Click, x, y);
    }
    public void Scroll(float x, float y, float dx, float dy)
    {
        scroll = new PageInputAction(PageInputKind.Scroll, x, y,
            Clamp((scroll?.ScrollX ?? 0) + dx), Clamp((scroll?.ScrollY ?? 0) + dy));
    }
    private static float Clamp(float value) => System.Math.Max(-0.5f, System.Math.Min(0.5f, value));
    public bool TryTake(out PageInputAction action)
    {
        action = default;
        if (running) return false;
        if (click.HasValue) { action = click.Value; click = null; }
        else if (scroll.HasValue) { action = scroll.Value; scroll = null; }
        else return false;
        running = true; Ticket++;
        return true;
    }
    public bool Complete(long ticket)
    {
        if (ticket != Ticket || !running) return false;
        running = false; return true;
    }
}
