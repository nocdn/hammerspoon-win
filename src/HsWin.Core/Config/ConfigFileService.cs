namespace HsWin.Core.Config;

public sealed class ConfigFileService
{
    public const string ConfigFileName = "config.js";

    public const string DefaultConfig = """
        // Hammerspoon (Windows Edition) config
        // Edit this file, then use the tray menu to reload it.

        console.log("Reloading config");
        hs.hotkey.bind(["ctrl", "alt"], "R", () => {
          const isRunning = hs.application.isRunning("TOTClient-Win64-Shipping.exe");

          hs.alert.show(
            isRunning ? "Outlast Trials is running" : "Outlast Trials is not running",
            { type: isRunning ? "success" : "error", durationMs: 2000 }
          );
        });

        const outlastProcessName = "TOTClient-Win64-Shipping.exe";

        hs.hotkey.bind([], "`", () => {
          if (hs.application.isRunning(outlastProcessName)) {
            const result = hs.media.playPause();
            const text = result.action === "played"
              ? "Played"
              : result.action === "paused"
                ? "Paused"
                : "Played/Paused";

            hs.alert.show(text, { durationMs: 400 });
          }
        });

        hs.hotkey.bind([], "delete", () => {
          if (hs.application.isRunning(outlastProcessName)) {
            hs.media.previousTrack();
          }
        });

        hs.hotkey.bind([], "pageup", () => {
          if (hs.application.isRunning(outlastProcessName)) {
            hs.media.nextTrack();
          }
        });

        // Other examples:
        // console.log("Any values you want to inspect", { hello: "world" });
        // hs.alert.show("Plain message", { type: "normal", durationMs: 1500 });
        // console.log(hs.application.runningApplications());
        // hs.hotkey.bind(["ctrl", "alt"], "mouse.middle", () => hs.alert.show("Middle mouse"));
        // hs.hotkey.bind([], "mouse.back", () => hs.alert.show("Thumb back"));
        // hs.hotkey.bind([], "mouse.forward", () => hs.alert.show("Thumb forward"));
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
