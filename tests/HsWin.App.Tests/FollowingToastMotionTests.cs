using System.Windows;

namespace HsWin.App.Tests;

public sealed class FollowingToastMotionTests
{
    [Fact]
    public void SmoothCursorUsesSnapExponentialResponse()
    {
        var elapsedMs = 10d;
        var result = FollowingToastMotion.SmoothCursor(
            new Point(0, 0),
            new Point(100, 50),
            TimeSpan.FromMilliseconds(elapsedMs));
        var expectedAmount = 1 - Math.Exp(
            -elapsedMs / FollowingToastStyleMetrics.FollowResponseMs);

        Assert.Equal(100 * expectedAmount, result.X, 6);
        Assert.Equal(50 * expectedAmount, result.Y, 6);
    }

    [Fact]
    public void SmoothCursorClampsLongFramesLikeSnap()
    {
        var result = FollowingToastMotion.SmoothCursor(
            new Point(0, 0),
            new Point(100, 0),
            TimeSpan.FromSeconds(1));
        var expectedAmount = 1 - Math.Exp(
            -FollowingToastStyleMetrics.MaximumFrameIntervalMs
            / FollowingToastStyleMetrics.FollowResponseMs);

        Assert.Equal(100 * expectedAmount, result.X, 6);
    }

    [Fact]
    public void PlacePillOffsetsBelowAndRightOfCursor()
    {
        var result = FollowingToastMotion.PlacePill(
            new Point(100, 200),
            new Size(80, 24),
            new Rect(0, 0, 1920, 1080));

        Assert.Equal(new Point(116, 218), result);
    }

    [Fact]
    public void PlacePillScalesCursorOffsetForNativePixels()
    {
        var result = FollowingToastMotion.PlacePill(
            new Point(100, 200),
            new Size(80, 24),
            new Rect(0, 0, 1920, 1080),
            1.5);

        Assert.Equal(new Point(124, 227), result);
    }

    [Fact]
    public void PlacePillClampsInsideScreenInset()
    {
        var result = FollowingToastMotion.PlacePill(
            new Point(990, 790),
            new Size(100, 30),
            new Rect(0, 0, 1000, 800));

        Assert.Equal(new Point(888, 758), result);
    }
}
