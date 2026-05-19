using Microsoft.Win32;

namespace HammerspoonWin.App;

internal sealed class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly string _valueName;
    private readonly string _executablePath;

    public StartupService(string valueName, string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(valueName);
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        _valueName = valueName;
        _executablePath = executablePath;
    }

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        var value = key?.GetValue(_valueName) as string;
        return string.Equals(value, StartupCommand, StringComparison.OrdinalIgnoreCase);
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("Could not open the current user's startup registry key.");

        if (enabled)
        {
            key.SetValue(_valueName, StartupCommand, RegistryValueKind.String);
            return;
        }

        key.DeleteValue(_valueName, throwOnMissingValue: false);
    }

    private string StartupCommand => $"\"{_executablePath}\"";
}
