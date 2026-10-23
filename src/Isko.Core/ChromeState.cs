namespace Isko.Core;

// Home: launcher with site tiles. Page: the owned page always fills the screen.
public enum BrowserSurface { Home, Page }

// At most one overlay at a time. Controls is the floating bar summoned over a page.
public enum ChromeOverlay { None, Controls, Menu, Address }

// Shell state is independent of the engine. Opening an overlay never navigates a page.
public sealed class ChromeState
{
    public BrowserSurface Surface { get; private set; } = BrowserSurface.Home;
    public ChromeOverlay Overlay { get; private set; }
    public bool MenuOpen => Overlay == ChromeOverlay.Menu;
    // Remote keys belong to the page only while nothing covers it.
    public bool PageInteractive => Surface == BrowserSurface.Page && Overlay == ChromeOverlay.None;
    public void PageOpened() { Surface = BrowserSurface.Page; Overlay = ChromeOverlay.None; }
    public void PageClosed() { Surface = BrowserSurface.Home; Overlay = ChromeOverlay.None; }
    public bool Show(ChromeOverlay overlay)
    {
        if (overlay == ChromeOverlay.Controls && Surface != BrowserSurface.Page) return false;
        Overlay = overlay; return true;
    }
    // Back from the drawer or address entry returns to the bar over a page, never to a closed page.
    public bool Dismiss()
    {
        if (Overlay == ChromeOverlay.None) return false;
        Overlay = Overlay != ChromeOverlay.Controls && Surface == BrowserSurface.Page
            ? ChromeOverlay.Controls : ChromeOverlay.None;
        return true;
    }
    public bool CloseAll()
    {
        if (Overlay == ChromeOverlay.None) return false;
        Overlay = ChromeOverlay.None; return true;
    }
}

public readonly struct FocusRegion
{
    public float X { get; }
    public float Y { get; }
    public float Width { get; }
    public float Height { get; }
    public FocusRegion(float x, float y, float width, float height)
    { X = x; Y = y; Width = width; Height = height; }
}

public static class DirectionalFocus
{
    // Coordinates share the shell's logical 1920x1080 space. Hidden controls are
    // excluded by the caller; no wrap into a different row or a closed drawer.
    public static int Next(System.Collections.Generic.IReadOnlyList<FocusRegion> items, int current, int dx, int dy)
    {
        if (current < 0 || current >= items.Count) return items.Count == 0 ? -1 : 0;
        var origin = items[current];
        var best = current; var score = float.MaxValue;
        for (var i = 0; i < items.Count; i++) {
            if (i == current) continue;
            var item = items[i];
            var x = item.X + item.Width / 2 - origin.X - origin.Width / 2;
            var y = item.Y + item.Height / 2 - origin.Y - origin.Height / 2;
            var forward = x * dx + y * dy;
            if (forward <= 0) continue;
            var sideways = System.Math.Abs(dx != 0 ? y : x);
            // Prefer aligned controls; distance breaks ties within the same lane.
            var candidate = forward + sideways * 3;
            if (candidate < score) { score = candidate; best = i; }
        }
        return best;
    }
}
