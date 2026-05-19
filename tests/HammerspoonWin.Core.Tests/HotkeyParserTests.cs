using HammerspoonWin.Core.Hotkeys;

namespace HammerspoonWin.Core.Tests;

public sealed class HotkeyParserTests
{
    [Fact]
    public void ParseAcceptsModifierArrayAndLetterKey()
    {
        var definition = HotkeyParser.Parse(new[] { "ctrl", "alt" }, "R");

        Assert.Equal(HotkeyModifiers.Control | HotkeyModifiers.Alt, definition.Modifiers);
        Assert.Equal((uint)'R', definition.VirtualKey);
    }

    [Theory]
    [InlineData("command", HotkeyModifiers.Win)]
    [InlineData("cmd", HotkeyModifiers.Win)]
    [InlineData("option", HotkeyModifiers.Alt)]
    [InlineData("control", HotkeyModifiers.Control)]
    public void ParseAcceptsHammerspoonStyleModifierAliases(string modifier, HotkeyModifiers expected)
    {
        var definition = HotkeyParser.Parse(new[] { modifier }, "K");

        Assert.Equal(expected, definition.Modifiers);
    }

    [Theory]
    [InlineData("F1", 0x70u)]
    [InlineData("F12", 0x7Bu)]
    [InlineData("left", 0x25u)]
    [InlineData("space", 0x20u)]
    [InlineData("return", 0x0Du)]
    public void ParseAcceptsNamedKeys(string key, uint expectedVirtualKey)
    {
        var definition = HotkeyParser.Parse(new[] { "ctrl" }, key);

        Assert.Equal(expectedVirtualKey, definition.VirtualKey);
    }

    [Fact]
    public void ParseRejectsUnsupportedModifier()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            HotkeyParser.Parse(new[] { "hyper" }, "K"));

        Assert.Contains("Unsupported hotkey modifier", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseRejectsUnsupportedKey()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            HotkeyParser.Parse(new[] { "ctrl" }, "not-a-key"));

        Assert.Contains("Unsupported hotkey key", exception.Message, StringComparison.Ordinal);
    }
}
