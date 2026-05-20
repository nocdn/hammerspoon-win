using WpfFontFamily = System.Windows.Media.FontFamily;

namespace HsWin.App;

internal static class ToastFonts
{
    public const string TextFamilyName = "SF Pro Text";
    public const string TextRegularResourcePath = "Assets/Fonts/SF-Pro-Text-Regular.otf";

    public static readonly WpfFontFamily TextFontFamily = CreateTextFontFamily();

    private static WpfFontFamily CreateTextFontFamily()
    {
        var assemblyName = typeof(ToastFonts).Assembly.GetName().Name
            ?? throw new InvalidOperationException("Toast font assembly name is missing.");
        var encodedAssemblyName = Uri.EscapeDataString(assemblyName);
        var source = string.Create(
            null,
            $"pack://application:,,,/{encodedAssemblyName};component/{TextRegularResourcePath}#{TextFamilyName}");
        return new WpfFontFamily(source);
    }
}
