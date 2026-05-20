namespace HsWin.Core.Keyboard;

public sealed record KeyboardEventWatchOptions(bool IncludeInjected, bool Blocking)
{
    public static KeyboardEventWatchOptions Default { get; } = new(
        IncludeInjected: false,
        Blocking: false);
}
