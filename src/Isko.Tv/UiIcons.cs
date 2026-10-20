namespace Isko.Tv;

// Original line icons on a 24px grid, written to the data directory on first use
// so NUI can load them as vector images.
internal static class UiIcons
{
    private const string Open = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"#fff\" stroke-width=\"STROKE\" stroke-linecap=\"round\" stroke-linejoin=\"round\">";
    private static readonly Dictionary<string, string> Shapes = new() {
        ["search"] = "<circle cx=\"10.5\" cy=\"10.5\" r=\"6.5\"/><path d=\"M15.5 15.5L20 20\"/>",
        ["home"] = "<path d=\"M4 10.5L12 4l8 6.5V20h-5.5v-5.5h-5V20H4z\"/>",
        ["star"] = "<path d=\"M12 3.8l2.5 5.1 5.6.8-4.1 4 1 5.6-5-2.7-5 2.7 1-5.6-4.1-4 5.6-.8z\"/>",
        // Favourite state is shown by colour (see Program.Gold), never by a heavier filled glyph.
        ["star-on"] = "<path d=\"M12 3.8l2.5 5.1 5.6.8-4.1 4 1 5.6-5-2.7-5 2.7 1-5.6-4.1-4 5.6-.8z\"/>",
        ["history"] = "<circle cx=\"12\" cy=\"12\" r=\"8.5\"/><path d=\"M12 7.5V12l3 2\"/>",
        ["info"] = "<circle cx=\"12\" cy=\"12\" r=\"8.5\"/><path d=\"M12 11v5.5M12 7.8v.2\"/>",
        ["back"] = "<path d=\"M14.5 5.5L8 12l6.5 6.5\"/>",
        ["go"] = "<path d=\"M5 12h13.5M13 6.5l5.5 5.5-5.5 5.5\"/>",
        ["reload"] = "<path d=\"M19.5 12a7.5 7.5 0 1 1-2.2-5.3\"/><path d=\"M19.5 4.5v4h-4\"/>",
        ["close"] = "<path d=\"M6.5 6.5l11 11M17.5 6.5l-11 11\"/>",
        ["more"] = "<circle fill=\"#fff\" stroke=\"none\" cx=\"5.5\" cy=\"12\" r=\"1.8\"/><circle fill=\"#fff\" stroke=\"none\" cx=\"12\" cy=\"12\" r=\"1.8\"/><circle fill=\"#fff\" stroke=\"none\" cx=\"18.5\" cy=\"12\" r=\"1.8\"/>",
        ["play"] = "<path d=\"M7.8 4.9v14.2L19.6 12z\"/>",
        ["lock"] = "<rect x=\"5.5\" y=\"10.5\" width=\"13\" height=\"9.5\" rx=\"2\"/><path d=\"M8.5 10.5V8a3.5 3.5 0 0 1 7 0v2.5\"/>",
        ["shield"] = "<path d=\"M12 3.5l7 2.8v5.2c0 4.3-3 7.6-7 9-4-1.4-7-4.7-7-9V6.3z\"/>",
        ["trash"] = "<path d=\"M4.5 7h15M9.5 7V4.5h5V7M6.5 7l1 13h9l1-13\"/>",
        ["grid"] = "<rect x=\"4\" y=\"4\" width=\"6.5\" height=\"6.5\" rx=\"1.5\"/><rect x=\"13.5\" y=\"4\" width=\"6.5\" height=\"6.5\" rx=\"1.5\"/><rect x=\"4\" y=\"13.5\" width=\"6.5\" height=\"6.5\" rx=\"1.5\"/><rect x=\"13.5\" y=\"13.5\" width=\"6.5\" height=\"6.5\" rx=\"1.5\"/>"
    };
    // Geometric "isko" in the mark's language: round i-dot, circular o, round caps, and the
    // logo's violet→blue gradient (lightened for legibility on dark). 136×64 units.
    private const string WordmarkSvg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 136 64\">" +
        "<defs><linearGradient id=\"g\" gradientUnits=\"userSpaceOnUse\" x1=\"0\" y1=\"0\" x2=\"136\" y2=\"0\">" +
        "<stop offset=\"0\" stop-color=\"#B46BFF\"/><stop offset=\"0.5\" stop-color=\"#7B7BFF\"/><stop offset=\"1\" stop-color=\"#5CC2FF\"/></linearGradient></defs>" +
        "<g fill=\"none\" stroke=\"url(#g)\" stroke-width=\"9\" stroke-linecap=\"round\" stroke-linejoin=\"round\">" +
        "<path d=\"M8 27V53.5\"/>" +
        "<path d=\"M48.5 28.5C46 24.5 41 22.5 36.5 22.5C30.5 22.5 25.5 25.5 25.5 30.5C25.5 36 31.5 37.8 37 39.2C43 40.7 49.5 42.8 49.5 49C49.5 54.6 43.8 57.5 37.6 57.5C31.8 57.5 27 55.4 24.5 51.6\"/>" +
        "<path d=\"M64 6.5V53.5\"/><path d=\"M85 25L66.5 41.5\"/><path d=\"M73 36L87 53.5\"/>" +
        "<circle cx=\"113\" cy=\"40\" r=\"15\"/></g>" +
        "<circle cx=\"8\" cy=\"10.5\" r=\"6\" fill=\"url(#g)\"/></svg>";
    public const float WordmarkAspect = 136f / 64f;
    // Page pointer: white arrow with a dark outline, legible on light and dark pages.
    // Its tip is at (3, 2) of the 24-unit grid.
    private const string CursorSvg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\">" +
        "<path d=\"M3 2v17.5l4.6-4.4 2.9 6.6 3-1.3-2.9-6.5H17z\" fill=\"#fff\" stroke=\"#0A0B10\" stroke-width=\"1.5\" stroke-linejoin=\"round\"/></svg>";
    public const float CursorTipX = 3f / 24, CursorTipY = 2f / 24;
    private static string directory = "";

    public static string Wordmark()
    {
        var file = Path.Combine(directory, "wordmark-1.svg");
        try { if (!File.Exists(file)) File.WriteAllText(file, WordmarkSvg); } catch { }
        return file;
    }

    public static string Cursor()
    {
        var file = Path.Combine(directory, "cursor-1.svg");
        try { if (!File.Exists(file)) File.WriteAllText(file, CursorSvg); } catch { }
        return file;
    }

    public static void Install(string dataDirectory)
    {
        directory = Path.Combine(dataDirectory, "ui-icons");
        try { Directory.CreateDirectory(directory); } catch { }
    }
    // Stroke is solved per rendered size so every icon draws the same ~2.25 px line,
    // whatever its box. Tint is applied by the caller through View.Color.
    public static string For(string name, Tizen.NUI.Color color, float size)
    {
        var rgb = $"{(int)(color.R * 255):x2}{(int)(color.G * 255):x2}{(int)(color.B * 255):x2}";
        var alpha = Math.Clamp(color.A, 0, 1);
        var stroke = Math.Clamp(2.25f * 24 / Math.Max(1, size), 1.2f, 2.6f);
        var invariant = System.Globalization.CultureInfo.InvariantCulture;
        var file = Path.Combine(directory, $"{name}-{rgb}-{(int)(alpha * 100)}-{(int)(stroke * 100)}.svg");
        try {
            if (!File.Exists(file)) {
                var shape = Shapes[name].Replace("#fff", "#" + rgb);
                var svg = Open.Replace("#fff", "#" + rgb).Replace("STROKE", stroke.ToString("0.##", invariant)) +
                    $"<g opacity=\"{alpha.ToString(invariant)}\">" + shape + "</g></svg>";
                File.WriteAllText(file, svg);
            }
        } catch { /* Missing icons degrade to empty space; labels still carry meaning. */ }
        return file;
    }
}
