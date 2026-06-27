using System.Runtime.InteropServices;
using System.IO;

namespace HsWin.App;

internal sealed class CliInstallService
{
    public const string CliFileName = "hspn.exe";

    private const uint WmSettingChange = 0x001A;
    private const uint SmtoAbortIfHung = 0x0002;
    private static readonly nint HwndBroadcast = 0xffff;

    private readonly string _installDirectory;

    public CliInstallService(string installDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installDirectory);

        _installDirectory = Path.GetFullPath(installDirectory);
    }

    public string CliPath => Path.Combine(_installDirectory, CliFileName);

    public bool IsInstalled()
    {
        return PathContainsDirectory(
            Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User),
            _installDirectory);
    }

    public CliInstallResult Install()
    {
        if (!File.Exists(CliPath))
        {
            throw new FileNotFoundException(
                $"The {CliFileName} executable was not found next to the installed app.",
                CliPath);
        }

        var userPath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User);
        if (PathContainsDirectory(userPath, _installDirectory))
        {
            return CliInstallResult.AlreadyInstalled;
        }

        var updatedUserPath = AddDirectoryToPath(userPath, _installDirectory);
        Environment.SetEnvironmentVariable("Path", updatedUserPath, EnvironmentVariableTarget.User);

        var processPath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable(
            "Path",
            AddDirectoryToPath(processPath, _installDirectory),
            EnvironmentVariableTarget.Process);

        BroadcastEnvironmentChange();
        return CliInstallResult.Installed;
    }

    internal static bool PathContainsDirectory(string? pathValue, string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return false;
        }

        var normalizedDirectory = NormalizeDirectory(directory);
        return pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static entry => entry.Trim('"'))
            .Any(entry =>
                TryNormalizeDirectory(entry, out var normalizedEntry)
                && string.Equals(normalizedEntry, normalizedDirectory, StringComparison.OrdinalIgnoreCase));
    }

    internal static string AddDirectoryToPath(string? pathValue, string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (PathContainsDirectory(pathValue, directory))
        {
            return pathValue ?? string.Empty;
        }

        var normalizedDirectory = NormalizeDirectory(directory);
        return string.IsNullOrWhiteSpace(pathValue)
            ? normalizedDirectory
            : $"{pathValue.TrimEnd(Path.PathSeparator)}{Path.PathSeparator}{normalizedDirectory}";
    }

    private static string NormalizeDirectory(string directory)
    {
        var expanded = Environment.ExpandEnvironmentVariables(directory.Trim().Trim('"'));
        return Path.GetFullPath(expanded).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool TryNormalizeDirectory(string directory, out string normalizedDirectory)
    {
        try
        {
            normalizedDirectory = NormalizeDirectory(directory);
            return true;
        }
        catch (ArgumentException)
        {
        }
        catch (NotSupportedException)
        {
        }
        catch (PathTooLongException)
        {
        }

        normalizedDirectory = string.Empty;
        return false;
    }

    private static void BroadcastEnvironmentChange()
    {
        _ = SendMessageTimeout(
            HwndBroadcast,
            WmSettingChange,
            0,
            "Environment",
            SmtoAbortIfHung,
            5000,
            out _);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint SendMessageTimeout(
        nint hWnd,
        uint msg,
        nuint wParam,
        string lParam,
        uint flags,
        uint timeout,
        out nuint result);
}
