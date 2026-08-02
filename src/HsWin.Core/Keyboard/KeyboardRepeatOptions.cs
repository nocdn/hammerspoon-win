using HsWin.Core.Hotkeys;

namespace HsWin.Core.Keyboard;

public sealed record KeyboardRepeatOptions(
    int IntervalMs,
    HotkeyModifiers SuppressPhysicalModifiers,
    KeyboardInputMethod InputMethod = KeyboardInputMethod.SendInput,
    int KeyDownMs = 0)
{
    public const int DefaultIntervalMs = 10;
    public const int MinimumIntervalMs = 1;
    public const int MaximumIntervalMs = 1000;
    public const int DefaultKeyDownMs = 0;

    public static KeyboardRepeatOptions Default { get; } =
        new(DefaultIntervalMs, HotkeyModifiers.None, KeyboardInputMethod.SendInput, DefaultKeyDownMs);
}
