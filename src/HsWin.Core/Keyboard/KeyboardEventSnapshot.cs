namespace HsWin.Core.Keyboard;

public sealed record KeyboardEventSnapshot(
    string Type,
    uint KeyCode,
    string Key,
    string[] Modifiers,
    uint ModifierFlags,
    bool IsKeyDown,
    bool IsKeyUp,
    bool IsModifier,
    bool IsInjected,
    bool IsExtended);
