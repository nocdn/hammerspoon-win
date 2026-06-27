using HsWin.Core.Config;

namespace HsWin.Core.Tests;

public sealed class ConfigLinterTests
{
    [Fact]
    public void LintSourceAllowsDefaultConfig()
    {
        var result = new ConfigLinter().LintSource(ConfigFileService.DefaultConfig);

        Assert.True(result.Success);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void LintSourceCatchesLiteralZeroTimerInsideCallback()
    {
        var result = new ConfigLinter().LintSource("""
            hs.hotkey.bind(["ctrl"], "A", () => {
              hs.timer.doAfter(0, () => {});
            });
            """);

        var diagnostic = Assert.Single(result.Diagnostics, item => item.Code == "HSWIN001");
        Assert.Equal(ConfigLintSeverity.Error, diagnostic.Severity);
        Assert.Equal(2, diagnostic.Line);
        Assert.Contains("at least 1 millisecond", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LintSourceIgnoresTimerTextInStringsAndComments()
    {
        var result = new ConfigLinter().LintSource("""
            console.log("hs.timer.doAfter(0, () => {})");
            // hs.timer.doEvery(0, () => {});
            /* hs.timer.doAfter(0, () => {}); */
            hs.timer.doAfter(1, () => {});
            """);

        Assert.True(result.Success);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void LintSourceCatchesScriptApiValidationErrors()
    {
        var result = new ConfigLinter().LintSource("""hs.hotkey.bind(["hyper"], "A", () => {});""");

        var diagnostic = Assert.Single(result.Diagnostics, item => item.Code == "HSWIN100");
        Assert.Equal(ConfigLintSeverity.Error, diagnostic.Severity);
        Assert.Contains("Unsupported hotkey modifier", diagnostic.Message, StringComparison.Ordinal);
    }
}
