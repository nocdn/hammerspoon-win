using HsWin.Core.Alerts;

namespace HsWin.Core.Tests;

public sealed class AlertRequestParserTests
{
    [Fact]
    public void FromScriptArgumentsUsesRequestedDefaults()
    {
        var request = AlertRequestParser.FromScriptArguments("Loaded");

        Assert.Equal("Loaded", request.Text);
        Assert.Equal(AlertKind.Success, request.Kind);
        Assert.Equal(2000, request.DurationMs);
    }

    [Theory]
    [InlineData("normal", AlertKind.Normal)]
    [InlineData("none", AlertKind.Normal)]
    [InlineData("success", AlertKind.Success)]
    [InlineData("ok", AlertKind.Success)]
    [InlineData("error", AlertKind.Error)]
    [InlineData("failed", AlertKind.Error)]
    public void FromScriptArgumentsAcceptsKnownKindAliases(string kind, AlertKind expectedKind)
    {
        var request = AlertRequestParser.FromScriptArguments("Saved", kind, 1234);

        Assert.Equal(expectedKind, request.Kind);
        Assert.Equal(1234, request.DurationMs);
    }

    [Fact]
    public void FromScriptArgumentsReadsDictionaryOptions()
    {
        var request = AlertRequestParser.FromScriptArguments(
            "Plain",
            new Dictionary<string, object?>
            {
                ["type"] = "normal",
                ["durationMs"] = 1500
            });

        Assert.Equal(AlertKind.Normal, request.Kind);
        Assert.Equal(1500, request.DurationMs);
    }

    [Fact]
    public void FromScriptArgumentsLetsExplicitDurationOverrideOptionsObject()
    {
        var request = AlertRequestParser.FromScriptArguments(
            "Saved",
            new Dictionary<string, object?>
            {
                ["type"] = "success",
                ["durationMs"] = 1500
            },
            2500);

        Assert.Equal(AlertKind.Success, request.Kind);
        Assert.Equal(2500, request.DurationMs);
    }

    [Fact]
    public void FromScriptArgumentsRejectsUnknownKind()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            AlertRequestParser.FromScriptArguments("Saved", "warning"));

        Assert.Contains("Unknown alert type", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FromScriptArgumentsRejectsNegativeDuration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AlertRequestParser.FromScriptArguments("Saved", "success", -1));
    }
}
