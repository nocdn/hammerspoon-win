(() => {
  const host = globalThis.__hswinHost;
  const formatConsoleValue = (value) => {
    if (typeof value === "string") {
      return value;
    }

    if (value instanceof Error) {
      return value.stack || value.message || String(value);
    }

    try {
      const serialized = JSON.stringify(value);
      if (serialized !== undefined) {
        return serialized;
      }
    } catch {
    }

    return String(value);
  };

  const writeConsole = (level, values) => {
    host.Console.Log(level, values.map(formatConsoleValue).join(" "));
  };

  const parseJson = (json) => JSON.parse(json);

  const pasteboard = Object.freeze({
    getContents() {
      return host.Clipboard.GetText();
    },

    setContents(text) {
      return host.Clipboard.SetText(text);
    }
  });

  const createAudioDevice = (device) => Object.freeze({
    ...device,

    getVolume() {
      return parseJson(host.Audio.GetVolumeJson(device.id)).volume;
    },

    setVolume(volume) {
      return parseJson(host.Audio.SetVolumeJson(device.id, volume));
    },

    getMuted() {
      return parseJson(host.Audio.GetVolumeJson(device.id)).muted;
    },

    setMuted(muted) {
      return parseJson(host.Audio.SetMutedJson(device.id, muted));
    },

    toggleMute() {
      return parseJson(host.Audio.ToggleMuteJson(device.id));
    }
  });

  const audiodevice = Object.freeze({
    defaultOutputDevice() {
      return createAudioDevice(parseJson(host.Audio.GetDefaultOutputDeviceJson()));
    },

    allOutputDevices() {
      return parseJson(host.Audio.GetOutputDevicesJson()).map(createAudioDevice);
    },

    getVolume(deviceId) {
      return parseJson(host.Audio.GetVolumeJson(deviceId)).volume;
    },

    setVolume(volume, deviceId) {
      return parseJson(host.Audio.SetVolumeJson(deviceId, volume));
    },

    getMuted(deviceId) {
      return parseJson(host.Audio.GetVolumeJson(deviceId)).muted;
    },

    setMuted(muted, deviceId) {
      return parseJson(host.Audio.SetMutedJson(deviceId, muted));
    },

    toggleMute(deviceId) {
      return parseJson(host.Audio.ToggleMuteJson(deviceId));
    }
  });

  const sound = Object.freeze({
    getVolume() {
      return audiodevice.getVolume();
    },

    setVolume(volume) {
      return audiodevice.setVolume(volume);
    },

    getMuted() {
      return audiodevice.getMuted();
    },

    setMuted(muted) {
      return audiodevice.setMuted(muted);
    },

    toggleMute() {
      return audiodevice.toggleMute();
    }
  });

  globalThis.hs = Object.freeze({
    execute(command, options) {
      return parseJson(host.Shell.ExecuteCommandJson(command, options));
    },

    alert: Object.freeze({
      show(text, optionsOrKind, durationMs) {
        host.Alerts.Show(text, optionsOrKind, durationMs);
      }
    }),

    pasteboard,
    clipboard: pasteboard,

    hotkey: Object.freeze({
      bind(modifiers, key, pressedFn) {
        return host.Hotkeys.Bind(modifiers, key, pressedFn);
      }
    }),

    application: Object.freeze({
      isRunning(processName) {
        return host.Applications.IsRunning(processName);
      },

      runningApplications() {
        return parseJson(host.Applications.GetRunningApplicationsJson());
      },

      launch(target, options) {
        return parseJson(host.Applications.LaunchJson(target, options));
      }
    }),

    media: Object.freeze({
      playPause() {
        return parseJson(host.Media.PlayPauseJson());
      },

      previousTrack() {
        return parseJson(host.Media.PreviousTrackJson());
      },

      nextTrack() {
        return parseJson(host.Media.NextTrackJson());
      }
    }),

    audiodevice,
    sound,

    keyboard: Object.freeze({
      watch(callback, options) {
        if (typeof callback !== "function") {
          throw new Error("Keyboard watch callback must be a function.");
        }

        return host.Keyboard.Watch((eventJson) => callback(parseJson(eventJson)) === true, options);
      },

      tap(key, options) {
        host.Keyboard.Tap(key, options);
      },

      repeat(key, options) {
        return host.Keyboard.Repeat(key, options);
      },

      keyDown(key) {
        host.Keyboard.KeyDown(key);
      },

      keyUp(key) {
        host.Keyboard.KeyUp(key);
      },

      isDown(key) {
        return host.Keyboard.IsDown(key);
      }
    }),

    timer: Object.freeze({
      doAfter(delayMs, callback) {
        if (typeof callback !== "function") {
          throw new Error("Timer callback must be a function.");
        }

        return host.Timers.DoAfter(delayMs, callback);
      },

      doEvery(intervalMs, callback) {
        if (typeof callback !== "function") {
          throw new Error("Timer callback must be a function.");
        }

        return host.Timers.DoEvery(intervalMs, callback);
      }
    })
  });

  globalThis.console = Object.freeze({
    log(...values) {
      writeConsole("log", values);
    },

    info(...values) {
      writeConsole("info", values);
    },

    warn(...values) {
      writeConsole("warn", values);
    },

    error(...values) {
      writeConsole("error", values);
    }
  });
})();
