using HsWin.Core.Hotkeys;
using HsWin.Core.Keyboard;

namespace HsWin.Core.Tests;

public sealed class KeyboardKeyRulesTests
{
    [Theory]
    [InlineData(KeyboardKeyRules.VkLeftShift, true)]
    [InlineData(KeyboardKeyRules.VkMenu, true)]
    [InlineData((uint)'W', false)]
    public void IsModifierVirtualKeyIdentifiesModifiers(uint virtualKey, bool expected)
    {
        Assert.Equal(expected, KeyboardKeyRules.IsModifierVirtualKey(virtualKey));
    }

    [Fact]
    public void GetModifierNamesReturnsStableScriptNames()
    {
        Assert.Equal(
            ["ctrl", "alt", "shift"],
            KeyboardKeyRules.GetModifierNames(
                HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift));
    }

    [Theory]
    [InlineData(0x25, true)]
    [InlineData(0x41, false)]
    public void IsExtendedVirtualKeyIdentifiesNavigationKeys(uint virtualKey, bool expected)
    {
        Assert.Equal(expected, KeyboardKeyRules.IsExtendedVirtualKey(virtualKey));
    }

    [Theory]
    [InlineData((uint)'W', "w")]
    [InlineData(0x21, "pageup")]
    [InlineData(0xC0, "`")]
    public void GetDisplayNameReturnsScriptFriendlyNames(uint virtualKey, string expected)
    {
        Assert.Equal(expected, KeyboardKeyRules.GetDisplayName(virtualKey));
    }
}
