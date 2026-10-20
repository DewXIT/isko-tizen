using System;

namespace Isko.Core;

// Remote-driven pointer over a page, in the shell's logical 1920x1080 space. Held arrows
// accelerate; pushing against an edge asks the caller to scroll instead of moving. After a
// few idle seconds it hides, and the next press only reveals it where it was, so a stray key
// during playback never moves or clicks anything unseen.
public sealed class PageCursor
{
    public const float Width = 1920, Height = 1080;
    public const long IdleMs = 4000;
    private const long RepeatWindowMs = 400;
    private const float MinStep = 24, StepGrowth = 8, MaxStep = 96;
    private int lastDx, lastDy, repeats;
    private long lastMove = long.MinValue / 2, lastActivity = long.MinValue / 2;

    public float X { get; private set; } = Width / 2;
    public float Y { get; private set; } = Height / 2;

    public bool Visible(long now) => now - lastActivity < IdleMs;

    // A new page starts with the cursor shown in the middle of the screen.
    public void Reset(long now)
    {
        X = Width / 2; Y = Height / 2; repeats = 0; lastActivity = now;
        lastDx = lastDy = 0; lastMove = long.MinValue / 2;
    }

    public void Hide() => lastActivity = long.MinValue / 2;

    // OK press: true when the cursor was on screen and the press should click under it.
    public bool Activate(long now)
    {
        var shown = Visible(now);
        lastActivity = now;
        return shown;
    }

    // Returns wheel steps per axis (1..3, signed) when the cursor is already at that edge.
    public (int X, int Y) Move(int dx, int dy, long now)
    {
        repeats = dx == lastDx && dy == lastDy && now - lastMove < RepeatWindowMs ? repeats + 1 : 0;
        lastDx = dx; lastDy = dy; lastMove = now;
        var shown = Visible(now);
        lastActivity = now;
        if (!shown) return (0, 0);
        var step = Math.Min(MinStep + repeats * StepGrowth, MaxStep);
        var wheel = repeats >= 6 ? 3 : repeats >= 2 ? 2 : 1;
        var x = Math.Max(0, Math.Min(Width - 1, X + dx * step));
        var y = Math.Max(0, Math.Min(Height - 1, Y + dy * step));
        var scroll = (dx != 0 && x == X ? dx * wheel : 0, dy != 0 && y == Y ? dy * wheel : 0);
        X = x; Y = y;
        return scroll;
    }
}
