namespace HsWin.Core.Config;

public sealed record HsWinPaths(
    string AppDirectory,
    string ConfigFilePath,
    string RuntimeLogDirectory,
    string ConfigLogDirectory)
{
    public const string AppDirectoryName = "HsWin";

    public static HsWinPaths FromAppData()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
        {
            throw new InvalidOperationException("The current Windows profile does not expose an application data directory.");
        }

        return FromRoot(appData);
    }

    public static HsWinPaths FromRoot(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        var appDirectory = Path.Combine(rootDirectory, AppDirectoryName);
        return new HsWinPaths(
            appDirectory,
            Path.Combine(appDirectory, ConfigFileService.ConfigFileName),
            Path.Combine(appDirectory, "runtime-logs"),
            Path.Combine(appDirectory, "config-logs"));
    }
}
