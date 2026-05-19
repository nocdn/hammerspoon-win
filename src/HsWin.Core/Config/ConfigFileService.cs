namespace HsWin.Core.Config;

public sealed class ConfigFileService
{
    public const string ConfigFileName = "config.js";

    public const string DefaultConfig = """
        // Hammerspoon (Windows Edition) config
        // Edit this file, then use the tray menu to reload it.

        console.log("Reloading config");

        const turboRepeat = {
          modifiers: ["alt", "shift"],
          intervalMs: 5,
          keyCode: null,
          repeat: null,

          stop() {
            if (this.repeat) {
              this.repeat.stop();
              this.repeat = null;
            }

            this.keyCode = null;
          },

          hasTriggerModifiers(event) {
            return this.modifiers.every(modifier => event.modifiers.includes(modifier));
          },

          start(event) {
            if (this.keyCode !== null && this.keyCode !== event.keyCode) {
              this.stop();
            }

            if (this.repeat) {
              return;
            }

            this.keyCode = event.keyCode;
            this.repeat = hs.keyboard.repeat(this.keyCode, {
              intervalMs: this.intervalMs,
              suppressPhysicalModifiers: this.modifiers
            });
          }
        };

        hs.keyboard.watch(event => {
          if (event.isModifier) {
            if (!turboRepeat.hasTriggerModifiers(event)) {
              turboRepeat.stop();
            }

            return false;
          }

          if (event.type === "keydown" && turboRepeat.hasTriggerModifiers(event)) {
            turboRepeat.start(event);
            return true;
          }

          if (event.type === "keyup" && event.keyCode === turboRepeat.keyCode) {
            turboRepeat.stop();
            return true;
          }

          return false;
        });

        hs.hotkey.bind(["ctrl", "alt"], "R", () => {
          const isRunning = hs.application.isRunning("r5apex_dx12.exe");

          hs.alert.show(
            isRunning ? "Apex is running" : "Apex is not running",
            { type: isRunning ? "success" : "error", durationMs: 2000 }
          );
        });

        const apexProcessName = "r5apex_dx12.exe";

        hs.hotkey.bind([], "`", () => {
          if (hs.application.isRunning(apexProcessName)) {
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
          if (hs.application.isRunning(apexProcessName)) {
            hs.media.previousTrack();
          }
        });

        hs.hotkey.bind([], "pageup", () => {
          if (hs.application.isRunning(apexProcessName)) {
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
