namespace HsWin.Core.Mouse;

public sealed record MouseScreenSnapshot(
    string Id,
    string Name,
    bool IsPrimary,
    MousePointSnapshot MousePosition,
    MouseRectangleSnapshot Bounds,
    MouseRectangleSnapshot WorkingArea);
