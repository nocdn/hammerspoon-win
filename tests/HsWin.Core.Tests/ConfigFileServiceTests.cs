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
