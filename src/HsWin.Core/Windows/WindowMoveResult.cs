namespace HsWin.Core.Windows;

public sealed record WindowMoveResult(
    string WindowId,
    bool Success,
    bool Moved,
    string? Reason,
    WindowRectangleSnapshot? Frame)
{
    public static WindowMoveResult MovedTo(string windowId, WindowRectangleSnapshot frame) =>
        new(windowId, Success: true, Moved: true, Reason: null, frame);

    public static WindowMoveResult AlreadyOnScreen(string windowId, WindowRectangleSnapshot frame) =>
        new(windowId, Success: true, Moved: false, Reason: "already-on-screen", frame);

    public static WindowMoveResult NotMoved(string windowId, string reason) =>
        new(windowId, Success: false, Moved: false, reason, Frame: null);
}
