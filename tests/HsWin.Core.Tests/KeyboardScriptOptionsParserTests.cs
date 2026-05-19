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
    }

    [Fact]
    public void ParseWatchOptionsReadsIncludeInjected()
    {
        var options = KeyboardScriptOptionsParser.ParseWatchOptions(
            new Dictionary<string, object?> { ["includeInjected"] = true });

        Assert.True(options.IncludeInjected);
    }

    [Fact]
    public void ParseTapOptionsReadsSuppressedModifiers()
    {
        var options = KeyboardScriptOptionsParser.ParseTapOptions(
            new Dictionary<string, object?> { ["suppressPhysicalModifiers"] = new[] { "alt", "shift" } });

        Assert.Equal(HotkeyModifiers.Alt | HotkeyModifiers.Shift, options.SuppressPhysicalModifiers);
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
