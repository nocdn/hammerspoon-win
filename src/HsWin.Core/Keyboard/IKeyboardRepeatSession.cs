namespace HsWin.Core.Keyboard;

/// <summary>
/// A running keyboard-repeat loop that can change rate without tear-down races.
/// </summary>
public interface IKeyboardRepeatSession : IDisposable
{
    int IntervalMs { get; }

    void SetIntervalMs(int intervalMs);
}
