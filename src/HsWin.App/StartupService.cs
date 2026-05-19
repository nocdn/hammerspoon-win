using Microsoft.Win32;

namespace HsWin.App;

internal sealed class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly string _valueName;
    private readonly string _executablePath;
    private readonly IReadOnlyList<string> _legacyValueNames;

    public StartupService(string valueName, string executablePath, params string[] legacyValueNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(valueName);
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        _valueName = valueName;
        _executablePath = executablePath;
        _legacyValueNames = legacyValueNames;
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
            DeleteLegacyValues(key);
            key.SetValue(_valueName, StartupCommand, RegistryValueKind.String);
            return;
        }

        key.DeleteValue(_valueName, throwOnMissingValue: false);
        DeleteLegacyValues(key);
    }

    private void DeleteLegacyValues(RegistryKey key)
    {
        foreach (var legacyValueName in _legacyValueNames)
        {
            key.DeleteValue(legacyValueName, throwOnMissingValue: false);
        }
    }

    private string StartupCommand => $"\"{_executablePath}\"";
}
