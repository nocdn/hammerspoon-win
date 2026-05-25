namespace HsWin.Core.Windows;

public sealed record WindowSnapshot(
    string Id,
    string Title,
    int ProcessId,
    string? ProcessName,
    WindowRectangleSnapshot Frame,
    bool IsMinimized,
    bool IsMaximized,
    bool IsVisible);
