using System;

/// <summary>
/// Simple global manager to let other scripts signal that they are currently dragging or capturing pointer input.
/// Call InputLockManager.BeginDrag() when starting a drag on a model, and InputLockManager.EndDrag() when finished.
/// Uses a reference counter so multiple overlapping requests are handled safely.
/// </summary>
public static class InputLockManager
{
    static int dragCount = 0;

    public static bool IsDragging => dragCount > 0;

    public static void BeginDrag()
    {
        dragCount++;
    }

    public static void EndDrag()
    {
        dragCount = Math.Max(0, dragCount - 1);
    }

    public static void Reset()
    {
        dragCount = 0;
    }
}