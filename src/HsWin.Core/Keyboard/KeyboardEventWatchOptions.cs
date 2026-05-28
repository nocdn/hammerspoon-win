namespace HsWin.Core.Keyboard;

public sealed record KeyboardEventWatchOptions(
    bool IncludeInjected,
    bool Blocking,
    IReadOnlySet<uint>? KeyFilter = null,
    bool Prepend = false)
{
    public static KeyboardEventWatchOptions Default { get; } = new(
        IncludeInjected: false,
        Blocking: false);

    public bool HasKeyFilter => KeyFilter is { Count: > 0 };
}
