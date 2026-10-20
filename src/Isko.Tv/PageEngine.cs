using Tizen.NUI.BaseComponents;

namespace Isko.Tv;

// Public baseline: advanced engine capabilities are explicitly unavailable.
internal class PageEngine : IDisposable
{
    public virtual string Status => "Ochrona stron: niedostępna w tym wariancie.";
    public virtual string Instructions => "Menu / Wstecz: pasek · Zamknij: powrót do startu";
    public virtual void Attach(WebView view, Action close, Action<string> navigate, Action<bool> fullscreen, Action<string> status, Measurements metrics) { }
    public virtual void Started(string url) { }
    public virtual void Loaded(string url) { }
    public virtual void Detach(WebView view) { }
    public virtual bool Back() => false;
    public virtual void ToggleProtection() { }
    public virtual void ToggleException() { }
    public virtual void ToggleFixture() { }
    public virtual void Dispose() { }
}
