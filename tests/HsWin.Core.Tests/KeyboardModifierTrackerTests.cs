using HsWin.Core.Hotkeys;
using HsWin.Core.Keyboard;

namespace HsWin.Core.Tests;

public sealed class KeyboardModifierTrackerTests
{
    [Fact]
    public void ApplyTracksLeftModifiersFromHookEvents()
    {
        var tracker = new KeyboardModifierTracker();

        tracker.Apply(KeyboardKeyRules.VkLeftMenu, isKeyUp: false);
        tracker.Apply(KeyboardKeyRules.VkLeftShift, isKeyUp: false);

        Assert.Equal(HotkeyModifiers.Alt | HotkeyModifiers.Shift, tracker.Pressed);
    }

    [Fact]
    public void ApplyKeepsModifierPressedUntilBothSidesAreReleased()
    {
        var tracker = new KeyboardModifierTracker();

        tracker.Apply(KeyboardKeyRules.VkLeftShift, isKeyUp: false);
        tracker.Apply(KeyboardKeyRules.VkRightShift, isKeyUp: false);
        tracker.Apply(KeyboardKeyRules.VkLeftShift, isKeyUp: true);

        Assert.Equal(HotkeyModifiers.Shift, tracker.Pressed);
    }

    [Fact]
    public void ApplyIgnoresDuplicateModifierKeyDown()
    {
        var tracker = new KeyboardModifierTracker();

        tracker.Apply(KeyboardKeyRules.VkLeftShift, isKeyUp: false);
        tracker.Apply(KeyboardKeyRules.VkLeftShift, isKeyUp: false);
        tracker.Apply(KeyboardKeyRules.VkLeftShift, isKeyUp: true);

        Assert.Equal(HotkeyModifiers.None, tracker.Pressed);
    }

    [Fact]
    public void ApplyIgnoresNonModifierKeys()
    {
        var tracker = new KeyboardModifierTracker();

        tracker.Apply((uint)'W', isKeyUp: false);

        Assert.Equal(HotkeyModifiers.None, tracker.Pressed);
    }
}
