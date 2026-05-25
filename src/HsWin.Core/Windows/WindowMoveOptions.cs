namespace HsWin.Core.Windows;

public sealed record WindowMoveOptions(
    bool PreserveSize = true,
    bool UseWorkingArea = true);
