using HsWin.App.Windows;

namespace HsWin.App.Tests;

public sealed class WindowIdTests
{
    [Fact]
    public void FormatUsesHexHandle()
    {
        Assert.Equal("0x1234", WindowId.Format(new IntPtr(0x1234)));
    }

    [Theory]
    [InlineData("0x1234")]
    [InlineData("4660")]
    public void TryParseAcceptsHexAndDecimalIds(string value)
    {
        Assert.True(WindowId.TryParse(value, out var handle));
        Assert.Equal(new IntPtr(0x1234), handle);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("not-a-window")]
    public void TryParseRejectsInvalidIds(string value)
    {
        Assert.False(WindowId.TryParse(value, out _));
    }
}
