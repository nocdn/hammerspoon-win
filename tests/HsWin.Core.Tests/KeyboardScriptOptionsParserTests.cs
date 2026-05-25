using HsWin.Core.Hotkeys;
using HsWin.Core.Keyboard;

namespace HsWin.Core.Tests;

public sealed class KeyboardScriptOptionsParserTests
{
    [Fact]
    public void ParseWatchOptionsDefaultsToIgnoringInjectedEvents()
    {
        var options = KeyboardScriptOptionsParser.ParseWatchOptions(null);

        Assert.False(options.IncludeInjected);
        Assert.False(options.Blocking);
    }

    [Fact]
    public void ParseWatchOptionsReadsIncludeInjected()
    {
        var options = KeyboardScriptOptionsParser.ParseWatchOptions(
            new Dictionary<string, object?> { ["includeInjected"] = true });

        Assert.True(options.IncludeInjected);
    }

    [Fact]
    public void ParseWatchOptionsReadsBlocking()
    {
        var options = KeyboardScriptOptionsParser.ParseWatchOptions(
            new Dictionary<string, object?> { ["blocking"] = true });

        Assert.True(options.Blocking);
    }

    [Theory]
    [InlineData("synchronous")]
    [InlineData("sync")]
    [InlineData("swallow")]
    public void ParseWatchOptionsReadsBlockingAliases(string optionName)
    {
        var options = KeyboardScriptOptionsParser.ParseWatchOptions(
            new Dictionary<string, object?> { [optionName] = true });

        Assert.True(options.Blocking);
    }

    [Fact]
    public void ParseTapOptionsReadsSuppressedModifiers()
    {
        var options = KeyboardScriptOptionsParser.ParseTapOptions(
            new Dictionary<string, object?> { ["suppressPhysicalModifiers"] = new[] { "alt", "shift" } });

        Assert.Equal(HotkeyModifiers.Alt | HotkeyModifiers.Shift, options.SuppressPhysicalModifiers);
        Assert.Equal(HotkeyModifiers.None, options.Modifiers);
    }

    [Fact]
    public void ParseTapOptionsReadsModifiers()
    {
        var options = KeyboardScriptOptionsParser.ParseTapOptions(
            new Dictionary<string, object?> { ["modifiers"] = new[] { "win", "shift" } });

        Assert.Equal(HotkeyModifiers.None, options.SuppressPhysicalModifiers);
        Assert.Equal(HotkeyModifiers.Win | HotkeyModifiers.Shift, options.Modifiers);
    }

    [Fact]
    public void ParseRepeatOptionsReadsIntervalAndSuppressedModifiers()
    {
        var options = KeyboardScriptOptionsParser.ParseRepeatOptions(
            new Dictionary<string, object?>
            {
                ["intervalMs"] = 5,
                ["suppressPhysicalModifiers"] = new[] { "alt", "shift" }
            });

        Assert.Equal(5, options.IntervalMs);
        Assert.Equal(HotkeyModifiers.Alt | HotkeyModifiers.Shift, options.SuppressPhysicalModifiers);
    }
}
