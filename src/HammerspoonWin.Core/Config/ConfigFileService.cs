namespace HammerspoonWin.Core.Config;

public sealed class ConfigFileService
{
    public const string ConfigFileName = "config.js";

    public const string DefaultConfig = """
        // HammerspoonWin config
        // Edit this file, then use the tray menu to reload it.

        console.log("Reloading config");
        hs.alert.show("HammerspoonWin config loaded");

        // Examples:
        // console.log("Any values you want to inspect", { hello: "world" });
        // hs.alert.show("Saved", { type: "success", durationMs: 2000 });
        // hs.alert.show("Something failed", { type: "error", durationMs: 4000 });
        // hs.alert.show("Plain message", { type: "normal", durationMs: 1500 });
        // hs.hotkey.bind(["ctrl", "alt"], "R", () => hs.alert.show("Hotkey pressed"));
        """;

    public ConfigFileService(string configFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configFilePath);
        ConfigFilePath = configFilePath;
    }

    public string ConfigFilePath { get; }

    public string ConfigDirectory => Path.GetDirectoryName(ConfigFilePath)
        ?? throw new InvalidOperationException("The config path must have a parent directory.");

    public void EnsureConfigFile()
    {
        Directory.CreateDirectory(ConfigDirectory);

        if (!File.Exists(ConfigFilePath))
        {
            File.WriteAllText(ConfigFilePath, DefaultConfig);
        }
    }
}
