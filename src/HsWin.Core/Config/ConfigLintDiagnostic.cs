namespace HsWin.Core.Config;

public sealed record ConfigLintDiagnostic(
    ConfigLintSeverity Severity,
    string Code,
    string Message,
    int? Line = null,
    int? Column = null);
