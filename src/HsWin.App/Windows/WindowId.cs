using System.Globalization;

namespace HsWin.App.Windows;

internal static class WindowId
{
    public static string Format(IntPtr handle) =>
        $"0x{handle.ToInt64():X}";

    public static bool TryParse(string value, out IntPtr handle)
    {
        handle = IntPtr.Zero;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();
        var style = NumberStyles.Integer;
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            text = text[2..];
            style = NumberStyles.HexNumber;
        }

        if (!long.TryParse(text, style, CultureInfo.InvariantCulture, out var parsed) || parsed == 0)
        {
            return false;
        }

        handle = new IntPtr(parsed);
        return true;
    }
}
