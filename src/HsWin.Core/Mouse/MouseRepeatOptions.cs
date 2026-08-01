namespace HsWin.Core.Mouse;

public sealed record MouseRepeatOptions(
    int IntervalMs,
    MouseInputMethod InputMethod = MouseInputMethod.SendInput)
{
    public const int DefaultIntervalMs = 10;
    public const int MinimumIntervalMs = 1;
    public const int MaximumIntervalMs = 1000;

    public static MouseRepeatOptions Default { get; } = new(DefaultIntervalMs, MouseInputMethod.SendInput);
}
