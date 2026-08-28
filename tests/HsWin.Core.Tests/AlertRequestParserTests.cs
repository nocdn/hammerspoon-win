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
        Assert.Equal(AlertIcon.Auto, request.Icon);
        Assert.Equal(AlertIcon.Dot, request.EffectiveIcon);
        Assert.Equal(AlertStyle.Standard, request.Style);
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
        Assert.Equal(AlertIcon.None, request.EffectiveIcon);
        Assert.Equal(1500, request.DurationMs);
    }

    [Fact]
    public void FromScriptArgumentsReadsLoaderIconOption()
    {
        var request = AlertRequestParser.FromScriptArguments(
            "Working",
            new Dictionary<string, object?>
            {
                ["type"] = "normal",
                ["icon"] = "loader",
                ["durationMs"] = 60000
            });

        Assert.Equal(AlertKind.Normal, request.Kind);
        Assert.Equal(AlertIcon.Loader, request.Icon);
        Assert.Equal(AlertIcon.Loader, request.EffectiveIcon);
        Assert.Equal(60000, request.DurationMs);
    }

    [Fact]
    public void FromScriptArgumentsTreatsLoadingTrueAsLoaderIcon()
    {
        var request = AlertRequestParser.FromScriptArguments(
            "Working",
            new Dictionary<string, object?>
            {
                ["type"] = "normal",
                ["loading"] = true
            });

        Assert.Equal(AlertIcon.Loader, request.EffectiveIcon);
    }

    [Fact]
    public void FromScriptArgumentsTreatsLoadingFalseAsAutomaticIcon()
    {
        var request = AlertRequestParser.FromScriptArguments(
            "Done",
            new Dictionary<string, object?>
            {
                ["type"] = "success",
                ["loading"] = false
            });

        Assert.Equal(AlertIcon.Auto, request.Icon);
        Assert.Equal(AlertIcon.Dot, request.EffectiveIcon);
    }

    [Fact]
    public void FromScriptArgumentsReadsFollowingStyleAsTextOnlyPill()
    {
        var request = AlertRequestParser.FromScriptArguments(
            "Testing",
            new Dictionary<string, object?>
            {
                ["style"] = "following",
                ["type"] = "success",
                ["icon"] = "loader",
                ["durationMs"] = 6000
            });

        Assert.Equal(AlertStyle.Following, request.Style);
        Assert.Equal(AlertIcon.Loader, request.Icon);
        Assert.Equal(AlertIcon.None, request.EffectiveIcon);
        Assert.Equal(6000, request.DurationMs);
    }

    [Theory]
    [InlineData("following")]
    [InlineData("follow")]
    [InlineData("cursor")]
    [InlineData("cursor-following")]
    public void FromScriptArgumentsAcceptsFollowingStyleAliases(string style)
    {
        var request = AlertRequestParser.FromScriptArguments(
            "Testing",
            new Dictionary<string, object?> { ["variant"] = style });

        Assert.Equal(AlertStyle.Following, request.Style);
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
    public void FromScriptArgumentsRejectsUnknownIcon()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            AlertRequestParser.FromScriptArguments(
                "Saved",
                new Dictionary<string, object?>
                {
                    ["icon"] = "warning"
                }));

        Assert.Contains("Unknown alert icon", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FromScriptArgumentsRejectsUnknownStyle()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            AlertRequestParser.FromScriptArguments(
                "Saved",
                new Dictionary<string, object?>
                {
                    ["style"] = "floating"
                }));

        Assert.Contains("Unknown alert style", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FromScriptArgumentsRejectsNegativeDuration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AlertRequestParser.FromScriptArguments("Saved", "success", -1));
    }
}
