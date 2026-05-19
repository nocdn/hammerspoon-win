using HsWin.Core.Hotkeys;

namespace HsWin.Core.Keyboard;

/// <summary>
/// Tracks physical modifier keys from low-level keyboard hook events.
/// </summary>
public sealed class KeyboardModifierTracker
{
    private readonly HashSet<uint> _pressedModifierKeys = [];

    public HotkeyModifiers Pressed { get; private set; }

    public void Apply(uint virtualKey, bool isKeyUp)
    {
        if (!KeyboardKeyRules.IsModifierVirtualKey(virtualKey))
        {
            return;
        }

        if (isKeyUp)
        {
            _pressedModifierKeys.Remove(virtualKey);
        }
        else
        {
            _pressedModifierKeys.Add(virtualKey);
        }

        RecomputePressed();
    }

    public void Reset()
    {
        _pressedModifierKeys.Clear();
        Pressed = HotkeyModifiers.None;
    }

    private void RecomputePressed()
    {
        var pressed = HotkeyModifiers.None;
        foreach (var virtualKey in _pressedModifierKeys)
        {
            pressed |= KeyboardKeyRules.ModifierForVirtualKey(virtualKey);
        }

        Pressed = pressed;
    }
}
