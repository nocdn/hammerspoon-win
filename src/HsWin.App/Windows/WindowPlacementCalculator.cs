using HsWin.Core.Windows;

namespace HsWin.App.Windows;

internal static class WindowPlacementCalculator
{
    public static WindowRectangleSnapshot CalculateTargetFrame(
        WindowRectangleSnapshot currentFrame,
        WindowRectangleSnapshot sourceArea,
        WindowRectangleSnapshot targetArea,
        WindowMoveOptions options)
    {
        var targetWidth = Math.Max(1, targetArea.Width);
        var targetHeight = Math.Max(1, targetArea.Height);
        var width = options.PreserveSize
            ? Math.Clamp(Math.Max(1, currentFrame.Width), 1, targetWidth)
            : targetWidth;
        var height = options.PreserveSize
            ? Math.Clamp(Math.Max(1, currentFrame.Height), 1, targetHeight)
            : targetHeight;

        var sourceOffsetX = currentFrame.X - sourceArea.X;
        var sourceOffsetY = currentFrame.Y - sourceArea.Y;
        var x = targetArea.X + Math.Clamp(sourceOffsetX, 0, Math.Max(0, targetWidth - width));
        var y = targetArea.Y + Math.Clamp(sourceOffsetY, 0, Math.Max(0, targetHeight - height));

        return new WindowRectangleSnapshot(x, y, width, height);
    }

    public static bool ContainsWindowCenter(WindowRectangleSnapshot area, WindowRectangleSnapshot frame)
    {
        var centerX = frame.X + (frame.Width / 2);
        var centerY = frame.Y + (frame.Height / 2);
        return centerX >= area.X
            && centerX < area.X + area.Width
            && centerY >= area.Y
            && centerY < area.Y + area.Height;
    }
}
