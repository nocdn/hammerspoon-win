using HammerspoonWin.Core.Logging;

namespace HammerspoonWin.Core.Tests;

public sealed class ReloadScriptConsoleLoggerTests
{
    [Fact]
    public void BeginReloadCreatesNewLogFile()
    {
        using var directory = TemporaryDirectory.Create();
        var timestamp = new DateTimeOffset(2026, 5, 19, 13, 47, 0, TimeSpan.Zero);
        var logger = new ReloadScriptConsoleLogger(directory.Path, () => timestamp);

        logger.BeginReload("config.js");

        Assert.Equal("05-19-2026-13-47.log", Path.GetFileName(logger.CurrentLogFilePath));
        Assert.True(File.Exists(logger.CurrentLogFilePath));
    }

    [Fact]
    public void BeginReloadRotatesToNewFile()
    {
        using var directory = TemporaryDirectory.Create();
        var timestamp = new DateTimeOffset(2026, 5, 19, 13, 47, 0, TimeSpan.Zero);
        var logger = new ReloadScriptConsoleLogger(directory.Path, () => timestamp);

        logger.BeginReload("config.js");
        var firstPath = logger.CurrentLogFilePath;
        logger.BeginReload("config.js");
        var secondPath = logger.CurrentLogFilePath;

        Assert.NotEqual(firstPath, secondPath);
        Assert.Equal("05-19-2026-13-47-2.log", Path.GetFileName(secondPath));
    }

    [Fact]
    public void WriteAppendsToCurrentReloadLog()
    {
        using var directory = TemporaryDirectory.Create();
        var logger = new ReloadScriptConsoleLogger(directory.Path, () => new DateTimeOffset(2026, 5, 19, 13, 47, 0, TimeSpan.Zero));

        logger.BeginReload("config.js");
        logger.Write("log", "hello world");

        var contents = File.ReadAllText(logger.CurrentLogFilePath!);
        Assert.Contains("[log] hello world", contents, StringComparison.Ordinal);
    }
}
