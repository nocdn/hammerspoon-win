namespace HsWin.App.Tests;

public sealed class FollowingToastStyleMetricsTests
{
    [Fact]
    public void FollowingToastMatchesSnapPillGeometryAndTypography()
    {
        Assert.Equal(12, FollowingToastStyleMetrics.TextFontSize);
        Assert.Equal(500, FollowingToastStyleMetrics.TextFontWeight.ToOpenTypeWeight());
        Assert.Equal(251, FollowingToastStyleMetrics.FillColorComponent);
        Assert.Equal(229, FollowingToastStyleMetrics.OutlineColorComponent);
        Assert.Equal(51, FollowingToastStyleMetrics.TextColorComponent);
        Assert.Equal(8, FollowingToastStyleMetrics.HorizontalPadding);
        Assert.Equal(4, FollowingToastStyleMetrics.VerticalPadding);
        Assert.Equal(1, FollowingToastStyleMetrics.OutlineWidth);
        Assert.Equal(16, FollowingToastStyleMetrics.CursorOffsetX);
        Assert.Equal(18, FollowingToastStyleMetrics.CursorOffsetY);
        Assert.Equal(12, FollowingToastStyleMetrics.ScreenInset);
    }

    [Fact]
    public void FollowingToastMatchesSnapMotionTiming()
    {
        Assert.Equal(60, FollowingToastStyleMetrics.FollowResponseMs);
        Assert.Equal(240, FollowingToastStyleMetrics.TargetFrameRate);
        Assert.Equal(120, FollowingToastStyleMetrics.EnterDurationMs);
        Assert.Equal(160, FollowingToastStyleMetrics.ExitDurationMs);
        Assert.Equal(0.15, FollowingToastStyleMetrics.TransitionBlurRadius);
    }

    [Fact]
    public void FollowingToastMatchesSnapShadow()
    {
        Assert.Equal(2, FollowingToastStyleMetrics.ShadowBlurRadius);
        Assert.Equal(0.10, FollowingToastStyleMetrics.ShadowOpacity);
    }
}
