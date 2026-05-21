namespace HsWin.Core.Hotkeys;

public sealed record HotkeyHeldOptions(
    bool IncludeInjected,
    bool Blocking,
    bool AllowExtraModifiers,
    bool Repeat)
{
    public static HotkeyHeldOptions Default { get; } = new(
        IncludeInjected: false,
        Blocking: true,
        AllowExtraModifiers: false,
        Repeat: false);
}
