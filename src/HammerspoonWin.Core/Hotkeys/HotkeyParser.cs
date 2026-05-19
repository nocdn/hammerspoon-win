using System.Collections;
using System.Globalization;
using Microsoft.ClearScript;

namespace HammerspoonWin.Core.Hotkeys;

public static class HotkeyParser
{
    private static readonly IReadOnlyDictionary<string, HotkeyModifiers> ModifierAliases =
        new Dictionary<string, HotkeyModifiers>(StringComparer.OrdinalIgnoreCase)
        {
            ["alt"] = HotkeyModifiers.Alt,
            ["option"] = HotkeyModifiers.Alt,
            ["opt"] = HotkeyModifiers.Alt,
            ["ctrl"] = HotkeyModifiers.Control,
            ["control"] = HotkeyModifiers.Control,
            ["shift"] = HotkeyModifiers.Shift,
            ["cmd"] = HotkeyModifiers.Win,
            ["command"] = HotkeyModifiers.Win,
            ["win"] = HotkeyModifiers.Win,
            ["windows"] = HotkeyModifiers.Win,
            ["meta"] = HotkeyModifiers.Win
        };

    private static readonly IReadOnlyDictionary<string, uint> NamedKeys =
        new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase)
        {
            ["backspace"] = 0x08,
            ["delete"] = 0x2E,
            ["del"] = 0x2E,
            ["tab"] = 0x09,
            ["enter"] = 0x0D,
            ["return"] = 0x0D,
            ["escape"] = 0x1B,
            ["esc"] = 0x1B,
            ["space"] = 0x20,
            ["pageup"] = 0x21,
            ["pagedown"] = 0x22,
            ["home"] = 0x24,
            ["end"] = 0x23,
            ["left"] = 0x25,
            ["up"] = 0x26,
            ["right"] = 0x27,
            ["down"] = 0x28,
            ["insert"] = 0x2D,
            ["ins"] = 0x2D,
            ["plus"] = 0xBB,
            ["minus"] = 0xBD,
            ["comma"] = 0xBC,
            ["period"] = 0xBE,
            ["dot"] = 0xBE,
            ["slash"] = 0xBF,
            ["semicolon"] = 0xBA,
            ["quote"] = 0xDE,
            ["backquote"] = 0xC0,
            ["grave"] = 0xC0,
            ["leftbracket"] = 0xDB,
            ["rightbracket"] = 0xDD,
            ["backslash"] = 0xDC
        };

    private static readonly IReadOnlyDictionary<string, HotkeyMouseButton> MouseButtonAliases =
        new Dictionary<string, HotkeyMouseButton>(StringComparer.OrdinalIgnoreCase)
        {
            ["middle"] = HotkeyMouseButton.Middle,
            ["middlemouse"] = HotkeyMouseButton.Middle,
            ["mouse.middle"] = HotkeyMouseButton.Middle,
            ["mouse.button3"] = HotkeyMouseButton.Middle,
            ["mousebutton3"] = HotkeyMouseButton.Middle,
            ["button3"] = HotkeyMouseButton.Middle,

            ["back"] = HotkeyMouseButton.XButton1,
            ["backward"] = HotkeyMouseButton.XButton1,
            ["thumb1"] = HotkeyMouseButton.XButton1,
            ["xbutton1"] = HotkeyMouseButton.XButton1,
            ["mouse.back"] = HotkeyMouseButton.XButton1,
            ["mouse.backward"] = HotkeyMouseButton.XButton1,
            ["mouse.xbutton1"] = HotkeyMouseButton.XButton1,
            ["mouse.button4"] = HotkeyMouseButton.XButton1,
            ["mousebutton4"] = HotkeyMouseButton.XButton1,
            ["button4"] = HotkeyMouseButton.XButton1,

            ["forward"] = HotkeyMouseButton.XButton2,
            ["thumb2"] = HotkeyMouseButton.XButton2,
            ["xbutton2"] = HotkeyMouseButton.XButton2,
            ["mouse.forward"] = HotkeyMouseButton.XButton2,
            ["mouse.xbutton2"] = HotkeyMouseButton.XButton2,
            ["mouse.button5"] = HotkeyMouseButton.XButton2,
            ["mousebutton5"] = HotkeyMouseButton.XButton2,
            ["button5"] = HotkeyMouseButton.XButton2
        };

    private static readonly IReadOnlyDictionary<char, uint> SingleCharacterKeys =
        new Dictionary<char, uint>
        {
            ['`'] = 0xC0,
            ['~'] = 0xC0,
            ['-'] = 0xBD,
            ['='] = 0xBB,
            [','] = 0xBC,
            ['.'] = 0xBE,
            ['/'] = 0xBF,
            [';'] = 0xBA,
            ['\''] = 0xDE,
            ['['] = 0xDB,
            [']'] = 0xDD,
            ['\\'] = 0xDC
        };

    public static HotkeyDefinition Parse(object? modifiers, object? key)
    {
        var parsedModifiers = ParseModifiers(modifiers);
        if (TryParseMouseButton(key, out var mouseButton))
        {
            return HotkeyDefinition.CreateMouseButton(parsedModifiers, mouseButton);
        }

        return HotkeyDefinition.CreateKeyboard(parsedModifiers, ParseVirtualKey(key));
    }

    public static HotkeyModifiers ParseModifiers(object? value)
    {
        var modifiers = HotkeyModifiers.None;

        foreach (var item in EnumerateModifierValues(value))
        {
            var name = Convert.ToString(item, CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (!ModifierAliases.TryGetValue(name.Trim(), out var modifier))
            {
                throw new ArgumentException($"Unsupported hotkey modifier '{name}'.", nameof(value));
            }

            modifiers |= modifier;
        }

        return modifiers;
    }

    public static uint ParseVirtualKey(object? value)
    {
        if (value is null || ReferenceEquals(value, Undefined.Value))
        {
            throw new ArgumentException("Hotkey key is required.", nameof(value));
        }

        if (value is int intValue)
        {
            return CheckedVirtualKey(intValue);
        }

        if (value is uint uintValue)
        {
            return CheckedVirtualKey(uintValue);
        }

        var key = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Hotkey key cannot be empty.", nameof(value));
        }

        if (TryParseMouseButtonName(key, out _))
        {
            throw new ArgumentException($"Mouse hotkey key '{key}' does not have a virtual-key code.", nameof(value));
        }

        if (key.Length == 1)
        {
            var character = char.ToUpperInvariant(key[0]);
            if (character is >= 'A' and <= 'Z')
            {
                return character;
            }

            if (character is >= '0' and <= '9')
            {
                return character;
            }

            if (SingleCharacterKeys.TryGetValue(key[0], out var singleCharacterVirtualKey))
            {
                return singleCharacterVirtualKey;
            }
        }

        if (key.StartsWith('F') && int.TryParse(key[1..], NumberStyles.None, CultureInfo.InvariantCulture, out var functionKey))
        {
            if (functionKey is >= 1 and <= 24)
            {
                return (uint)(0x70 + functionKey - 1);
            }
        }

        if (NamedKeys.TryGetValue(NormalizeKeyName(key), out var virtualKey))
        {
            return virtualKey;
        }

        throw new ArgumentException($"Unsupported hotkey key '{key}'.", nameof(value));
    }

    private static bool TryParseMouseButton(object? value, out HotkeyMouseButton mouseButton)
    {
        mouseButton = default;

        if (value is null || ReferenceEquals(value, Undefined.Value))
        {
            return false;
        }

        if (value is int or uint)
        {
            return false;
        }

        var key = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim();
        return !string.IsNullOrWhiteSpace(key) && TryParseMouseButtonName(key, out mouseButton);
    }

    private static bool TryParseMouseButtonName(string key, out HotkeyMouseButton mouseButton)
    {
        return MouseButtonAliases.TryGetValue(NormalizeMouseButtonName(key), out mouseButton);
    }

    private static IEnumerable<object?> EnumerateModifierValues(object? value)
    {
        if (value is null || ReferenceEquals(value, Undefined.Value))
        {
            yield break;
        }

        if (value is string singleModifier)
        {
            yield return singleModifier;
            yield break;
        }

        if (value is ScriptObject scriptObject)
        {
            foreach (var index in scriptObject.PropertyIndices.Order())
            {
                yield return scriptObject.GetProperty(index);
            }

            yield break;
        }

        if (value is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                yield return item;
            }

            yield break;
        }

        throw new ArgumentException("Hotkey modifiers must be a string or an array.", nameof(value));
    }

    private static uint CheckedVirtualKey(int value)
    {
        if (value is < 0 or > 0xFF)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Virtual-key code must fit in one byte.");
        }

        return (uint)value;
    }

    private static uint CheckedVirtualKey(uint value)
    {
        if (value > 0xFF)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Virtual-key code must fit in one byte.");
        }

        return value;
    }

    private static string NormalizeKeyName(string value)
    {
        return value
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();
    }

    private static string NormalizeMouseButtonName(string value)
    {
        return NormalizeKeyName(value);
    }
}
