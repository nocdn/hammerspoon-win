using HsWin.Core.Hotkeys;

namespace HsWin.Core.Keyboard;

public sealed record KeyboardRepeatOptions(int IntervalMs, HotkeyModifiers SuppressPhysicalModifiers)
{
    public const int DefaultIntervalMs = 10;
    public const int MinimumIntervalMs = 1;
    public const int MaximumIntervalMs = 1000;

    public static KeyboardRepeatOptions Default { get; } =
        new(DefaultIntervalMs, HotkeyModifiers.None);
}
