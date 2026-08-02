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

    [Fact]
    public void LintSourceRejectsModifierTapRepeatInsideCallback()
    {
        var result = new ConfigLinter().LintSource("""
            hs.hotkey.bind(["ctrl"], "A", () => {
              hs.keyboard.repeat("shift", { intervalMs: 120 });
            });
            """);

        var diagnostic = Assert.Single(result.Diagnostics, item => item.Code == "HSWIN002");
        Assert.Equal(ConfigLintSeverity.Error, diagnostic.Severity);
        Assert.Equal(2, diagnostic.Line);
        Assert.Contains("repeatPulse", diagnostic.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""hs.keyboard.repeatPulse("shift", { intervalMs: 120 });""")]
    [InlineData("""hs.keyboard.repeatPulse("shift", { intervalMs: 120, keyDownMs: 0 });""")]
    public void LintSourceRejectsPulseWithoutPositiveLiteralDuration(string source)
    {
        var result = new ConfigLinter().LintSource(source);

        var diagnostic = Assert.Single(result.Diagnostics, item => item.Code == "HSWIN003");
        Assert.Equal(ConfigLintSeverity.Error, diagnostic.Severity);
        Assert.Contains("keyDownMs", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LintSourceAllowsDynamicPositivePulseDuration()
    {
        var result = new ConfigLinter().LintSource("""
            const pulse = { keyDownMs: 60 };
            hs.hotkey.bind(["ctrl"], "A", () => {
              hs.keyboard.repeatPulse("shift", {
                intervalMs: 120,
                keyDownMs: pulse.keyDownMs
              });
            });
            """);

        Assert.DoesNotContain(result.Diagnostics, item => item.Code is "HSWIN002" or "HSWIN003");
    }

    [Fact]
    public void LintSourceIgnoresKeyboardRepeatTextInStringsAndComments()
    {
        var result = new ConfigLinter().LintSource("""
            console.log("hs.keyboard.repeat('shift', { intervalMs: 10 })");
            // hs.keyboard.repeat("ctrl", { intervalMs: 10 });
            /* hs.keyboard.repeatPulse("shift", { intervalMs: 120 }); */
            """);

        Assert.DoesNotContain(result.Diagnostics, item => item.Code is "HSWIN002" or "HSWIN003");
    }
}
