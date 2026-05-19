using HsWin.Core.Hotkeys;

namespace HsWin.Core.Tests;

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
    [InlineData("`", 0xC0u)]
    [InlineData("delete", 0x2Eu)]
    [InlineData("pageup", 0x21u)]
    public void ParseAcceptsNamedKeys(string key, uint expectedVirtualKey)
    {
        var definition = HotkeyParser.Parse(new[] { "ctrl" }, key);

        Assert.Equal(HotkeyInputKind.Keyboard, definition.InputKind);
        Assert.Equal(expectedVirtualKey, definition.VirtualKey);
    }

    [Theory]
    [InlineData("mouse.middle", HotkeyMouseButton.Middle)]
    [InlineData("middle", HotkeyMouseButton.Middle)]
    [InlineData("mouse.button3", HotkeyMouseButton.Middle)]
    [InlineData("mouse.back", HotkeyMouseButton.XButton1)]
    [InlineData("mouse.xbutton1", HotkeyMouseButton.XButton1)]
    [InlineData("button4", HotkeyMouseButton.XButton1)]
    [InlineData("mouse.forward", HotkeyMouseButton.XButton2)]
    [InlineData("mouse.xbutton2", HotkeyMouseButton.XButton2)]
    [InlineData("button5", HotkeyMouseButton.XButton2)]
    public void ParseAcceptsMouseButtonKeys(string key, HotkeyMouseButton expectedButton)
    {
        var definition = HotkeyParser.Parse(new[] { "ctrl", "alt" }, key);

        Assert.Equal(HotkeyInputKind.MouseButton, definition.InputKind);
        Assert.Equal(HotkeyModifiers.Control | HotkeyModifiers.Alt, definition.Modifiers);
        Assert.Equal(expectedButton, definition.MouseButton);
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
