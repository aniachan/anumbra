using System;

namespace Anumbra;

/// <summary>
/// Calls an action on dispose. Used in <c>using</c> statements to ensure
/// paired ImGui calls (EndGroup, TreePop, EndPopup, etc.) always run.
/// Identical to the pattern used in Heliosphere.
/// </summary>
internal sealed class OnDispose : IDisposable
{
    private readonly Action _action;
    private bool _disposed;

    internal OnDispose(Action action)
    {
        _action = action;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _action();
    }
}
