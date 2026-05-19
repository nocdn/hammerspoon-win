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
          intervalMs: 15,
          keyCode: null,
          repeat: null,
          state: "idle",
          sequence: 0,

          stop() {
            this.sequence++;

            const repeat = this.repeat;
            this.repeat = null;
            this.keyCode = null;
            this.state = "idle";

            if (repeat) {
              repeat.stop();
            }
          },

          hasTriggerModifiers(event) {
            return this.modifiers.every(modifier => event.modifiers.includes(modifier));
          },

          start(event) {
            if (this.keyCode !== null && this.keyCode !== event.keyCode) {
              this.stop();
            }

            if (this.state !== "idle") {
              return;
            }

            this.state = "starting";
            this.keyCode = event.keyCode;
            const sequence = ++this.sequence;

            try {
              const repeat = hs.keyboard.repeat(this.keyCode, {
                intervalMs: this.intervalMs,
                suppressPhysicalModifiers: this.modifiers
              });

              if (this.sequence !== sequence || this.state !== "starting") {
                repeat.stop();
                return;
              }

              this.repeat = repeat;
              this.state = "running";
            } catch (error) {
              if (this.sequence === sequence) {
                this.repeat = null;
                this.keyCode = null;
                this.state = "idle";
              }

              throw error;
            }
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

        const apex = {
          processName: "r5apex_dx12.exe",
          isRunning: false,

          refresh() {
            this.isRunning = hs.application.isRunning(this.processName);
            return this.isRunning;
          }
        };

        apex.refresh();
        hs.timer.doEvery(1000, () => apex.refresh());

        hs.hotkey.bind(["ctrl", "alt", "shift"], "F12", () => {
          const isRunning = apex.refresh();

          hs.alert.show(
            isRunning ? "Apex is running" : "Apex is not running",
            { type: isRunning ? "success" : "error", durationMs: 2000 }
          );
        });

        hs.hotkey.bind([], "`", () => {
          if (apex.isRunning) {
            hs.alert.show("Play/Pause", { durationMs: 400 });
            hs.media.playPause();
          }
        });

        hs.hotkey.bind([], "delete", () => {
          if (apex.isRunning) {
            hs.media.previousTrack();
          }
        });

        hs.hotkey.bind([], "pageup", () => {
          if (apex.isRunning) {
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
