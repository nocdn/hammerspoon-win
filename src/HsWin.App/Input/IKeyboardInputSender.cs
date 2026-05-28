using HsWin.Core.Logging;

namespace HsWin.App.Input;

internal interface IKeyboardInputSender
{
    void SendKeyDown(uint virtualKey, IRuntimeLogger? logger = null);

    void SendKeyUp(uint virtualKey, IRuntimeLogger? logger = null);

    void SendTap(
        uint virtualKey,
        IReadOnlyList<uint>? suppressedModifierVirtualKeys = null,
        IReadOnlyList<uint>? modifierVirtualKeys = null,
        IRuntimeLogger? logger = null);
}

internal sealed class NativeKeyboardInputSender : IKeyboardInputSender
{
    public static NativeKeyboardInputSender Instance { get; } = new();

    private NativeKeyboardInputSender()
    {
    }

    public void SendKeyDown(uint virtualKey, IRuntimeLogger? logger = null)
    {
        KeyboardInputSender.SendKeyDown(virtualKey, logger);
    }

    public void SendKeyUp(uint virtualKey, IRuntimeLogger? logger = null)
    {
        KeyboardInputSender.SendKeyUp(virtualKey, logger);
    }

    public void SendTap(
        uint virtualKey,
        IReadOnlyList<uint>? suppressedModifierVirtualKeys = null,
        IReadOnlyList<uint>? modifierVirtualKeys = null,
        IRuntimeLogger? logger = null)
    {
        KeyboardInputSender.SendTap(virtualKey, suppressedModifierVirtualKeys, modifierVirtualKeys, logger);
    }
}
