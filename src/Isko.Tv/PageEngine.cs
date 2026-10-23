using Tizen.NUI.BaseComponents;

namespace Isko.Tv;

// Public baseline: advanced engine capabilities are explicitly unavailable.
internal class PageEngine : IDisposable
{
    public virtual string Status => "Ochrona stron: niedostępna w tym wariancie.";
    public virtual void Attach(WebView view, Action close, Action<string> navigate, Action<bool> fullscreen, Action<string> status, Measurements metrics) { }
    public virtual void Started(string url) { }
    public virtual void Loaded(string url) { }
    public virtual void Detach(WebView view) { }
    // Called after the WebView is destroyed: give the engine and the managed heap a chance to return memory.
    public virtual void Released() { }
    // Raised on the UI thread with the address of a streaming manifest the page requested, and whether the user had
    // just pressed OK. The address may hold a session token: keep it in memory only.
    public Action<string, bool>? StreamDetected { get; set; }
    public virtual bool Back() => false;
    public virtual bool CanGoBack => false;
    // The user pressed OK on the page: the only gesture signal the native popup event lacks.
    public virtual void UserInput() { }
    public virtual void ToggleProtection() { }
    public virtual void ToggleException() { }
    public virtual void ToggleFixture() { }
    public virtual void Dispose() { }
}
