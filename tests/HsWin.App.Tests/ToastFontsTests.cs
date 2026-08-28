namespace HsWin.App.Tests;

public sealed class ToastFontsTests
{
    [Fact]
    public void ToastTextFontUsesEmbeddedSfProTextRegular()
    {
        Assert.Equal("SF Pro Text", ToastFonts.TextFamilyName);
        Assert.Contains(
            ToastFonts.TextRegularResourcePath,
            ToastFonts.TextFontFamily.Source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void FollowingToastFontUsesEmbeddedSfProRoundedMedium()
    {
        Assert.Equal("SF Pro Rounded", ToastFonts.RoundedFamilyName);
        Assert.Contains(
            ToastFonts.RoundedMediumResourcePath,
            ToastFonts.RoundedMediumFontFamily.Source,
            StringComparison.Ordinal);
    }
}
