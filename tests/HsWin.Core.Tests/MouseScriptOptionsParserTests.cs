using HsWin.Core.Mouse;

namespace HsWin.Core.Tests;

public sealed class MouseScriptOptionsParserTests
{
    [Fact]
    public void ParseRepeatOptionsDefaultsToTenMilliseconds()
    {
        var options = MouseScriptOptionsParser.ParseRepeatOptions(null);

        Assert.Equal(10, options.IntervalMs);
    }

    [Fact]
    public void ParseRepeatOptionsReadsIntervalAliases()
    {
        var intervalMs = MouseScriptOptionsParser.ParseRepeatOptions(
            new Dictionary<string, object?> { ["interval"] = 20 });

        Assert.Equal(20, intervalMs.IntervalMs);
    }

    [Fact]
    public void ParseRepeatOptionsReadsWindowMessageInputMethod()
    {
        var options = MouseScriptOptionsParser.ParseRepeatOptions(
            new Dictionary<string, object?>
            {
                ["intervalMs"] = 20,
                ["inputMethod"] = "windowMessage"
            });

        Assert.Equal(MouseInputMethod.WindowMessage, options.InputMethod);
    }

    [Fact]
    public void ParseRepeatOptionsDefaultsToSendInput()
    {
        var options = MouseScriptOptionsParser.ParseRepeatOptions(null);

        Assert.Equal(MouseInputMethod.SendInput, options.InputMethod);
    }

    [Fact]
    public void ParseRepeatOptionsRejectsUnknownInputMethod()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            MouseScriptOptionsParser.ParseRepeatOptions(
                new Dictionary<string, object?> { ["inputMethod"] = "unknown" }));

        Assert.Contains("sendInput", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public void ParseRepeatOptionsRejectsIntervalsOutsideSupportedRange(int intervalMs)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            MouseScriptOptionsParser.ParseRepeatOptions(
                new Dictionary<string, object?> { ["intervalMs"] = intervalMs }));

        Assert.Contains("between 1 and 1000", exception.Message, StringComparison.Ordinal);
    }
}
