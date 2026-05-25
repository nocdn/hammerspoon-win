namespace HsWin.App.Windows;

internal enum WindowMoveStrategy
{
    SetFrame,
    SetRestoreBoundsAndRestore,
    RestoreMoveAndMaximize
}

internal static class WindowMoveStrategySelector
{
    public const int SwShowminimized = 2;

    public const int SwShowmaximized = 3;

    public static WindowMoveStrategy Select(int showCommand) =>
        showCommand switch
        {
            SwShowmaximized => WindowMoveStrategy.RestoreMoveAndMaximize,
            SwShowminimized => WindowMoveStrategy.SetRestoreBoundsAndRestore,
            _ => WindowMoveStrategy.SetFrame
        };
}
