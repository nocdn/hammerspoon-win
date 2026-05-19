using HammerspoonWin.Core.Applications;

namespace HammerspoonWin.Core.Tests;

public sealed class ProcessNameMatcherTests
{
    [Theory]
    [InlineData("chrome", "chrome")]
    [InlineData("chrome.exe", "chrome")]
    [InlineData("  r5apex.exe  ", "r5apex")]
    [InlineData(@"C:\Games\Apex\r5apex.exe", "r5apex")]
    public void NormalizeAcceptsCommonProcessNameInputs(string input, string expected)
    {
        Assert.Equal(expected, ProcessNameMatcher.Normalize(input));
    }

    [Fact]
    public void NormalizeRejectsEmptyNames()
    {
        Assert.Throws<ArgumentException>(() => ProcessNameMatcher.Normalize(" "));
    }
}
