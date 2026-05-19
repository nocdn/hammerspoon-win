namespace HsWin.Core.Keyboard;

public sealed record KeyboardEventWatchOptions(bool IncludeInjected)
{
    public static KeyboardEventWatchOptions Default { get; } = new(IncludeInjected: false);
}
