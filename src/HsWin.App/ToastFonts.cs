using WpfFontFamily = System.Windows.Media.FontFamily;

namespace HsWin.App;

internal static class ToastFonts
{
    public const string FamilyName = "Inter";
    public const string RegularResourcePath = "Assets/Fonts/Inter-Regular.ttf";
    public const string MediumResourcePath = "Assets/Fonts/Inter-Medium.ttf";

    public static readonly WpfFontFamily RegularFontFamily = CreateFontFamily(RegularResourcePath);
    public static readonly WpfFontFamily MediumFontFamily = CreateFontFamily(MediumResourcePath);

    private static WpfFontFamily CreateFontFamily(string resourcePath)
    {
        var assemblyName = typeof(ToastFonts).Assembly.GetName().Name
            ?? throw new InvalidOperationException("Toast font assembly name is missing.");
        var encodedAssemblyName = Uri.EscapeDataString(assemblyName);
        var source = string.Create(
            null,
            $"pack://application:,,,/{encodedAssemblyName};component/{resourcePath}#{FamilyName}");
        return new WpfFontFamily(source);
    }
}
