using WpfFontFamily = System.Windows.Media.FontFamily;

namespace HsWin.App;

internal static class ToastFonts
{
    public const string TextFamilyName = "SF Pro Text";
    public const string TextRegularResourcePath = "Assets/Fonts/SF-Pro-Text-Regular.otf";
    public const string RoundedFamilyName = "SF Pro Rounded";
    public const string RoundedMediumResourcePath = "Assets/Fonts/SF-Pro-Rounded-Medium.otf";

    public static readonly WpfFontFamily TextFontFamily = CreateTextFontFamily();
    public static readonly WpfFontFamily RoundedMediumFontFamily = CreateFontFamily(
        RoundedMediumResourcePath,
        RoundedFamilyName);

    private static WpfFontFamily CreateTextFontFamily()
    {
        return CreateFontFamily(TextRegularResourcePath, TextFamilyName);
    }

    private static WpfFontFamily CreateFontFamily(string resourcePath, string familyName)
    {
        var assemblyName = typeof(ToastFonts).Assembly.GetName().Name
            ?? throw new InvalidOperationException("Toast font assembly name is missing.");
        var encodedAssemblyName = Uri.EscapeDataString(assemblyName);
        var source = string.Create(
            null,
            $"pack://application:,,,/{encodedAssemblyName};component/{resourcePath}#{familyName}");
        return new WpfFontFamily(source);
    }
}
