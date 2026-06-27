namespace HsWin.Core.Config;

public sealed class ConfigLintResult
{
    public ConfigLintResult(IReadOnlyList<ConfigLintDiagnostic> diagnostics)
    {
        Diagnostics = diagnostics;
    }

    public IReadOnlyList<ConfigLintDiagnostic> Diagnostics { get; }

    public int ErrorCount => Diagnostics.Count(static diagnostic => diagnostic.Severity == ConfigLintSeverity.Error);

    public int WarningCount => Diagnostics.Count(static diagnostic => diagnostic.Severity == ConfigLintSeverity.Warning);

    public bool Success => ErrorCount == 0;
}
