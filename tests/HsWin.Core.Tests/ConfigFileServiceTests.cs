using HsWin.Core.Config;

namespace HsWin.Core.Tests;

public sealed class ConfigFileServiceTests
{
    [Fact]
    public void EnsureConfigFileCreatesDefaultConfigJs()
    {
        using var directory = TemporaryDirectory.Create();
        var configPath = Path.Combine(directory.Path, "config.js");
        var service = new ConfigFileService(configPath);

        service.EnsureConfigFile();

        Assert.True(File.Exists(configPath));
        Assert.Contains("hs.alert.show", File.ReadAllText(configPath), StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultConfigRefreshesApexStateOnPressInsteadOfTimerPolling()
    {
        using var directory = TemporaryDirectory.Create();
        var configPath = Path.Combine(directory.Path, "config.js");
        var service = new ConfigFileService(configPath);

        service.EnsureConfigFile();

        var config = File.ReadAllText(configPath);
        Assert.DoesNotContain("doEvery(1000", config, StringComparison.Ordinal);
        Assert.Contains("if (apex.refresh())", config, StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureConfigFileCreatesHardenedTurboRepeatState()
    {
        using var directory = TemporaryDirectory.Create();
        var configPath = Path.Combine(directory.Path, "config.js");
        var service = new ConfigFileService(configPath);

        service.EnsureConfigFile();

        var config = File.ReadAllText(configPath);
        Assert.Contains("intervalMs: 15", config, StringComparison.Ordinal);
        Assert.Contains("state: \"idle\"", config, StringComparison.Ordinal);
        Assert.Contains("this.state = \"starting\";", config, StringComparison.Ordinal);
        Assert.Contains("this.state = \"running\";", config, StringComparison.Ordinal);
        Assert.Contains("const sequence = ++this.sequence;", config, StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureConfigFileDoesNotOverwriteExistingConfig()
    {
        using var directory = TemporaryDirectory.Create();
        var configPath = Path.Combine(directory.Path, "config.js");
        const string customConfig = "hs.alert.show('Custom');";
        File.WriteAllText(configPath, customConfig);
        var service = new ConfigFileService(configPath);

        service.EnsureConfigFile();

        Assert.Equal(customConfig, File.ReadAllText(configPath));
    }
}
