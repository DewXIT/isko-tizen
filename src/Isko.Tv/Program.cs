using System.Diagnostics;
using System.Collections.Concurrent;
using Isko.Core;
using Isko.Protection;
using Tizen.Applications;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using Tizen.System;

namespace Isko.Tv;

// TV flow: launcher (hero + site tiles) → page on the whole screen → floating bar on demand.
// All shell views live in one logical 1920x1080 stage scaled to the window.
internal sealed class Program : NUIApplication
{
    private enum Layer { Backdrop, Home, Hero, Sites, Bar, BarItems, MenuFrame, MenuItems, Edit, Suggestions, Loading, Cursor, Toast }
    private enum Kind { Static, Glass, Primary, Field, Row, Tile }
    private sealed class Element
    {
        public View View = null!;
        public Layer Layer;
        public Kind Kind;
        public string Key = "";
        public FocusRegion Region;
        public Action? Action;
        public TextLabel? Label;
        public ImageView? Icon;
        public Color Normal = null!, Text = null!, IconTint = null!, Glow = null!;
        public bool Control, Visible;
        public Animation? Motion;
    }
    private readonly SingleViewOwner<WebView> page = new();
    private readonly ChromeState chrome = new();
    private readonly Measurements metrics = new();
    private readonly PageCursor cursor = new();
    private readonly PageInputBuffer pageInput = new();
    private readonly ConcurrentQueue<long> inputCompletions = new();
    private Tizen.NUI.Timer? inputTimer;
    private bool inputTimerRunning, inputUnavailable;
    private long inputDeadline;
    private readonly PageEngine engine = new ProtectedPageEngine();
    // Native playback: the WebView cannot decode H.264 on this TV, the platform player can. The page stays loaded but
    // hidden and paused; the stream address comes from the page's own manifest request and stays in memory only.
    private NativeVideo? native;
    private bool nativeActive, nativeStarting;
    private string? streamCandidate;
    private bool windowTransparent;
    // The last few native-playback steps, host and stage only (never the address), shown under Informacje.
    private readonly List<string> nativeNotes = new();
    private void NativeNote(string text)
    {
        nativeNotes.Add(DateTime.Now.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + " " + text);
        while (nativeNotes.Count > 6) nativeNotes.RemoveAt(0);
    }
    private readonly List<Element> elements = new();
    private readonly Stopwatch clock = Stopwatch.StartNew(), loading = new();
    private static readonly Color Background = Rgb(0x0A, 0x0B, 0x10);
    private static readonly Color Ink = Rgb(0x0A, 0x0B, 0x10);
    private static readonly Color Violet = Rgb(0x7B, 0x61, 0xFF);
    private static readonly Color Magenta = Rgb(0xFF, 0x4F, 0xA3);
    private static readonly Color Sky = Rgb(0x3D, 0x7B, 0xFF);
    private static readonly Color Gold = Rgb(0xFF, 0xC8, 0x4A);
    private static readonly Color Soft = new(1, 1, 1, 0.68f);
    private static readonly Color Faint = new(1, 1, 1, 0.45f);
    // Weights, not size alone, carry hierarchy; bold is never used for body or buttons.
    private const string Regular = "regular", Medium = "medium", Semi = "semiBold";
    private static readonly Color Glass = new(1, 1, 1, 0.07f);
    private static readonly Color GlassEdge = new(1, 1, 1, 0.11f);
    private static readonly Color Smoke = new(0.05f, 0.055f, 0.08f, 0.9f);
    // Tile hues are picked from the host name, not from a site's branding.
    private static readonly Color[] Tints = {
        Rgb(0x7B, 0x61, 0xFF), Rgb(0xFF, 0x4F, 0xA3), Rgb(0x22, 0xC5, 0xA6), Rgb(0xFF, 0x8A, 0x3D),
        Rgb(0x3D, 0x7B, 0xFF), Rgb(0xB9, 0x5C, 0xFF), Rgb(0xFF, 0x5A, 0x5A), Rgb(0x3D, 0xC9, 0x6B)
    };
    private static readonly string[] Days = { "niedziela", "poniedziałek", "wtorek", "środa", "czwartek", "piątek", "sobota" };
    private static readonly string[] Months = { "stycznia", "lutego", "marca", "kwietnia", "maja", "czerwca", "lipca",
        "sierpnia", "września", "października", "listopada", "grudnia" };
    private Tizen.NUI.Window window = null!;
    private View stage = null!, loadingBar = null!;
    private ImageView pointer = null!;
    private bool pointerShown;
    // One container per Layer, in enum order: a layer rebuilt later (Hero, Sites) still draws below the overlays.
    private readonly View[] layers = new View[Enum.GetValues<Layer>().Length];
    private TextField address = null!;
    private TextLabel status = null!, clockLabel = null!, dateLabel = null!, menuHeading = null!, editHint = null!;
    private View editError = null!;
    private SessionStore session = null!;
    private SiteIcons icons = null!;
    private Tizen.NUI.Timer? timer;
    private Animation? loadingMotion;
    private string currentUrl = "", platform = "unknown", menuSection = "main";
    private string? logoPath;
    private int savedPage;
    private long toastUntil;
    private bool paused, terminating, isLoading, homeStale, editErrorShown, backHintShown, menuNested, pageLoaded;

    public static void Main(string[] args) { using var app = new Program(); app.Run(args); }

    // Developer builds only (see tools/tv/package.cjs --start-url); empty in normal packages.
    private string? startUrl = ReadStartUrl();
    private static string? ReadStartUrl()
    {
        var value = typeof(Program).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
            .OfType<System.Reflection.AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "IskoStartUrl")?.Value;
        return !string.IsNullOrEmpty(value) && UrlSafety.TryNormalizeAddress(value, out var url) ? url : null;
    }

    protected override void OnCreate()
    {
        base.OnCreate();
        window = Tizen.NUI.Window.Default;
        window.BackgroundColor = Background;
        window.Resized += OnResized;
        window.KeyEvent += OnWindowKey;
        // The platform's rectangular focus frame ignores rounded shapes; Highlight draws focus instead.
        FocusManager.Instance.FocusIndicator = new View();
        stage = new View {
            Size = new Size(1920, 1080), ParentOrigin = ParentOrigin.TopLeft,
            PivotPoint = PivotPoint.TopLeft, PositionUsesPivotPoint = true
        };
        window.Add(stage);
        for (var i = 0; i < layers.Length; i++) {
            layers[i] = new View { Size = new Size(1920, 1080), ParentOrigin = ParentOrigin.TopLeft,
                PivotPoint = PivotPoint.TopLeft, PositionUsesPivotPoint = true };
            stage.Add(layers[i]);
        }
        var data = Application.Current.DirectoryInfo.Data;
        UiIcons.Install(data);
        session = new SessionStore(data);
        icons = new SiteIcons(data);
        try {
            var logo = System.IO.Path.Combine(Application.Current.DirectoryInfo.SharedResource, "isko.png");
            if (File.Exists(logo)) logoPath = logo;
        } catch { }
        try { if (Information.TryGetValue("http://tizen.org/feature/platform.version", out string value)) platform = value; } catch { }
        BuildBackdrop(); BuildHome(); BuildBarFrame(); BuildMenuFrame(); BuildEdit(); BuildLoading(); BuildCursor(); BuildToast();
        engine.StreamDetected = OnStreamDetected;
        UpdateHome();
        timer = new Tizen.NUI.Timer(500);
        timer.Tick += Tick; timer.Start();
        Render(); FocusHome();
        metrics.Event("nui-browser-shell-created");
    }

    // ---------- Home ----------
    private void BuildBackdrop()
    {
        Glow(Layer.Backdrop, 1500, 60, 1700, Violet, 0.42f);
        Glow(Layer.Backdrop, 160, 1040, 1500, Magenta, 0.24f);
        Glow(Layer.Backdrop, 1060, 760, 1100, Sky, 0.10f);
    }
    private void BuildHome()
    {
        // Mark and drawn wordmark share one vertical centre (y = 96).
        if (logoPath != null) Logo(Layer.Home, 200, 64, 64);
        var wordmark = new ImageView { ResourceUrl = UiIcons.Wordmark() };
        Add(wordmark, Layer.Home, logoPath != null ? 280 : 200, 68, MathF.Round(56 * UiIcons.WordmarkAspect), 56, Color.Transparent);
        Field(Layer.Home, "home-search", "Wpisz adres albo wyszukaj", "search", 560, 58, 800, 76, () => BeginEdit(""));
        clockLabel = Text(Layer.Home, "", 1480, 50, 400, 56, 40, Color.White, Medium, HorizontalAlignment.End);
        dateLabel = Text(Layer.Home, "", 1480, 104, 400, 34, 22, Soft, Regular, HorizontalAlignment.End);
        UpdateClock();
        var rail = Panel(Layer.Home, 40, 264, 104, 552, Glass, 52);
        rail.BorderlineWidth = 1.5f; rail.BorderlineColor = GlassEdge;
        Round(Layer.Home, "rail-home", "home", 56, 288, 72, FocusHome, Kind.Glass, new Color(Violet.R, Violet.G, Violet.B, 0.35f));
        Round(Layer.Home, "rail-search", "search", 56, 396, 72, () => BeginEdit(""));
        Round(Layer.Home, "rail-favorites", "star", 56, 504, 72, () => OpenMenu("favorites"));
        Round(Layer.Home, "rail-history", "history", 56, 612, 72, () => OpenMenu("history"));
        Round(Layer.Home, "rail-more", "more", 56, 720, 72, () => OpenMenu());
    }
    private void UpdateHome()
    {
        Clear(Layer.Hero); Clear(Layer.Sites);
        var onHome = chrome.Surface == BrowserSurface.Home;
        var last = session.LastOrigin;
        var origins = session.Favorites.Concat(session.History).Distinct().Where(o => o != last).ToList();
        if (last != null) {
            if (onHome) icons.Request(last);
            var name = DisplaySite(last, 30);
            var tint = TintFor(name);
            Glow(Layer.Hero, 1700, 392, 760, tint, 0.55f);
            var card = Gradient(Layer.Hero, 200, 196, 1680, 392, tint, 0.48f, 0.10f, 40);
            card.BorderlineWidth = 1.5f; card.BorderlineColor = GlassEdge;
            Text(card, "Ostatnio otwarte", 72, 58, 900, 34, 24, Soft, Medium);
            Text(card, name, 68, 92, 1180, 112, 88, Color.White, Semi);
            Text(card, session.IsFavorite(last) ? "W ulubionych · wróć jednym kliknięciem" : "Wróć jednym kliknięciem", 72, 204, 1100, 40, 26, Soft);
            // Icon-only, same size as every action button; violet alone marks the primary.
            Round(Layer.Hero, "hero-continue", "play", 272, 464, 80, () => Navigate(last), Kind.Primary, Violet);
            Round(Layer.Hero, "hero-favorite", session.IsFavorite(last) ? "star-on" : "star", 372, 464, 80, () => {
                session.ToggleFavorite(last); RefreshHome();
            });
            SiteBadge(Layer.Hero, last, name, tint, 1588, 282, 220);
        } else {
            Glow(Layer.Hero, 1600, 392, 700, Violet, 0.45f);
            var card = Gradient(Layer.Hero, 200, 196, 1680, 392, Violet, 0.42f, 0.08f, 40);
            card.BorderlineWidth = 1.5f; card.BorderlineColor = GlassEdge;
            Text(card, "Witaj w Isko", 72, 58, 900, 34, 24, Soft, Medium);
            Text(card, "Dokąd dziś?", 68, 92, 1180, 112, 88, Color.White, Semi);
            Text(card, "Wpisz adres albo wyszukaj transmisję. Strony zapiszą się tutaj.", 72, 204, 1200, 40, 26, Soft);
            Button(Layer.Hero, "hero-search", "Wpisz adres", "search", 272, 462, 320, 84, () => BeginEdit(""), Kind.Primary);
            if (logoPath != null) Logo(Layer.Hero, 1588, 282, 220);
            else {
                var plate = Panel(Layer.Hero, 1588, 282, 220, 220, Glass, 56);
                plate.BorderlineWidth = 1.5f; plate.BorderlineColor = GlassEdge;
                Icon(plate, "search", 60, 60, 100, Color.White);
            }
        }
        if (origins.Count == 0) return;
        Text(Layer.Sites, "Twoje strony", 200, 634, 900, 44, 28, Color.White, Semi);
        var shown = origins.Count > 6 ? origins.Take(5).ToList() : origins;
        for (var i = 0; i < shown.Count; i++) {
            if (onHome) icons.Request(shown[i]);
            Tile(shown[i], 200 + i * 284, 692);
        }
        if (origins.Count > 6) {
            var more = Panel(Layer.Sites, 200 + 5 * 284, 692, 260, 232, Glass, 32);
            more.BorderlineWidth = 1.5f; more.BorderlineColor = GlassEdge;
            Icon(more, "grid", 98, 52, 64, Color.White);
            Text(more, "Wszystkie", 16, 146, 228, 40, 24, Color.White, Medium, HorizontalAlignment.Center);
            Text(more, (origins.Count + 1) + " stron", 16, 182, 228, 30, 20, Faint, Regular, HorizontalAlignment.Center);
            MakeTile(Find(more)!, "tile-more", Violet, () => OpenMenu("history"));
        }
    }
    private void Tile(string origin, float x, float y)
    {
        var name = DisplaySite(origin, 18);
        var tint = TintFor(name);
        var tile = Panel(Layer.Sites, x, y, 260, 232, Glass, 32);
        tile.BorderlineColor = GlassEdge; tile.BorderlineWidth = 1.5f;
        SiteBadge(tile, origin, name, tint, 74, 34, 112);
        Text(tile, name, 16, 166, 228, 40, 24, Color.White, Medium, HorizontalAlignment.Center);
        if (session.Favorites.Contains(origin)) Icon(tile, "star-on", 216, 16, 28, Gold);
        MakeTile(Find(tile)!, "tile:" + origin, tint, () => Navigate(origin));
    }
    private void MakeTile(Element element, string key, Color tint, Action action)
    {
        element.Kind = Kind.Tile; element.Key = key; element.Glow = tint; element.Control = true; element.Action = action;
        Interactive(element);
    }
    // The site's own icon when one has been fetched, otherwise a tinted monogram.
    private void SiteBadge(Layer layer, string origin, string name, Color tint, float x, float y, float size)
    {
        var holder = Panel(layer, x, y, size, size, Color.Transparent, size / 4);
        holder.BoxShadow = new Shadow(size / 3, new Color(tint.R, tint.G, tint.B, 0.55f), new Vector2(0, size / 12), new Vector2(0, 0));
        SiteBadge(holder, origin, name, tint, 0, 0, size);
    }
    private void SiteBadge(View parent, string origin, string name, Color tint, float x, float y, float size)
    {
        var radius = size / 4;
        var path = icons.Find(origin);
        if (path != null) {
            var natural = 0;
            try { natural = ImageLoader.GetOriginalImageSize(path).Width; } catch { }
            if (natural == 0 || natural >= 96) {
                parent.Add(new ImageView { ResourceUrl = path, Position = new Position(x, y), Size = new Size(size, size),
                    CornerRadius = new Vector4(radius, radius, radius, radius), FittingMode = FittingModeType.ScaleToFill });
                return;
            }
            // Small favicons sit on a light plate instead of being blown up.
            var plate = new View { BackgroundColor = Color.White, Position = new Position(x, y), Size = new Size(size, size),
                CornerRadius = new Vector4(radius, radius, radius, radius) };
            plate.Add(new ImageView { ResourceUrl = path, Position = new Position(size * 0.25f, size * 0.25f), Size = new Size(size * 0.5f, size * 0.5f) });
            parent.Add(plate);
            return;
        }
        var monogram = new View { Position = new Position(x, y), Size = new Size(size, size),
            CornerRadius = new Vector4(radius, radius, radius, radius) };
        monogram.Background = LinearGradient(Lighten(tint, 0.25f), tint, 1, 1, -0.5f, -0.5f, 0.5f, 0.5f);
        var letter = Label(name.Substring(0, 1).ToUpperInvariant(), size * 0.46f, Color.White, Semi, HorizontalAlignment.Center, false);
        letter.Size = new Size(size, size);
        monogram.Add(letter);
        parent.Add(monogram);
    }
    private void RefreshHome()
    {
        var key = elements.FirstOrDefault(e => e.View.HasFocus())?.Key;
        UpdateHome(); Render();
        var target = elements.FirstOrDefault(e => e.Control && e.Key == key && e.View.Focusable);
        if (target != null) FocusManager.Instance.SetCurrentFocusView(target.View); else FocusHome();
        homeStale = false;
    }
    private static string DisplaySite(string url, int max = 22)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return "Strona";
        var host = uri.IdnHost.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.IdnHost.Substring(4) : uri.IdnHost;
        return host.Length > max ? string.Concat(host.AsSpan(0, max - 1), "…") : host;
    }
    private static Color TintFor(string name)
    {
        var hash = 2166136261u; // FNV-1a: string.GetHashCode is randomized per process.
        foreach (var c in name) hash = (hash ^ c) * 16777619u;
        return Tints[hash % (uint)Tints.Length];
    }
    private void UpdateClock()
    {
        var now = DateTime.Now;
        var time = now.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        if (clockLabel.Text != time) {
            clockLabel.Text = time;
            dateLabel.Text = Days[(int)now.DayOfWeek] + ", " + now.Day + " " + Months[now.Month - 1];
        }
    }

    // ---------- Page bar: floating controls over a full-screen page ----------
    private void BuildBarFrame()
    {
        var bar = Panel(Layer.Bar, 64, 40, 1792, 120, Smoke, 60);
        bar.BorderlineWidth = 1.5f; bar.BorderlineColor = GlassEdge;
        bar.BoxShadow = new Shadow(56, new Color(0, 0, 0, 0.6f), new Vector2(0, 18), new Vector2(0, 0));
    }
    private void ShowControls(bool focusClose = false)
    {
        if (page.Current == null || !chrome.Show(ChromeOverlay.Controls)) return;
        Clear(Layer.BarItems);
        // One control height (80) centred in the 120px bar; the address field takes the rest.
        var x = 84f;
        // Without history there is nowhere to go back to: the same slot closes the page instead of flickering.
        var canBack = engine.CanGoBack;
        var left = canBack ? Round(Layer.BarItems, "bar-back", "back", x, 60, 80, () => { HideOverlay(); engine.Back(); })
            : Round(Layer.BarItems, "bar-close", "close", x, 60, 80, () => ClosePage());
        x += 96;
        Round(Layer.BarItems, "bar-reload", "reload", x, 60, 80, Reload); x += 96;
        var fieldWidth = 1836 - x - (canBack ? 16 + 80 + 16 + 80 + 16 + 80 : 16 + 80 + 16 + 80);
        var field = Field(Layer.BarItems, "bar-address", DisplaySite(currentUrl, 60), currentUrl.StartsWith("https:", StringComparison.Ordinal) ? "lock" : "search",
            x, 60, fieldWidth, 80, () => BeginEdit(currentUrl), Color.White);
        x += fieldWidth + 16;
        Round(Layer.BarItems, "bar-favorite", session.IsFavorite(currentUrl) ? "star-on" : "star", x, 60, 80, ToggleFavorite); x += 96;
        var close = canBack ? Round(Layer.BarItems, "bar-close", "close", x, 60, 80, () => ClosePage()) : left;
        if (canBack) x += 96;
        Round(Layer.BarItems, "bar-more", "more", x, 60, 80, () => OpenMenu());
        Render();
        FocusManager.Instance.SetCurrentFocusView(focusClose ? close : field);
    }
    private void HideOverlay()
    {
        chrome.CloseAll(); Render();
        if (page.Current != null) FocusManager.Instance.SetCurrentFocusView(page.Current); else FocusHome();
    }
    private void Reload()
    {
        HideOverlay();
        if (page.Current != null && currentUrl.Length != 0) { SetStatus("Odświeżanie…"); page.Current.LoadUrl(currentUrl); }
    }
    private void ToggleFavorite()
    {
        var added = session.ToggleFavorite(currentUrl); UpdateHome(); ShowControls();
        SetStatus(added ? "Dodano do ulubionych" : "Usunięto z ulubionych");
    }

    // ---------- Drawer: lists, protection and information ----------
    private void BuildMenuFrame()
    {
        Panel(Layer.MenuFrame, 0, 0, 1920, 1080, new Color(0, 0, 0, 0.55f));
        var panel = Panel(Layer.MenuFrame, 1280, 0, 640, 1080, new Color(0.06f, 0.065f, 0.09f, 0.97f));
        panel.BorderlineWidth = 1.5f; panel.BorderlineColor = GlassEdge;
        Glow(Layer.MenuFrame, 1900, 0, 900, Violet, 0.22f);
        menuHeading = Text(Layer.MenuFrame, "Więcej", 1336, 72, 528, 64, 40, Color.White, Semi);
    }
    private void OpenMenu(string section = "main", bool reset = true)
    {
        // Back climbs to "Więcej" only from a list entered through it; a list opened from the
        // rail, a tile or the bar closes straight back to where it came from.
        if (chrome.Overlay != ChromeOverlay.Menu || section == "main") menuNested = false;
        else if (menuSection == "main") menuNested = true;
        chrome.Show(ChromeOverlay.Menu); menuSection = section;
        if (reset) savedPage = 0;
        Clear(Layer.MenuItems);
        var y = 196f;
        void Item(string text, string icon, Action action) { Row(Layer.MenuItems, "menu:" + text, text, icon, 1336, y, 528, 88, action); y += 104; }
        void Note(string text, float height) { Text(Layer.MenuItems, text, 1336, y, 528, height, 24, Soft, Regular, HorizontalAlignment.Begin, true); y += height + 24; }
        if (section == "main") {
            menuHeading.Text = "Więcej";
            Item("Ulubione", "star", () => OpenMenu("favorites"));
            Item("Historia", "history", () => OpenMenu("history"));
            Item("Ochrona witryny", "shield", () => OpenMenu("protection"));
            if (page.Current != null && streamCandidate != null) Item("Odtwórz wykrytą transmisję", "info", () => { HideOverlay(); StartNative(streamCandidate); });
            if (page.Current != null) Item("Zmień tryb obrazu wideo", "info", () => { HideOverlay(); ToggleVideoPath(); });
            if (session.History.Count != 0) Item("Wyczyść historię", "trash", () => {
                session.ClearHistory(); icons.Forget(session.Favorites); UpdateHome(); OpenMenu(); SetStatus("Historia wyczyszczona");
            });
            Item("Informacje", "info", () => OpenMenu("about"));
        } else if (section == "favorites" || section == "history") {
            var saved = section == "favorites" ? session.Favorites : session.History;
            menuHeading.Text = section == "favorites" ? "Ulubione" : "Historia";
            savedPage = Math.Clamp(savedPage, 0, Math.Max(0, (saved.Count - 1) / 7));
            if (saved.Count == 0) Note(section == "favorites" ? "Dodaj stronę gwiazdką na pasku strony." : "Tu pojawią się odwiedzone strony.", 70);
            foreach (var origin in saved.Skip(savedPage * 7).Take(7)) {
                var destination = origin; Item(DisplaySite(origin, 26), section == "favorites" ? "star-on" : "history", () => Navigate(destination));
            }
            if (saved.Count > 7) {
                if (savedPage > 0) Button(Layer.MenuItems, "menu-prev", "Poprzednie", "back", 1336, 940, 256, 80,
                    () => { savedPage--; OpenMenu(section, false); FocusMenuKey("menu-prev", "menu-next"); }, Kind.Glass);
                if ((savedPage + 1) * 7 < saved.Count) Button(Layer.MenuItems, "menu-next", "Następne", "", 1608, 940, 256, 80,
                    () => { savedPage++; OpenMenu(section, false); FocusMenuKey("menu-next", "menu-prev"); }, Kind.Glass);
            }
        } else if (section == "protection") {
            menuHeading.Text = "Ochrona";
            Note(engine.Status, 120);
            Item("Włącz / wyłącz ochronę", "shield", () => { engine.ToggleProtection(); OpenMenu("protection"); });
            if (page.Current != null)
                Item("Wyjątek dla tej strony", "star", () => { engine.ToggleException(); OpenMenu("protection"); });
        } else {
            menuHeading.Text = "Informacje";
            Note("Isko · Tizen " + platform + "\n\n" + engine.Status + (nativeNotes.Count == 0 ? "" : "\n\nOdtwarzanie natywne:\n" + string.Join("\n", nativeNotes)), 240 + 32 * nativeNotes.Count);
            Item("Test blokady żądań", "shield", engine.ToggleFixture);
        }
        Render();
        var first = elements.FirstOrDefault(e => e.Layer == Layer.MenuItems && e.Control);
        if (first != null) FocusManager.Instance.SetCurrentFocusView(first.View);
    }

    // After paging, stay on the pager so repeated presses keep turning pages; fall back when it vanished.
    private void FocusMenuKey(params string[] keys)
    {
        foreach (var key in keys) {
            var target = elements.FirstOrDefault(e => e.Layer == Layer.MenuItems && e.Key == key && e.Visible);
            if (target != null) { FocusManager.Instance.SetCurrentFocusView(target.View); return; }
        }
    }

    // ---------- Address entry with suggestions ----------
    private void BuildEdit()
    {
        Panel(Layer.Edit, 0, 0, 1920, 1080, new Color(0.03f, 0.035f, 0.05f, 0.95f));
        Glow(Layer.Edit, 960, -100, 1600, Violet, 0.30f);
        var pill = Panel(Layer.Edit, 200, 140, 1520, 104, Color.White, 52);
        pill.BoxShadow = new Shadow(60, new Color(Violet.R, Violet.G, Violet.B, 0.45f), new Vector2(0, 16), new Vector2(0, 0));
        Icon(pill, "search", 36, 32, 40, Ink);
        address = new TextField {
            PlaceholderText = "Adres, np. twitch.tv — albo czego szukasz", PixelSize = 36,
            TextColor = Ink, PlaceholderTextColor = new Vector4(0.42f, 0.45f, 0.5f, 1), BackgroundColor = Color.Transparent,
            EnableMarkup = false, VerticalAlignment = VerticalAlignment.Center, Position = new Position(100, 0), Size = new Size(1380, 104)
        };
        var input = new InputMethod {
            PanelLayout = InputMethod.PanelLayoutType.URL, AutoCapital = InputMethod.AutoCapitalType.None,
            ActionButton = InputMethod.ActionButtonTitleType.Go
        };
        address.InputMethodSettings = input.OutputMap;
        address.GetInputMethodContext().TextPrediction = false;
        address.KeyEvent += HandleKey;
        address.TextChanged += (_, _) => {
            if (chrome.Overlay != ChromeOverlay.Address) return;
            // Some on-screen keyboards commit their Go key as a line break instead of a key event.
            var text = address.Text;
            if (text.IndexOfAny(LineBreaks) >= 0) {
                // Not inside the field's own change event: editing the text or tearing the overlay down there
                // re-enters the native text control.
                text = text.Replace("\r", "").Replace("\n", "");
                Defer(() => { if (chrome.Overlay == ChromeOverlay.Address) { address.Text = text; Submit(text); } }); return;
            }
            editErrorShown = false; UpdateSuggestions();
        };
        pill.Add(address);
        // Shown only for an invalid address; the keys need no legend.
        editError = Panel(Layer.Edit, 228, 264, 760, 52, new Color(1, 0.33f, 0.4f, 0.16f), 26);
        Icon(editError, "info", 18, 12, 28, Rgb(0xFF, 0x8A, 0x95));
        editHint = Text(editError, "", 58, 0, 684, 52, 23, Rgb(0xFF, 0xB3, 0xBA), Medium);
    }
    private void BeginEdit(string text)
    {
        chrome.Show(ChromeOverlay.Address);
        address.Text = text; editErrorShown = false;
        UpdateSuggestions();
        Render(); FocusManager.Instance.SetCurrentFocusView(address);
    }
    private void UpdateSuggestions()
    {
        Clear(Layer.Suggestions);
        var text = address.Text.Trim();
        // Close under the field; the invalid-address chip needs the room between them only while it shows.
        var y = editErrorShown ? 336f : 276f;
        if (text.Length != 0) {
            var typed = text;
            Row(Layer.Suggestions, "suggest-go", LooksLikeAddress(text) ? "Przejdź do " + text : "Szukaj „" + text + "”",
                LooksLikeAddress(text) ? "go" : "search", 200, y, 1520, 88, () => Defer(() => Submit(typed)));
            y += 100;
        }
        foreach (var origin in session.Favorites.Concat(session.History).Distinct()
            .Where(o => text.Length == 0 || o.Contains(text, StringComparison.OrdinalIgnoreCase)).Take(text.Length == 0 ? 6 : 5)) {
            var destination = origin;
            Row(Layer.Suggestions, "suggest:" + origin, DisplaySite(origin, 60), session.Favorites.Contains(origin) ? "star-on" : "history",
                200, y, 1520, 88, () => Navigate(destination));
            y += 100;
        }
        Render();
    }
    // Runs after the current native event returns, so a handler never disposes the view that is calling it.
    private readonly HashSet<Tizen.NUI.Timer> deferred = new();
    private void Defer(Action action)
    {
        var once = new Tizen.NUI.Timer(30);
        deferred.Add(once); // rooted until it fires: nothing else references a one-shot timer
        once.Tick += (_, _) => {
            once.Stop(); deferred.Remove(once); once.Dispose();
            if (!terminating) { try { action(); } catch { metrics.Event("deferred-action-failed"); } }
            return false;
        };
        once.Start();
    }
    private static readonly char[] LineBreaks = { '\r', '\n' };
    // The keyboard's Go/Enter does not always reach the field's own handler; the window sees it either way.
    // Submit leaves the address overlay, so a key seen by both handlers is acted on once.
    private void OnWindowKey(object? sender, Tizen.NUI.Window.KeyEventArgs args)
    {
        if (nativeActive) { if (args.Key.State == Key.StateType.Down) NativeKey(args.Key.KeyPressedName); return; }
        if (chrome.Overlay != ChromeOverlay.Address || args.Key.State != Key.StateType.Down) return;
        if (args.Key.KeyPressedName is "Return" or "KP_Enter" && address.HasFocus()) Submit(address.Text);
    }
    private static bool LooksLikeAddress(string text) =>
        text.Contains("://") || (!text.Contains(' ') && (text.Contains('.') || text.StartsWith("localhost", StringComparison.Ordinal)));
    private void Submit(string input)
    {
        var text = input.Trim();
        if (text.Length == 0) return;
        // A phrase without a dot is a search, like in any modern browser.
        Navigate(LooksLikeAddress(text) ? text : "https://duckduckgo.com/?q=" + Uri.EscapeDataString(text));
    }

    // ---------- Loading line and toast ----------
    private void BuildLoading()
    {
        var track = Panel(Layer.Loading, 0, 0, 1920, 5, new Color(1, 1, 1, 0.06f));
        loadingBar = new View { Size = new Size(560, 5), Position = new Position(-560, 0), CornerRadius = new Vector4(3, 3, 3, 3) };
        loadingBar.Background = LinearGradient(Violet, Magenta, 1, 1, -0.5f, 0, 0.5f, 0);
        track.Add(loadingBar);
    }
    // ---------- Page cursor: the remote's arrows point, OK clicks ----------
    private const float PointerSize = 48;
    private void BuildCursor()
    {
        pointer = new ImageView { ResourceUrl = UiIcons.Cursor(), Size = new Size(PointerSize, PointerSize) };
        layers[(int)Layer.Cursor].Add(pointer);
        pointer.Hide();
    }
    private void UpdatePointer()
    {
        if (!nativeActive && chrome.PageInteractive && cursor.Visible(clock.ElapsedMilliseconds)) {
            pointer.Position = new Position(cursor.X - PointerSize * UiIcons.CursorTipX, cursor.Y - PointerSize * UiIcons.CursorTipY);
            pointer.Show(); pointerShown = true;
        } else { pointer.Hide(); pointerShown = false; }
    }
    // Arrows move the cursor, OK clicks under it; every other key stays with the page.
    private bool PageKey(string key)
    {
        if (!chrome.PageInteractive || page.Current == null) return false;
        var now = clock.ElapsedMilliseconds;
        var fx = cursor.X / PageCursor.Width; var fy = cursor.Y / PageCursor.Height;
        if (key is "Return" or "KP_Enter") {
            if (cursor.Activate(now)) { engine.UserInput(); pageInput.Click(fx, fy); DispatchPageInput(); metrics.Event("cursor-click"); }
            UpdatePointer(); return true;
        }
        var dx = key == "Left" ? -1 : key == "Right" ? 1 : 0;
        var dy = key == "Up" ? -1 : key == "Down" ? 1 : 0;
        if (dx == 0 && dy == 0) return false;
        // Against an edge the cursor scrolls instead, a twelfth of the screen per step.
        var scroll = cursor.Move(dx, dy, now);
        if (scroll != (0, 0))
            { pageInput.Scroll(cursor.X / PageCursor.Width, cursor.Y / PageCursor.Height,
                scroll.X / 12f, scroll.Y / 12f); DispatchPageInput(); }
        UpdatePointer(); return true;
    }

    // ---------- Native playback ----------
    private void OnStreamDetected(string url, bool afterUserPress)
    {
        if (nativeActive || nativeStarting || paused || terminating) return;
        streamCandidate = url;
        NativeNote("wykryto " + Isko.Core.StreamUrls.Describe(url) + (afterUserPress ? " po OK" : " bez OK"));
        metrics.Event("stream-detected", afterUserPress ? 1 : 0);
        // A manifest requested right after the user pressed OK is the stream they chose; a background one only
        // offers itself (previews and thumbnails use manifests too).
        if (afterUserPress) StartNative(url);
        else SetStatus("Wykryto transmisję · Menu → Odtwórz wykrytą transmisję");
    }

    private async void StartNative(string url)
    {
        var view = page.Current;
        if (nativeActive || nativeStarting || view == null || terminating) return;
        nativeStarting = true;
        try {
            SetStatus("Uruchamiam odtwarzacz…");
            string? cookie = null;
            var cookieTask = new TaskCompletionSource<string?>();
            try { view.EvaluateJavaScript("document.cookie", r => cookieTask.TrySetResult(r)); } catch { cookieTask.TrySetResult(null); }
            if (await Task.WhenAny(cookieTask.Task, Task.Delay(1500)) == cookieTask.Task) cookie = cookieTask.Task.Result?.Trim('"');
            try { view.EvaluateJavaScript(PageScripts.PauseMedia); } catch { }
            var video = new NativeVideo();
            // The window goes transparent before the player is prepared (the order that works on the TV) while the
            // page stays drawn on top of it; the page is hidden only once the player is ready, so no empty frame shows.
            window.SetTransparency(true); window.BackgroundColor = Color.Transparent; windowTransparent = true;
            NativeNote("przygotowanie " + Isko.Core.StreamUrls.Describe(url));
            try { await video.OpenAsync(window, url, cookie, TimeSpan.FromSeconds(20)); }
            catch (Exception e) { NativeNote("przygotowanie nieudane: " + e.GetType().Name); video.Dispose(); throw; }
            try {
                nativeActive = true; view.Hide(); view.Focusable = false; cursor.Hide(); Render();
                video.Start();
            } catch (Exception e) { NativeNote("start nieudany: " + e.GetType().Name); video.Dispose(); throw; }
            native = video;
            NativeNote("gra");
            metrics.Event("native-playback-started");
            NativeToast();
        } catch (Exception e) {
            metrics.Event("native-playback-failed");
            // The reason goes on screen: the remote has no Menu key to open the details under Informacje.
            var last = nativeNotes.Count == 0 ? e.GetType().Name : nativeNotes[nativeNotes.Count - 1];
            StopNative("Natywne odtwarzanie nie ruszyło · " + (last.Length > 9 && last[2] == ':' ? last.Substring(9) : last));
        } finally { nativeStarting = false; }
    }

    private void StopNative(string? message = null)
    {
        var view = page.Current;
        try { native?.Dispose(); } catch { }
        native = null;
        if (windowTransparent) { windowTransparent = false; try { window.BackgroundColor = Background; window.SetTransparency(false); } catch { } }
        if (nativeActive) {
            nativeActive = false;
            if (view != null) { view.Show(); view.Focusable = true; FocusManager.Instance.SetCurrentFocusView(view); }
            chrome.CloseAll(); Render();
        }
        if (message != null) { SetStatus(message); NativeNote(message); }
        metrics.Sample("after-native");
    }

    private void NativeToast()
    {
        var video = native;
        if (video == null) return;
        string Clock(int ms) { var t = TimeSpan.FromMilliseconds(Math.Max(0, ms)); return t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss", System.Globalization.CultureInfo.InvariantCulture) : t.ToString(@"m\:ss", System.Globalization.CultureInfo.InvariantCulture); }
        var position = Clock(video.PositionMs);
        var extent = video.CanSeek ? " / " + Clock(video.DurationMs) : " · na żywo";
        SetStatus((video.Playing ? "▶ " : "⏸ ") + position + extent + "   OK pauza · ← → ±10 s · Wstecz: strona");
    }

    private async void NativeKey(string key)
    {
        var video = native;
        if (key is "XF86Back" or "Escape" or "BackSpace" or "XF86Exit" or "XF86AudioStop") { StopNative("Wróciłeś do strony"); return; }
        if (video == null) return;
        try {
            if (key is "Return" or "KP_Enter" or "XF86AudioPlay" or "XF86AudioPause" or "space") video.TogglePause();
            else if (key is "Left" or "XF86AudioRewind") await video.SeekByAsync(-10000);
            else if (key is "Right" or "XF86AudioForward") await video.SeekByAsync(10000);
            NativeToast();
        } catch { metrics.Event("native-key-failed"); }
    }

    private void ToggleVideoPath()
    {
        // A video path must only be changed on an already loaded view. Some TV engines
        // stop loading entirely when this property is set immediately after construction.
        var target = page.Current;
        if (target == null || !pageLoaded) { SetStatus("Poczekaj na załadowanie strony"); return; }
        try {
            var overlay = !target.VideoHoleEnabled;
            target.VideoHoleEnabled = overlay;
            metrics.Event("video-overlay-mode", overlay ? 1 : 0);
            SetStatus(overlay ? "Obraz wideo: warstwa sprzętowa" : "Obraz wideo: wewnątrz strony");
        } catch { metrics.Event("video-path-unavailable"); SetStatus("Zmiana trybu obrazu jest niedostępna"); }
    }

    private void ResetPageInput()
    {
        pageInput.Reset(); inputDeadline = 0; inputUnavailable = false;
        inputTimer?.Stop(); inputTimerRunning = false;
        while (inputCompletions.TryDequeue(out _)) { }
    }
    private void DispatchPageInput()
    {
        if (inputUnavailable) { pageInput.Reset(); return; }
        var target = page.Current;
        if (target == null) { ResetPageInput(); return; }
        if (!chrome.PageInteractive) { pageInput.DiscardPending(); return; }
        if (pageInput.TryTake(out var action)) {
            var script = action.Kind == PageInputKind.Click ? PageScripts.ClickAt(action.X, action.Y)
                : PageScripts.ScrollAt(action.X, action.Y, action.ScrollX, action.ScrollY);
            var ticket = pageInput.Ticket;
            inputDeadline = clock.ElapsedMilliseconds + 2000;
            try { target.EvaluateJavaScript(script + ";true", _ => inputCompletions.Enqueue(ticket)); }
            catch { pageInput.Reset(); inputUnavailable = true; inputDeadline = 0; metrics.Event("page-input-unavailable"); }
        }
        if (pageInput.HasWork && !inputTimerRunning) {
            if (inputTimer == null) { inputTimer = new Tizen.NUI.Timer(16); inputTimer.Tick += FlushPageInput; }
            inputTimerRunning = true; inputTimer.Start();
        }
    }
    private bool FlushPageInput(object sender, Tizen.NUI.Timer.TickEventArgs args)
    {
        while (inputCompletions.TryDequeue(out var ticket))
            if (pageInput.Complete(ticket)) inputDeadline = 0;
        if (inputDeadline != 0 && clock.ElapsedMilliseconds >= inputDeadline && pageInput.HasWork) {
            // A busy page may answer later. Keep its one native call in flight so a
            // timeout neither grows the queue nor permanently disables future clicks.
            inputDeadline = 0;
            metrics.Event("page-input-timeout"); SetStatus("Strona jest zajęta. Poczekaj lub odśwież ją.");
        }
        DispatchPageInput();
        if (pageInput.HasWork) return true;
        inputDeadline = 0; inputTimerRunning = false; return false;
    }

    private void BuildToast()
    {
        var toast = Panel(Layer.Toast, 560, 944, 800, 84, Smoke, 42);
        toast.BorderlineWidth = 1.5f; toast.BorderlineColor = GlassEdge;
        toast.BoxShadow = new Shadow(40, new Color(0, 0, 0, 0.5f), new Vector2(0, 12), new Vector2(0, 0));
        toast.Add(new View { BackgroundColor = Violet, Position = new Position(36, 36), Size = new Size(12, 12), CornerRadius = new Vector4(6, 6, 6, 6) });
        status = Text(toast, "", 64, 0, 672, 84, 26, Color.White, Medium, HorizontalAlignment.Center);
    }
    private void SetLoading(bool value)
    {
        if (isLoading == value) return;
        isLoading = value;
        loadingMotion?.Stop(); loadingMotion?.Dispose(); loadingMotion = null;
        if (value) {
            loadingBar.PositionX = -560;
            loadingMotion = new Animation(1400) { Looping = true };
            loadingMotion.AnimateTo(loadingBar, "PositionX", 1920f, new AlphaFunction(AlphaFunction.BuiltinFunctions.EaseInOut));
            loadingMotion.Play();
        }
        Render();
    }

    // ---------- View factory ----------
    private static Color Rgb(int r, int g, int b) => new(r / 255f, g / 255f, b / 255f, 1);
    private static Color Lighten(Color c, float amount) =>
        new(c.R + (1 - c.R) * amount, c.G + (1 - c.G) * amount, c.B + (1 - c.B) * amount, c.A);
    private static PropertyMap LinearGradient(Color from, Color to, float fromAlpha, float toAlpha, float x1, float y1, float x2, float y2) =>
        new GradientVisual {
            StartPosition = new Vector2(x1, y1), EndPosition = new Vector2(x2, y2),
            StopOffset = new PropertyArray().Add(new PropertyValue(0f)).Add(new PropertyValue(1f)),
            StopColor = new PropertyArray().Add(new PropertyValue(new Vector4(from.R, from.G, from.B, fromAlpha)))
                .Add(new PropertyValue(new Vector4(to.R, to.G, to.B, toAlpha)))
        }.OutputVisualMap;
    // Soft light: a circle whose radial gradient fades to transparent. Centre coordinates.
    private void Glow(Layer layer, float cx, float cy, float diameter, Color color, float alpha)
    {
        var view = new View {
            Background = new GradientVisual {
                Center = new Vector2(0, 0), Radius = 0.5f,
                StopOffset = new PropertyArray().Add(new PropertyValue(0f)).Add(new PropertyValue(1f)),
                StopColor = new PropertyArray().Add(new PropertyValue(new Vector4(color.R, color.G, color.B, alpha)))
                    .Add(new PropertyValue(new Vector4(color.R, color.G, color.B, 0)))
            }.OutputVisualMap
        };
        Add(view, layer, cx - diameter / 2, cy - diameter / 2, diameter, diameter, Color.Transparent);
    }
    private View Gradient(Layer layer, float x, float y, float width, float height, Color tint, float left, float right, float radius)
    {
        var view = new View { CornerRadius = new Vector4(radius, radius, radius, radius) };
        view.Background = LinearGradient(tint, tint, left, right, -0.5f, 0, 0.5f, 0);
        Add(view, layer, x, y, width, height, Color.Transparent); return view;
    }
    // The 1254px artwork is decoded at its on-screen size, not as a ~6 MB full bitmap.
    private void Logo(Layer layer, float x, float y, float size)
    {
        var logo = new ImageView { ResourceUrl = logoPath, DesiredWidth = (int)size, DesiredHeight = (int)size };
        Add(logo, layer, x, y, size, size, Color.Transparent);
    }
    private View Panel(Layer layer, float x, float y, float width, float height, Color color, float radius = 0)
    {
        var view = new View { BackgroundColor = color, CornerRadius = new Vector4(radius, radius, radius, radius) };
        Add(view, layer, x, y, width, height, color); return view;
    }
    private TextLabel Text(Layer layer, string text, float x, float y, float width, float height, float px, Color color,
        string weight = Regular, HorizontalAlignment align = HorizontalAlignment.Begin, bool multi = false)
    {
        var label = Label(text, px, color, weight, align, multi);
        Add(label, layer, x, y, width, height, Color.Transparent); return label;
    }
    private static TextLabel Text(View parent, string text, float x, float y, float width, float height, float px, Color color,
        string weight = Regular, HorizontalAlignment align = HorizontalAlignment.Begin)
    {
        var label = Label(text, px, color, weight, align, false);
        label.Position = new Position(x, y); label.Size = new Size(width, height);
        parent.Add(label); return label;
    }
    private static TextLabel Label(string text, float px, Color color, string weight, HorizontalAlignment align, bool multi)
    {
        var label = new TextLabel { Text = text, PixelSize = px, TextColor = color, MultiLine = multi, Ellipsis = true,
            EnableMarkup = false, HorizontalAlignment = align, VerticalAlignment = multi ? VerticalAlignment.Top : VerticalAlignment.Center };
        label.FontStyle = new PropertyMap().Add("weight", new PropertyValue(weight));
        return label;
    }
    private static ImageView Icon(View parent, string name, float x, float y, float size, Color color)
    {
        // White artwork tinted through View.Color, so focus can recolour it without a reload.
        var icon = new ImageView { ResourceUrl = UiIcons.For(name, Color.White, size), Position = new Position(x, y), Size = new Size(size, size), Color = color };
        parent.Add(icon); return icon;
    }
    private static void Icons(Element element, string name, float x, float y, float size, Color color)
    {
        element.Icon = Icon(element.View, name, x, y, size, color);
        element.IconTint = color;
    }
    // Rendered width of a label, so icon + text groups can be centred exactly.
    private static float Measure(TextLabel label)
    {
        try { var width = label.NaturalSize.Width; if (width > 0) return width; } catch { }
        return label.Text.Length * label.PixelSize * 0.56f;
    }
    // Pill button with optional icon. Focus turns every control white with dark content.
    private View Button(Layer layer, string key, string text, string icon, float x, float y, float width, float height,
        Action action, Kind kind)
    {
        var view = Panel(layer, x, y, width, height, kind == Kind.Primary ? Violet : Glass, height / 2);
        if (kind != Kind.Primary) { view.BorderlineWidth = 1.5f; view.BorderlineColor = GlassEdge; }
        var element = Find(view)!;
        var px = height * 0.33f;
        element.Label = Text(view, text, 0, 0, width, height, px, Color.White, Medium, HorizontalAlignment.Center);
        if (icon.Length != 0) {
            // Centre icon + gap + measured text as one group; never trust a character count.
            var iconSize = height * 0.38f;
            const float gap = 14;
            var textWidth = Math.Min(Measure(element.Label), width - 64 - iconSize - gap);
            var start = (width - (iconSize + gap + textWidth)) / 2;
            Icons(element, icon, start, (height - iconSize) / 2, iconSize, Color.White);
            element.Label.HorizontalAlignment = HorizontalAlignment.Begin;
            element.Label.Position = new Position(start + iconSize + gap, 0);
            element.Label.Size = new Size(textWidth + 4, height);
        }
        return Control(element, key, kind, action);
    }
    private View Round(Layer layer, string key, string icon, float x, float y, float size, Action action,
        Kind kind = Kind.Glass, Color? rest = null)
    {
        var view = Panel(layer, x, y, size, size, rest ?? Glass, size / 2);
        if (kind != Kind.Primary) { view.BorderlineWidth = 1.5f; view.BorderlineColor = GlassEdge; }
        var element = Find(view)!;
        var iconSize = MathF.Round(size * 0.4f);
        Icons(element, icon, (size - iconSize) / 2, (size - iconSize) / 2, iconSize, icon == "star-on" ? Gold : Color.White);
        return Control(element, key, kind, action);
    }
    private View Field(Layer layer, string key, string text, string icon, float x, float y, float width, float height,
        Action action, Color? textColor = null)
    {
        var view = Panel(layer, x, y, width, height, Glass, height / 2);
        view.BorderlineWidth = 1.5f; view.BorderlineColor = GlassEdge;
        var element = Find(view)!;
        Icons(element, icon, 32, (height - 30) / 2, 30, textColor ?? Soft);
        element.Label = Text(view, text, 80, 0, width - 120, height, height * 0.34f, textColor ?? Soft, Regular);
        return Control(element, key, Kind.Field, action);
    }
    private View Row(Layer layer, string key, string text, string icon, float x, float y, float width, float height, Action action)
    {
        var view = Panel(layer, x, y, width, height, new Color(1, 1, 1, 0.05f), 24);
        var element = Find(view)!;
        Icons(element, icon, 32, (height - 32) / 2, 32, icon == "star-on" ? Gold : Soft);
        element.Label = Text(view, text, 88, 0, width - 120, height, 28, Color.White, Medium);
        return Control(element, key, Kind.Row, action);
    }
    private View Control(Element element, string key, Kind kind, Action action)
    {
        element.Key = key; element.Kind = kind; element.Control = true; element.Action = action;
        element.Text = element.Label?.TextColor ?? Color.White;
        Interactive(element); return element.View;
    }
    private void Interactive(Element element)
    {
        var view = element.View;
        view.PivotPoint = PivotPoint.Center; view.PositionUsesPivotPoint = false;
        view.KeyEvent += HandleKey;
        view.FocusGained += (_, _) => Highlight(element, true);
        view.FocusLost += (_, _) => Highlight(element, false);
    }
    private void Highlight(Element element, bool on)
    {
        if (!elements.Contains(element)) return;
        var view = element.View;
        if (element.Kind == Kind.Tile) {
            view.BorderlineWidth = on ? 4 : 1.5f;
            view.BorderlineColor = on ? Color.White : GlassEdge;
            view.BackgroundColor = on ? new Color(1, 1, 1, 0.14f) : element.Normal;
            view.BoxShadow = on ? new Shadow(64, new Color(element.Glow.R, element.Glow.G, element.Glow.B, 0.7f), new Vector2(0, 18), new Vector2(0, 0))
                : new Shadow(0, Color.Transparent, new Vector2(0, 0), new Vector2(0, 0));
        } else {
            view.BackgroundColor = on ? Color.White : element.Normal;
            if (element.Label != null) element.Label.TextColor = on ? Ink : element.Text;
            if (element.Icon != null) element.Icon.Color = on ? Ink : element.IconTint;
            view.BoxShadow = on ? new Shadow(40, new Color(1, 1, 1, 0.18f), new Vector2(0, 8), new Vector2(0, 0))
                : new Shadow(0, Color.Transparent, new Vector2(0, 0), new Vector2(0, 0));
        }
        var scale = on ? (element.Kind == Kind.Tile ? 1.1f : element.Kind == Kind.Row ? 1.02f : 1.06f) : 1f;
        element.Motion?.Stop(); element.Motion?.Dispose();
        element.Motion = new Animation(160);
        element.Motion.AnimateTo(view, "Scale", new Vector3(scale, scale, 1), new AlphaFunction(AlphaFunction.BuiltinFunctions.EaseOut));
        element.Motion.Play();
    }
    private void Add(View view, Layer layer, float x, float y, float width, float height, Color color)
    {
        view.Position = new Position(x, y); view.Size = new Size(width, height);
        elements.Add(new Element { View = view, Layer = layer, Region = new FocusRegion(x, y, width, height), Normal = color });
        layers[(int)layer].Add(view);
    }
    private Element? Find(View view) => elements.FirstOrDefault(e => ReferenceEquals(e.View, view));
    private void Clear(Layer layer)
    {
        foreach (var element in elements.Where(e => e.Layer == layer).ToArray()) {
            element.Motion?.Stop(); element.Motion?.Dispose(); element.Motion = null;
            if (element.Control) element.View.KeyEvent -= HandleKey;
            element.View.Focusable = false; layers[(int)element.Layer].Remove(element.View); DisposeTree(element.View); elements.Remove(element);
        }
    }
    private static void DisposeTree(View view)
    {
        foreach (var child in view.Children.ToArray()) DisposeTree(child);
        view.Dispose();
    }

    // ---------- Visibility, layering and scaling ----------
    private bool LayerVisible(Layer layer) => nativeActive ? layer == Layer.Toast && clock.ElapsedMilliseconds < toastUntil : layer switch {
        Layer.Backdrop or Layer.Home or Layer.Hero or Layer.Sites => chrome.Surface == BrowserSurface.Home,
        Layer.Bar or Layer.BarItems => chrome.Overlay == ChromeOverlay.Controls,
        Layer.MenuFrame or Layer.MenuItems => chrome.Overlay == ChromeOverlay.Menu,
        Layer.Edit or Layer.Suggestions => chrome.Overlay == ChromeOverlay.Address,
        Layer.Loading => isLoading && chrome.Surface == BrowserSurface.Page,
        Layer.Toast => clock.ElapsedMilliseconds < toastUntil && chrome.Overlay is ChromeOverlay.None or ChromeOverlay.Controls,
        _ => false
    };
    private bool LayerActive(Layer layer) => chrome.Overlay switch {
        ChromeOverlay.Menu => layer is Layer.MenuItems,
        ChromeOverlay.Controls => layer is Layer.BarItems,
        ChromeOverlay.Address => layer is Layer.Suggestions,
        _ => chrome.Surface == BrowserSurface.Home && layer is Layer.Home or Layer.Hero or Layer.Sites
    };
    private void Render()
    {
        foreach (var item in elements) {
            item.Visible = LayerVisible(item.Layer);
            item.View.Focusable = item.Control && item.Visible && LayerActive(item.Layer);
            if (item.Visible) item.View.Show(); else item.View.Hide();
        }
        address.Focusable = chrome.Overlay == ChromeOverlay.Address;
        if (!chrome.PageInteractive) pageInput.DiscardPending();
        if (chrome.Overlay == ChromeOverlay.Address && editErrorShown) editError.Show(); else editError.Hide();
        UpdatePointer();
        // A transparent full-screen stage must not swallow pointer input meant for the page.
        stage.Sensitive = !chrome.PageInteractive;
        Resize();
        stage.RaiseToTop();
    }
    private void OnResized(object? sender, Tizen.NUI.Window.ResizedEventArgs args) => Resize();
    private void Resize()
    {
        var size = window.WindowSize;
        stage.Scale = new Vector3(size.Width / 1920f, size.Height / 1080f, 1);
        if (page.Current != null) {
            page.Current.Position = new Position(0, 0);
            page.Current.Size = new Size(size.Width, size.Height);
        }
    }
    private bool Tick(object sender, Tizen.NUI.Timer.TickEventArgs args)
    {
        if (startUrl != null) { var first = startUrl; startUrl = null; Navigate(first); }
        if (nativeActive && native != null) {
            if (native.Ended) StopNative("Koniec transmisji");
            else if (native.Failure != null) { metrics.Event("native-playback-error"); StopNative("Błąd odtwarzacza. Wróciłeś do strony"); }
        }
        if (toastUntil != 0 && clock.ElapsedMilliseconds >= toastUntil) { toastUntil = 0; Render(); }
        while (icons.TryTakeReady(out _)) homeStale = true;
        if (pointerShown && !cursor.Visible(clock.ElapsedMilliseconds)) UpdatePointer();
        if (chrome.Surface == BrowserSurface.Home) {
            UpdateClock();
            if (homeStale && chrome.Overlay == ChromeOverlay.None) RefreshHome();
        }
        return true;
    }
    private void SetStatus(string text)
    {
        if (terminating || status == null) return;
        status.Text = text; toastUntil = clock.ElapsedMilliseconds + 4000; Render();
    }
    private void FocusHome()
    {
        chrome.CloseAll();
        if (homeStale) UpdateHome();
        homeStale = false;
        Render();
        var first = elements.FirstOrDefault(e => e.Layer == Layer.Hero && e.Control);
        if (first != null) FocusManager.Instance.SetCurrentFocusView(first.View);
    }

    // ---------- Page lifecycle ----------
    private void Navigate(string input)
    {
        if (paused || terminating) return;
        if (!UrlSafety.TryNormalizeAddress(input, out var url)) {
            if (chrome.Overlay == ChromeOverlay.Address) {
                editHint.Text = "To nie jest adres strony. Spróbuj np. twitch.tv";
                var width = Math.Min(1460, 58 + Measure(editHint) + 30);
                editError.Size = new Size(width, 52); editHint.Size = new Size(width - 58, 52);
                editErrorShown = true; UpdateSuggestions();
                FocusManager.Instance.SetCurrentFocusView(address); // the rebuilt rows replace the focused one
            }
            else SetStatus("Nieprawidłowy adres strony");
            return;
        }
        if (System.Version.TryParse(platform, out var version) && version.Major < 9) {
            SetStatus("Ten wariant wymaga Tizen 9 lub nowszego"); return;
        }
        if (!ClosePage(false)) return;
        try {
            var view = page.Open(() => new WebView());
            view.Focusable = true;
            view.PageLoadStarted += OnLoadStarted; view.PageLoadFinished += OnLoadFinished; view.PageLoadError += OnLoadError;
            view.KeyEvent += HandleKey; window.Add(view);
            engine.Attach(view, () => ShowControls(true), Navigate, OnPageFullscreen, SetStatus, metrics);
            currentUrl = url;
            chrome.PageOpened(); cursor.Reset(clock.ElapsedMilliseconds); engine.Started(url); SetLoading(true); Render();
            toastUntil = 0; view.LoadUrl(url);
            FocusManager.Instance.SetCurrentFocusView(view); metrics.Event("nui-webview-created");
        } catch {
            metrics.Event("nui-webview-start-failed");
            if (ClosePage(false)) SetStatus("Nie udało się uruchomić silnika strony");
        }
    }
    private void OnLoadStarted(object? sender, WebViewPageLoadEventArgs args)
    {
        if (!ReferenceEquals(sender, page.Current) || paused || terminating) return;
        ResetPageInput();
        pageLoaded = false;
        loading.Restart(); SetLoading(true); metrics.Event("load-started"); engine.Started(page.Current!.Url);
    }
    private void OnLoadFinished(object? sender, WebViewPageLoadEventArgs args)
    {
        if (!ReferenceEquals(sender, page.Current) || paused || terminating) return;
        pageLoaded = true;
        if (UrlSafety.TryNormalizeAddress(page.Current!.Url, out var safe)) {
            currentUrl = safe; session.Remember(safe); homeStale = true;
        }
        engine.Loaded(currentUrl); metrics.Event("load-finished-ms", loading.ElapsedMilliseconds);
        SetLoading(false);
        // One contextual hint per run instead of a permanent key legend.
        if (!backHintShown && chrome.PageInteractive) { backHintShown = true; SetStatus("Wstecz pokaże pasek sterowania"); }
    }
    private void OnLoadError(object? sender, WebViewPageLoadErrorEventArgs args)
    {
        if (!ReferenceEquals(sender, page.Current) || paused || terminating) return;
        Interlocked.Increment(ref metrics.LoadErrors); metrics.Event("load-error"); SetLoading(false);
        SetStatus("Nie udało się załadować strony");
    }
    // HTML video fullscreen (Lab): get every overlay out of the picture.
    private void OnPageFullscreen(bool entered)
    {
        if (page.Current == null) return;
        if (entered && chrome.CloseAll()) { Render(); FocusManager.Instance.SetCurrentFocusView(page.Current); }
        metrics.Event(entered ? "fullscreen-enter" : "fullscreen-exit");
    }
    private void GoBack()
    {
        switch (chrome.Overlay) {
            case ChromeOverlay.Menu when menuNested: {
                // Land on the entry that led into the list, not on the first one.
                var from = "menu:" + (menuSection switch {
                    "favorites" => "Ulubione", "history" => "Historia", "protection" => "Ochrona witryny", _ => "Informacje"
                });
                OpenMenu();
                var entry = elements.FirstOrDefault(e => e.Layer == Layer.MenuItems && e.Key == from);
                if (entry != null) FocusManager.Instance.SetCurrentFocusView(entry.View);
                return;
            }
            case ChromeOverlay.Menu or ChromeOverlay.Address:
                chrome.Dismiss();
                if (chrome.Overlay == ChromeOverlay.Controls) { chrome.CloseAll(); ShowControls(); } else FocusHome();
                return;
            case ChromeOverlay.Controls: HideOverlay(); return;
        }
        // Page: Lab exits HTML fullscreen and walks history first. At the root, Back shows the
        // bar with Close focused, so a single stray press never throws away the stream.
        if (page.Current != null) { if (!engine.Back()) ShowControls(true); return; }
        Exit();
    }
    private bool HandleKey(object sender, View.KeyEventArgs args)
    {
        if (args.Key.State != Key.StateType.Down) return false;
        var key = args.Key.KeyPressedName;
        var typing = ReferenceEquals(sender, address);
        if (key is "XF86Back" or "Escape" || (key == "BackSpace" && !typing)) { GoBack(); return true; }
        // Menu or a colour key summons the bar; Samsung's Solar remote may deliver neither.
        if (key is "XF86Menu" or "Menu" or "XF86Red" or "XF86Green" or "XF86Yellow" or "XF86Blue") {
            if (chrome.Surface == BrowserSurface.Page) { if (chrome.Overlay == ChromeOverlay.None) ShowControls(); else HideOverlay(); }
            else if (chrome.Overlay == ChromeOverlay.Menu) GoBack(); else OpenMenu();
            return true;
        }
        if (key == "XF86Exit") { ClosePage(); return true; }
        if (ReferenceEquals(sender, page.Current)) return PageKey(key);
        if (typing) {
            if (key is "Return" or "KP_Enter") { if (chrome.Overlay == ChromeOverlay.Address) Submit(address.Text); return true; }
            if (key == "Down") {
                var first = elements.FirstOrDefault(e => e.Layer == Layer.Suggestions && e.Control);
                if (first != null) FocusManager.Instance.SetCurrentFocusView(first.View);
                return true;
            }
            return key == "Up"; // Left/Right keep caret editing.
        }
        if (key is "Return" or "KP_Enter") { Find((View)sender)?.Action?.Invoke(); return true; }
        var dx = key == "Left" ? -1 : key == "Right" ? 1 : 0;
        var dy = key == "Up" ? -1 : key == "Down" ? 1 : 0;
        if (dx == 0 && dy == 0) return false;
        if (dy == 1 && chrome.Overlay == ChromeOverlay.Controls) { HideOverlay(); return true; }
        var current = Find((View)sender);
        // The side rail is its own lane: entered with Left, walked with Up/Down.
        var inRail = current?.Key.StartsWith("rail-", StringComparison.Ordinal) == true;
        var controls = elements.Where(e => e.Control && e.Visible && LayerActive(e.Layer) &&
            (dx != 0 || e.Key.StartsWith("rail-", StringComparison.Ordinal) == inRail)).ToList();
        var index = controls.FindIndex(e => ReferenceEquals(e.View, sender));
        var next = DirectionalFocus.Next(controls.Select(e => e.Region).ToArray(), index, dx, dy);
        if (next == index && dy == -1 && chrome.Overlay == ChromeOverlay.Address) FocusManager.Instance.SetCurrentFocusView(address);
        else if (next >= 0) FocusManager.Instance.SetCurrentFocusView(controls[next].View);
        return true;
    }
    private bool ClosePage(bool focusHome = true)
    {
        if (nativeActive) StopNative();
        pageLoaded = false;
        ResetPageInput();
        if (!page.TryClose(view => {
            engine.Detach(view);
            view.Focusable = false;
            view.PageLoadStarted -= OnLoadStarted; view.PageLoadFinished -= OnLoadFinished; view.PageLoadError -= OnLoadError;
            view.KeyEvent -= HandleKey; window.Remove(view); view.Dispose();
        })) {
            metrics.Event("webview-destruction-failed"); SetStatus("Nie udało się zamknąć strony. Uruchom Isko ponownie"); return false;
        }
        engine.Released();
        currentUrl = ""; SetLoading(false); cursor.Hide();
        chrome.PageClosed(); toastUntil = 0; UpdateHome(); homeStale = false; Render();
        if (!terminating && focusHome) FocusHome();
        metrics.Sample("after-close"); return true;
    }
    protected override void OnPause()
    {
        paused = true;
        try { metrics.Event(ClosePage(false) ? "pause-page-released" : "pause-page-release-failed"); }
        finally { base.OnPause(); }
    }
    protected override void OnResume()
    {
        base.OnResume(); paused = false;
        if (status != null && !page.CleanupFailed) FocusHome();
    }
    protected override void OnTerminate()
    {
        terminating = true;
        try {
            if (timer != null) { timer.Stop(); timer.Tick -= Tick; timer.Dispose(); timer = null; }
            if (inputTimer != null) { inputTimer.Stop(); inputTimer.Tick -= FlushPageInput; inputTimer.Dispose(); inputTimer = null; }
            loadingMotion?.Stop(); loadingMotion?.Dispose(); loadingMotion = null;
            if (window != null) { window.Resized -= OnResized; window.KeyEvent -= OnWindowKey; ClosePage(false); }
        } finally { engine.Dispose(); metrics.Dispose(); icons?.Dispose(); base.OnTerminate(); }
    }
}
