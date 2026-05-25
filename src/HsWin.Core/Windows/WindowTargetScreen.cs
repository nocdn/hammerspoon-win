namespace HsWin.Core.Windows;

public sealed record WindowTargetScreen(
    string Id,
    string Name,
    WindowRectangleSnapshot Bounds,
    WindowRectangleSnapshot WorkingArea);
