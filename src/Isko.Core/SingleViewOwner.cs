using System;

namespace Isko.Core;

// UI-thread ownership. A failed release retains ownership and forbids replacement.
public sealed class SingleViewOwner<T> where T : class
{
    public T? Current { get; private set; }
    public bool CleanupFailed { get; private set; }
    private bool changing;

    public T Open(Func<T> create)
    {
        if (changing || CleanupFailed) throw new InvalidOperationException("View ownership is not available.");
        if (Current != null) return Current;
        changing = true;
        try { return Current = create() ?? throw new InvalidOperationException("Factory returned no view."); }
        finally { changing = false; }
    }

    public bool TryClose(Action<T> release)
    {
        if (changing) return false;
        if (Current == null) return true;
        changing = true;
        try {
            release(Current);
            Current = null;
            CleanupFailed = false;
            return true;
        } catch {
            CleanupFailed = true;
            return false;
        } finally { changing = false; }
    }
}
