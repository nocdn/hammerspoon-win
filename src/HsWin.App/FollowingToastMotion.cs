using System.Windows;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace HsWin.App;

internal static class FollowingToastMotion
{
    public static Point SmoothCursor(Point current, Point target, TimeSpan elapsed)
    {
        var elapsedMs = Math.Clamp(
            elapsed.TotalMilliseconds,
            FollowingToastStyleMetrics.MinimumFrameIntervalMs,
            FollowingToastStyleMetrics.MaximumFrameIntervalMs);
        var followAmount = 1d - Math.Exp(-elapsedMs / FollowingToastStyleMetrics.FollowResponseMs);

        return new Point(
            current.X + ((target.X - current.X) * followAmount),
            current.Y + ((target.Y - current.Y) * followAmount));
    }

    public static Point PlacePill(
        Point cursor,
        Size pillSize,
        Rect screenBounds,
        double scale = 1)
    {
        var screenInset = FollowingToastStyleMetrics.ScreenInset * scale;
        var insetBounds = new Rect(
            screenBounds.Left + screenInset,
            screenBounds.Top + screenInset,
            Math.Max(0, screenBounds.Width - (screenInset * 2)),
            Math.Max(0, screenBounds.Height - (screenInset * 2)));

        var left = cursor.X + (FollowingToastStyleMetrics.CursorOffsetX * scale);
        var top = cursor.Y + (FollowingToastStyleMetrics.CursorOffsetY * scale);
        left = ClampOrigin(left, pillSize.Width, insetBounds.Left, insetBounds.Right);
        top = ClampOrigin(top, pillSize.Height, insetBounds.Top, insetBounds.Bottom);
        return new Point(left, top);
    }

    private static double ClampOrigin(double origin, double length, double minimum, double maximum)
    {
        if (length >= maximum - minimum)
        {
            return minimum;
        }

        return Math.Clamp(origin, minimum, maximum - length);
    }
}
