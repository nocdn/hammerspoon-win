using HsWin.Core.Mouse;
using System.Windows.Forms;
using DrawingRectangle = System.Drawing.Rectangle;

namespace HsWin.App.Mouse;

internal sealed class NativeMouseService : IMouseService
{
    public MouseScreenSnapshot? GetCurrentScreen()
    {
        var cursor = Cursor.Position;
        var screen = Screen.FromPoint(cursor);
        var id = string.IsNullOrWhiteSpace(screen.DeviceName)
            ? "Display"
            : screen.DeviceName;

        return new MouseScreenSnapshot(
            id,
            id,
            screen.Primary,
            new MousePointSnapshot(cursor.X, cursor.Y),
            ToRectangle(screen.Bounds),
            ToRectangle(screen.WorkingArea));
    }

    private static MouseRectangleSnapshot ToRectangle(DrawingRectangle rectangle)
    {
        return new MouseRectangleSnapshot(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
    }
}
