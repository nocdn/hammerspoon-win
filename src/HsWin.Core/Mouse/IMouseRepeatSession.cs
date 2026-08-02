namespace HsWin.Core.Mouse;

/// <summary>
/// A running mouse-repeat loop that can change rate without tear-down races.
/// </summary>
public interface IMouseRepeatSession : IDisposable
{
    int IntervalMs { get; }

    void SetIntervalMs(int intervalMs);
}
