namespace HsWin.App.Tests;

public sealed class ToastFontsTests
{
    [Fact]
    public void ToastTextFontUsesEmbeddedInterRegular()
    {
        Assert.Equal("Inter", ToastFonts.FamilyName);
        Assert.Contains(
            ToastFonts.RegularResourcePath,
            ToastFonts.RegularFontFamily.Source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void FollowingToastFontUsesEmbeddedInterMedium()
    {
        Assert.Equal("Inter", ToastFonts.FamilyName);
        Assert.Contains(
            ToastFonts.MediumResourcePath,
            ToastFonts.MediumFontFamily.Source,
            StringComparison.Ordinal);
    }
}
