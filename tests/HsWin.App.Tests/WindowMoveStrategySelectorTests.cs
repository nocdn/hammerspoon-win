using HsWin.App.Windows;

namespace HsWin.App.Tests;

public sealed class WindowMoveStrategySelectorTests
{
    [Fact]
    public void SelectUsesRestoreMoveAndMaximizeForMaximizedWindows()
    {
        Assert.Equal(
            WindowMoveStrategy.RestoreMoveAndMaximize,
            WindowMoveStrategySelector.Select(WindowMoveStrategySelector.SwShowmaximized));
    }

    [Fact]
    public void SelectUsesRestoreBoundsAndRestoreForMinimizedWindows()
    {
        Assert.Equal(
            WindowMoveStrategy.SetRestoreBoundsAndRestore,
            WindowMoveStrategySelector.Select(WindowMoveStrategySelector.SwShowminimized));
    }

    [Fact]
    public void SelectUsesSetFrameForNormalWindows()
    {
        Assert.Equal(WindowMoveStrategy.SetFrame, WindowMoveStrategySelector.Select(1));
    }
}
