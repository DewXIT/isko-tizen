using System.Text.Json;
using Isko.Protection;
namespace Isko.Tv;

// Stores origins only: arbitrary paths may contain bearer tokens too.
internal sealed class SessionStore
{
    private readonly string path;
    public List<string> History { get; set; } = new();
    public List<string> Favorites { get; set; } = new();
    public string? LastOrigin { get; set; }
    public SessionStore(string directory) { path = Path.Combine(directory, "session-origins.json"); Load(); }
    private static string? Origin(string? url) => Uri.TryCreate(url, UriKind.Absolute, out var u) &&
        (u.Scheme == "http" || u.Scheme == "https") && string.IsNullOrEmpty(u.UserInfo)
        ? u.GetLeftPart(UriPartial.Authority) + "/" : null;
    public void Remember(string url)
    {
        var origin = Origin(url); if (origin == null) return;
        LastOrigin = origin; History.Remove(origin); History.Insert(0, origin);
        if (History.Count > 30) History.RemoveRange(30, History.Count - 30);
        Save();
    }
    public void Favorite(string url) { var o = Origin(url); if (o != null && !Favorites.Contains(o)) { Favorites.Add(o); if (Favorites.Count > 30) Favorites.RemoveAt(0); Save(); } }
    public bool IsFavorite(string url) { var o = Origin(url); return o != null && Favorites.Contains(o); }
    // Returns the new state: true when the origin is now a favorite.
    public bool ToggleFavorite(string url)
    {
        var o = Origin(url); if (o == null) return false;
        if (Favorites.Remove(o)) { Save(); return false; }
        Favorite(o); return true;
    }
    public void ClearHistory() { History.Clear(); LastOrigin = null; Save(); }
    private void Load()
    {
        try {
            if (!File.Exists(path) || new FileInfo(path).Length > 16384) return;
            var data = JsonSerializer.Deserialize<Data>(File.ReadAllText(path));
            if (data == null) return;
            LastOrigin = Origin(data.LastOrigin);
            History = data.History.Select(Origin).Where(x => x != null).Cast<string>().Distinct().Take(30).ToList();
            Favorites = data.Favorites.Select(Origin).Where(x => x != null).Cast<string>().Distinct().Take(30).ToList();
        } catch { /* Corrupt state is ignored; never log its contents. */ }
    }
    private void Save()
    {
        try {
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(new Data { LastOrigin = LastOrigin, History = History, Favorites = Favorites }));
            File.Move(temp, path, true);
        } catch { }
    }
    private sealed class Data
    {
        public string? LastOrigin { get; set; }
        public List<string> History { get; set; } = new();
        public List<string> Favorites { get; set; } = new();
    }
}
