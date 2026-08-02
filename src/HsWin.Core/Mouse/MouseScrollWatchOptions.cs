namespace HsWin.Core.Mouse;

public sealed record MouseScrollWatchOptions(
    bool IncludeInjected,
    bool Blocking,
    MouseScrollAxis Axes,
    bool Prepend = false)
{
    public static MouseScrollWatchOptions Default { get; } = new(
        IncludeInjected: false,
        Blocking: false,
        Axes: MouseScrollAxis.Both);

    public bool IncludesAxis(MouseScrollAxis axis) =>
        axis != MouseScrollAxis.None && Axes.HasFlag(axis);
}
