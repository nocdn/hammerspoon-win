# Hammerspoon (Windows Edition)

Hammerspoon (Windows Edition) is a tray-first Windows automation host inspired by Hammerspoon. It starts without a main window, loads JavaScript from `%APPDATA%\HsWin\config.js`, and exposes a frozen `hs` global plus `console` logging.

Reload the config from the tray menu at any time. Each reload starts a fresh JavaScript engine, disposes previous hotkey bindings, keyboard watchers, and timers, rotates the config console log file, and shows a `Config reloaded` success toast when it completes.

## Current API

### `hs.alert.show(text, optionsOrKind?, durationMs?)`

Shows a toast notification.

```js
hs.alert.show("Config loaded");
hs.alert.show("Saved", "success", 2000);
hs.alert.show("Something failed", { type: "error", durationMs: 4000 });
hs.alert.show("Plain message", { type: "normal", durationMs: 1500 });
```

Defaults when omitted: `type` is `success`, `durationMs` is `2000`. Types are `normal`, `success`, and `error`; aliases include `ok`, `done`, `plain`, `info`, `failure`, and `failed`.

The first visible toast creates the toast window lazily; later toasts reuse the hidden window so repeated hotkey feedback stays lightweight and responsive.

### `console.log`, `console.info`, `console.warn`, `console.error`

Writes to `%APPDATA%\HsWin\config-logs\MM-dd-yyyy-HH-mm.log`. Objects are JSON-serialized; `Error` values use their stack or message.

```js
console.log("Reloading config");
console.warn("Deprecated binding");
console.error(new Error("Something went wrong"));
console.log("Apps", hs.application.runningApplications());
```

### `hs.pasteboard.getContents()`, `hs.pasteboard.setContents(text)`, `hs.clipboard`

Gets or sets Unicode text on the Windows clipboard. `hs.clipboard` is an alias for `hs.pasteboard`.

```js
const previous = hs.pasteboard.getContents();
hs.clipboard.setContents(`${previous}\nUpdated by HsWin`);
```

`getContents()` returns an empty string when the clipboard does not currently contain text. `setContents(text)` returns `true` after the clipboard is updated.

### `hs.execute(command, options?)`

Runs a command through `cmd.exe /S /C` and returns a result object.

```js
const result = hs.execute("git status --short", {
  cwd: "C:\\Users\\me\\project",
  timeoutMs: 5000
});

if (result.success) {
  console.log(result.output);
} else {
  console.error(result.error);
}
```

Options are `cwd`/`workingDirectory` and `timeoutMs`/`timeout`; the default timeout is 30000 ms. The result has `command`, `success`, `status`, `exitCode`, `output`, `error`, and `timedOut`.

### `hs.hotkey.bind(modifiers, key, callback)`

Registers a global hotkey or mouse-button binding. Bindings from a previous config reload are disposed automatically.

```js
hs.hotkey.bind(["ctrl", "alt"], "R", () => hs.alert.show("Hotkey pressed"));
hs.hotkey.bind([], "`", () => hs.media.playPause());
hs.hotkey.bind(["ctrl", "alt"], "mouse.middle", () => hs.alert.show("Middle mouse"));
```

Supported modifiers are `alt`, `option`, `opt`, `ctrl`, `control`, `shift`, `cmd`, `command`, `win`, `windows`, and `meta`.

`key` is a letter (`A`-`Z`), digit (`0`-`9`), function key (`F1`-`F24`), a named key, or a mouse button. Named keyboard keys include `backspace`, `delete`, `tab`, `enter`, `escape`, `space`, `pageup`, `pagedown`, `home`, `end`, arrows, `insert`, punctuation names, and punctuation literals. Mouse button keys include `mouse.middle`, `mouse.back`, and `mouse.forward` plus common aliases.

If a hotkey callback throws, the runtime shows an error toast instead of crashing the host.

### `hs.application.isRunning(processName)`

Returns whether a process with the given name is running. The name may include or omit the `.exe` extension; matching is case-insensitive and compares the executable file name.

```js
if (hs.application.isRunning("r5apex_dx12.exe")) {
  hs.media.playPause();
}
```

### `hs.application.runningApplications()`

Returns an array of running application snapshots:

```js
for (const app of hs.application.runningApplications()) {
  console.log(app.pid, app.processName, app.mainWindowTitle, app.path);
}
```

Each item has `pid`, `processName`, `mainWindowTitle`, and `path`.

### `hs.application.launch(target, options?)`

Opens a URL, document, executable, or registered app target through the Windows shell.

```js
if (!hs.application.isRunning("notepad.exe")) {
  hs.application.launch("notepad.exe");
}

hs.application.launch("https://www.hammerspoon.org/");
```

Options are `cwd`/`workingDirectory` and `arguments`/`args`. The result has `target`, `success`, `processId`, and `error`; `processId` can be `null` for shell-handled targets such as URLs.

### `hs.media.playPause()`, `hs.media.previousTrack()`, `hs.media.nextTrack()`

Controls the current Windows media session when one is available, otherwise sends the corresponding media key.

```js
const result = hs.media.playPause();
hs.alert.show(result.action === "played" ? "Played" : "Paused", { durationMs: 400 });

hs.media.previousTrack();
hs.media.nextTrack();
```

Each call returns `command`, `success`, `action`, `statusBefore`, `statusAfter`, and `backend`. `playPause` actions are `played`, `paused`, `toggled`, or `playPause`; track actions are `previousTrack` and `nextTrack`.

### `hs.audiodevice`, `hs.sound`

Reads and updates Windows output device volume and mute state. `hs.sound` is a simple default-output shortcut; `hs.audiodevice` can target a specific output device.

```js
const output = hs.audiodevice.defaultOutputDevice();
console.log(output.name, output.volume, output.muted);

output.setVolume(35);
output.setMuted(false);

for (const device of hs.audiodevice.allOutputDevices()) {
  console.log(device.id, device.name, device.volume, device.muted);
}

hs.sound.setVolume(20);
hs.sound.toggleMute();
```

Device objects have `id`, `name`, `isDefault`, `volume`, `muted`, plus `getVolume()`, `setVolume(volume)`, `getMuted()`, `setMuted(muted)`, and `toggleMute()`. `hs.audiodevice.getVolume(deviceId?)`, `setVolume(volume, deviceId?)`, `getMuted(deviceId?)`, `setMuted(muted, deviceId?)`, and `toggleMute(deviceId?)` use the default output device when `deviceId` is omitted. Volume is 0-100.

### `hs.keyboard.watch(callback, options?)`

Subscribes to global keyboard events. The callback receives a plain JavaScript object and may return `true` to swallow the physical event so the active app does not receive it.

```js
const watcher = hs.keyboard.watch(event => {
  if (event.type === "keydown" && event.key === "w" && event.modifiers.includes("alt")) {
    hs.alert.show("Alt+W");
    return true;
  }

  return false;
});

watcher.stop();
```

The implementation uses `WH_KEYBOARD_LL` for global key events and blocking. Injected events are ignored by default to avoid feedback loops when scripts call `hs.keyboard.tap`.

Event fields are `type`, `keyCode`, `key`, `modifiers`, `modifierFlags`, `isKeyDown`, `isKeyUp`, `isModifier`, `isInjected`, and `isExtended`.

Options:

| Option | Default | Description |
| --- | --- | --- |
| `includeInjected` | `false` | When `true`, callbacks also receive synthetic events. |

### `hs.keyboard.tap(key, options?)`, `hs.keyboard.repeat(key, options?)`, `hs.keyboard.keyDown(key)`, `hs.keyboard.keyUp(key)`, `hs.keyboard.isDown(key)`

Sends or queries keyboard input. `key` accepts the same keyboard key names as `hs.hotkey.bind`, or a numeric virtual-key code.

```js
hs.keyboard.tap("w");
const repeat = hs.keyboard.repeat("w", { intervalMs: 5 });
repeat.stop();
hs.keyboard.keyDown("shift");
hs.keyboard.keyUp("shift");

if (hs.keyboard.isDown("alt")) {
  console.log("Alt is physically down");
}
```

`tap` uses `SendInput`, which is the supported Win32 input injection API. To use modifiers as a trigger chord while sending a plain key, temporarily suppress them around the tap:

```js
hs.keyboard.tap(event.keyCode, { suppressPhysicalModifiers: ["alt", "shift"] });
```

Aliases for `suppressPhysicalModifiers` are `suppressModifiers` and `withoutModifiers`.

`repeat` runs the tap loop natively and logs start/stop performance summaries including the effective interval. It is much faster than implementing a high-frequency repeat loop in JavaScript with `hs.timer.doEvery`.

### `hs.timer.doAfter(delayMs, callback)`, `hs.timer.doEvery(intervalMs, callback)`

Runs JavaScript callbacks later or repeatedly on the app dispatcher thread. Returned handles support `stop()`, `dispose()`, and `delete()`; config reload stops active timers automatically.

```js
hs.timer.doAfter(500, () => hs.alert.show("Half a second later"));

const timer = hs.timer.doEvery(35, () => {
  hs.keyboard.tap("w");
});

timer.stop();
```

### Example config

The default `config.js` created on first run combines application checks, alerts, hotkeys, media controls, and a user-scripted Alt+Shift turbo-repeat:

```js
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
    const text =
      result.action === "played" ? "Played" :
      result.action === "paused" ? "Paused" :
      "Played/Paused";
    hs.alert.show(text, { durationMs: 400 });
  }
});
```

## Projects

- `src/HsWin.App`: WPF tray application, toast window, native hotkeys, keyboard hook, input injection, timers, clipboard, shell launching, audio/media control, startup integration, editor launching.
- `src/HsWin.Core`: config file creation, alert contracts, service contracts, ClearScript runtime, and script-facing API wiring.
- `tests/HsWin.Core.Tests`: parser, config, and JavaScript bridge tests.
- `tests/HsWin.App.Tests`: WPF-side constants and app-layer behavior that can be tested without fragile screenshots.

## Logs

- Runtime diagnostics: `%APPDATA%\HsWin\runtime-logs\MM-dd-yyyy-HH-mm.log`
- JavaScript console output: `%APPDATA%\HsWin\config-logs\MM-dd-yyyy-HH-mm.log`

The runtime diagnostics log rotates on every app launch. The JavaScript console log rotates on every config reload.

## Development

```powershell
dotnet build HsWin.slnx
dotnet test HsWin.slnx
.\scripts\Build-Installer.ps1
```

The installer is built with Inno Setup and written to `artifacts\installer\hswin-x64-setup.exe`. It installs the published `win-x64` app and shows a checked `Launch Hammerspoon (Windows Edition)` option on the final setup page.

## Releases

The GitHub Actions release workflow runs on `main` pushes and manual dispatches. It builds and tests on Windows, compiles the Inno Setup installer, uploads the installer as a workflow artifact, and publishes a normal GitHub release tagged with the workflow run number.
