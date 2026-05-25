using HsWin.App.Windows;
using HsWin.Core.Windows;

namespace HsWin.App.Tests;

public sealed class WindowPlacementCalculatorTests
{
    [Fact]
    public void CalculateTargetFramePreservesRelativePositionAcrossScreens()
    {
        var frame = new WindowRectangleSnapshot(100, 80, 800, 600);
        var source = new WindowRectangleSnapshot(0, 0, 1920, 1040);
        var target = new WindowRectangleSnapshot(1920, 0, 2560, 1400);

        var result = WindowPlacementCalculator.CalculateTargetFrame(frame, source, target, new WindowMoveOptions());

        Assert.Equal(new WindowRectangleSnapshot(2020, 80, 800, 600), result);
    }

    [Fact]
    public void CalculateTargetFrameClampsOversizedWindowsIntoTargetArea()
    {
        var frame = new WindowRectangleSnapshot(-120, 20, 3000, 1600);
        var source = new WindowRectangleSnapshot(-1920, 0, 1920, 1040);
        var target = new WindowRectangleSnapshot(0, 0, 1920, 1040);

        var result = WindowPlacementCalculator.CalculateTargetFrame(frame, source, target, new WindowMoveOptions());

        Assert.Equal(new WindowRectangleSnapshot(0, 0, 1920, 1040), result);
    }

    [Fact]
    public void CalculateTargetFrameCanFillTargetArea()
    {
        var frame = new WindowRectangleSnapshot(100, 80, 800, 600);
        var source = new WindowRectangleSnapshot(0, 0, 1920, 1040);
        var target = new WindowRectangleSnapshot(1920, 0, 2560, 1400);

        var result = WindowPlacementCalculator.CalculateTargetFrame(
            frame,
            source,
            target,
            new WindowMoveOptions(PreserveSize: false));

        Assert.Equal(new WindowRectangleSnapshot(1920, 0, 2560, 1400), result);
    }

    [Theory]
    [InlineData(10, 10, true)]
    [InlineData(2000, 10, false)]
    public void ContainsWindowCenterChecksCenterPoint(int x, int y, bool expected)
    {
        var area = new WindowRectangleSnapshot(0, 0, 1920, 1080);
        var frame = new WindowRectangleSnapshot(x, y, 800, 600);

        Assert.Equal(expected, WindowPlacementCalculator.ContainsWindowCenter(area, frame));
    }
}
