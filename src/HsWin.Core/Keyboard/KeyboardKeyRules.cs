using HsWin.Core.Hotkeys;

namespace HsWin.Core.Keyboard;

public static class KeyboardKeyRules
{
    public const uint VkShift = 0x10;
    public const uint VkControl = 0x11;
    public const uint VkMenu = 0x12;
    public const uint VkLeftShift = 0xA0;
    public const uint VkRightShift = 0xA1;
    public const uint VkLeftControl = 0xA2;
    public const uint VkRightControl = 0xA3;
    public const uint VkLeftMenu = 0xA4;
    public const uint VkRightMenu = 0xA5;
    public const uint VkLeftWin = 0x5B;
    public const uint VkRightWin = 0x5C;

    /// <summary>
    /// Every virtual key <see cref="IsModifierVirtualKey"/> reports as a modifier. Callers that
    /// must observe all modifier events (for example watch key filters that need modifier
    /// key-ups) MUST derive their set from this list rather than copying it.
    /// </summary>
    public static readonly uint[] AllModifierVirtualKeys =
    [
        VkShift,
        VkControl,
        VkMenu,
        VkLeftShift,
        VkRightShift,
        VkLeftControl,
        VkRightControl,
        VkLeftMenu,
        VkRightMenu,
        VkLeftWin,
        VkRightWin
    ];

    private static readonly IReadOnlyDictionary<uint, string> KeyNames =
        new Dictionary<uint, string>
        {
            [0x08] = "backspace",
            [0x09] = "tab",
            [0x0D] = "enter",
            [0x10] = "shift",
            [0x11] = "ctrl",
            [0x12] = "alt",
            [0x1B] = "escape",
            [0x20] = "space",
            [0x21] = "pageup",
            [0x22] = "pagedown",
            [0x23] = "end",
            [0x24] = "home",
            [0x25] = "left",
            [0x26] = "up",
            [0x27] = "right",
            [0x28] = "down",
            [0x2D] = "insert",
            [0x2E] = "delete",
            [0x5B] = "win",
            [0x5C] = "win",
            [0xA0] = "shift",
            [0xA1] = "shift",
            [0xA2] = "ctrl",
            [0xA3] = "ctrl",
            [0xA4] = "alt",
            [0xA5] = "alt",
            [0xBA] = ";",
            [0xBB] = "=",
            [0xBC] = ",",
            [0xBD] = "-",
            [0xBE] = ".",
            [0xBF] = "/",
            [0xC0] = "`",
            [0xDB] = "[",
            [0xDC] = "\\",
            [0xDD] = "]",
            [0xDE] = "'"
        };

    private const int DisplayNameTableSize = 256;

    // Resolved once per process: GetDisplayName runs on the WH_KEYBOARD_LL path for every
    // keystroke system-wide, so the common VK range must be a plain table lookup.
    private static readonly string?[] DisplayNameByVk = BuildDisplayNameTable();

    // Cached per HotkeyModifiers combination; GetModifierNames also runs per keystroke/scroll
    // event. Returned arrays are SHARED — callers must treat them as read-only.
    private static readonly string[][] ModifierNamesByFlags = BuildModifierNames();

    private static string?[] BuildDisplayNameTable()
    {
        var names = new string?[DisplayNameTableSize];

        for (var letter = 'A'; letter <= 'Z'; letter++)
        {
            names[letter] = char.ToLowerInvariant(letter).ToString();
        }

        for (var digit = '0'; digit <= '9'; digit++)
        {
            names[digit] = digit.ToString();
        }

        for (var functionKey = 0; functionKey <= 0x17; functionKey++)
        {
            names[0x70 + functionKey] = $"f{functionKey + 1}";
        }

        foreach (var (virtualKey, name) in KeyNames)
        {
            names[virtualKey] = name;
        }

        return names;
    }

    private static string[][] BuildModifierNames()
    {
        var all = HotkeyModifiers.Alt | HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Win;
        var tables = new string[16][];
        for (var flags = 0; flags < tables.Length; flags++)
        {
            var modifiers = (HotkeyModifiers)flags & all;
            var names = new List<string>(4);
            if ((modifiers & HotkeyModifiers.Control) != 0)
            {
                names.Add("ctrl");
            }

            if ((modifiers & HotkeyModifiers.Alt) != 0)
            {
                names.Add("alt");
            }

            if ((modifiers & HotkeyModifiers.Shift) != 0)
            {
                names.Add("shift");
            }

            if ((modifiers & HotkeyModifiers.Win) != 0)
            {
                names.Add("win");
            }

            tables[flags] = [.. names];
        }

        return tables;
    }

    public static bool IsModifierVirtualKey(uint virtualKey)
    {
        return virtualKey switch
        {
            VkShift or VkControl or VkMenu or VkLeftShift or VkRightShift or VkLeftControl or VkRightControl
                or VkLeftMenu or VkRightMenu or VkLeftWin or VkRightWin => true,
            _ => false
        };
    }

    public static HotkeyModifiers ModifierForVirtualKey(uint virtualKey)
    {
        return virtualKey switch
        {
            VkShift or VkLeftShift or VkRightShift => HotkeyModifiers.Shift,
            VkControl or VkLeftControl or VkRightControl => HotkeyModifiers.Control,
            VkMenu or VkLeftMenu or VkRightMenu => HotkeyModifiers.Alt,
            VkLeftWin or VkRightWin => HotkeyModifiers.Win,
            _ => HotkeyModifiers.None
        };
    }

    public static bool IsExtendedVirtualKey(uint virtualKey)
    {
        return virtualKey switch
        {
            0x21 or 0x22 or 0x23 or 0x24 or 0x25 or 0x26 or 0x27 or 0x28 or 0x2D or 0x2E
                or VkRightControl or VkRightMenu or VkLeftWin or VkRightWin => true,
            _ => false
        };
    }

    public static string GetDisplayName(uint virtualKey)
    {
        if (virtualKey < DisplayNameTableSize && DisplayNameByVk[virtualKey] is { } name)
        {
            return name;
        }

        return $"vk:0x{virtualKey:X2}";
    }

    public static IReadOnlyList<uint> GetModifierVirtualKeys(HotkeyModifiers modifiers)
    {
        var virtualKeys = new List<uint>(4);
        if ((modifiers & HotkeyModifiers.Control) != 0)
        {
            virtualKeys.Add(VkControl);
        }

        if ((modifiers & HotkeyModifiers.Alt) != 0)
        {
            virtualKeys.Add(VkMenu);
        }

        if ((modifiers & HotkeyModifiers.Shift) != 0)
        {
            virtualKeys.Add(VkShift);
        }

        if ((modifiers & HotkeyModifiers.Win) != 0)
        {
            virtualKeys.Add(VkLeftWin);
        }

        return virtualKeys;
    }

    /// <summary>
    /// Returns the script-facing modifier names for the given flags in the stable order
    /// ctrl, alt, shift, win. The returned array is cached and shared across calls —
    /// callers must not mutate it.
    /// </summary>
    public static string[] GetModifierNames(HotkeyModifiers modifiers)
    {
        var flags = (uint)modifiers
            & (uint)(HotkeyModifiers.Alt | HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Win);
        return ModifierNamesByFlags[flags];
    }
}
