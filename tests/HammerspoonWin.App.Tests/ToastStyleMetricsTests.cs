namespace HammerspoonWin.App.Tests;

public sealed class ToastStyleMetricsTests
{
    [Fact]
    public void ToastTypographyUsesRequestedFontSize()
    {
        Assert.Equal(14, ToastStyleMetrics.TextFontSize);
    }

    [Fact]
    public void ToastTypographyUsesSofterFontWeight()
    {
        Assert.Equal(400, ToastStyleMetrics.TextFontWeight.ToOpenTypeWeight());
    }

    [Fact]
    public void ToastDotUsesRequestedSizeAndGap()
    {
        Assert.Equal(6, ToastStyleMetrics.DotSize);
        Assert.Equal(8, ToastStyleMetrics.DotTextGap);
    }

    [Fact]
    public void ToastPaddingPreservesRobinInspiredInsets()
    {
        Assert.Equal(17, ToastStyleMetrics.DotStateLeftPadding);
        Assert.Equal(18, ToastStyleMetrics.DotStateRightPadding);
        Assert.Equal(17, ToastStyleMetrics.NormalHorizontalPadding);
        Assert.Equal(11, ToastStyleMetrics.VerticalPadding);
    }
}
