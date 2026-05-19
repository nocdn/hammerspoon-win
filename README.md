# Hammerspoon (Windows Edition)

Hammerspoon (Windows Edition) is a tray-first Windows automation host inspired by Hammerspoon. It starts without a main window, loads JavaScript from `%APPDATA%\HsWin\config.js`, and exposes a frozen `hs` global plus `console` logging.

Reload the config from the tray menu at any time. Each reload starts a fresh JavaScript engine, disposes previous hotkey bindings, and rotates the config console log file.

## Current API

### `hs.alert.show(text, optionsOrKind?, durationMs?)`

Shows a toast notification.

```js
hs.alert.show("Config loaded");
hs.alert.show("Saved", "success", 2000);
hs.alert.show("Something failed", { type: "error", durationMs: 4000 });
hs.alert.show("Plain message", { type: "normal", durationMs: 1500 });
```

| Argument | Description |
| --- | --- |
| `text` | Message string (required). |
| `optionsOrKind` | Either an alert type string (`normal`, `success`, `error`) or an options object. |
| `durationMs` | Display time in milliseconds when using the positional type argument. |

Options object fields (any one name is enough):

| Field | Aliases | Description |
| --- | --- | --- |
| `type` | `kind`, `state`, `status` | `normal`, `success`, or `error`. |
| `durationMs` | `duration` | Display time in milliseconds. |

Defaults when omitted: `type` is `success`, `durationMs` is `2000`.

Alert types:

| Type | Appearance |
| --- | --- |
| `normal` | Text only, no dot. |
| `success` | Green dot. |
| `error` | Red dot. |

The string forms `ok`, `done`, `plain`, `info`, `failure`, and `failed` are also accepted as aliases for the three main types.

### `console.log`, `console.info`, `console.warn`, `console.error`

Writes to `%APPDATA%\HsWin\config-logs\MM-dd-yyyy-HH-mm.log`. Objects are JSON-serialized; `Error` values use their stack or message.

```js
console.log("Reloading config");
console.info("Ready");
console.warn("Deprecated binding");
console.error(new Error("Something went wrong"));
console.log("Apps", hs.application.runningApplications());
```

### `hs.hotkey.bind(modifiers, key, callback)`

Registers a global hotkey or mouse-button binding. Returns a disposable registration object; bindings from a previous config reload are disposed automatically.

```js
hs.hotkey.bind(["ctrl", "alt"], "R", () => hs.alert.show("Hotkey pressed"));
hs.hotkey.bind([], "`", () => hs.media.playPause());
hs.hotkey.bind(["ctrl", "alt"], "mouse.middle", () => hs.alert.show("Middle mouse"));
hs.hotkey.bind([], "mouse.back", () => hs.alert.show("Thumb back"));
hs.hotkey.bind([], "mouse.forward", () => hs.alert.show("Thumb forward"));
```

`modifiers` is an array of modifier names (or a single modifier string). Supported modifiers:

| Name | Maps to |
| --- | --- |
| `alt`, `option`, `opt` | Alt |
| `ctrl`, `control` | Control |
| `shift` | Shift |
| `cmd`, `command`, `win`, `windows`, `meta` | Windows key |

`key` is a letter (`A`–`Z`), digit (`0`–`9`), function key (`F1`–`F24`), a named key, or a mouse button.

Named keyboard keys include `backspace`, `delete` / `del`, `tab`, `enter` / `return`, `escape` / `esc`, `space`, `pageup`, `pagedown`, `home`, `end`, `left`, `up`, `right`, `down`, `insert` / `ins`, `plus`, `minus`, `comma`, `period` / `dot`, `slash`, `semicolon`, `quote`, `backquote` / `grave`, `leftbracket`, `rightbracket`, `backslash`, and punctuation literals such as `` ` ``, `-`, `=`, `,`, `.`, `/`, `;`, `'`, `[`, `]`, `\`.

Mouse button keys include `mouse.middle`, `middle`, `mouse.back`, `back`, `mouse.forward`, `forward`, and aliases such as `mouse.xbutton1`, `button4`, `mouse.button5`.

If a hotkey callback throws, the runtime shows an error toast instead of crashing the host.

### `hs.application.isRunning(processName)`

Returns whether a process with the given name is running. The name may include or omit the `.exe` extension; matching is case-insensitive and compares the executable file name.

```js
if (hs.application.isRunning("r5apex")) {
  hs.alert.show("Apex is running");
}

if (hs.application.isRunning("r5apex_dx12.exe")) {
  hs.media.playPause();
}
```

### `hs.application.runningApplications()`

Returns an array of running application snapshots:

```js
const apps = hs.application.runningApplications();
for (const app of apps) {
  console.log(app.pid, app.processName, app.mainWindowTitle, app.path);
}
```

Each item has:

| Field | Type | Description |
| --- | --- | --- |
| `pid` | number | Process ID. |
| `processName` | string | Executable name without `.exe`. |
| `mainWindowTitle` | string \| null | Title of the main window, if any. |
| `path` | string \| null | Full path to the executable, if available. |

### `hs.media.playPause()`, `hs.media.previousTrack()`, `hs.media.nextTrack()`

Controls the current Windows media session when one is available, otherwise sends the corresponding media key.

```js
const result = hs.media.playPause();
if (result.action === "played") {
  hs.alert.show("Played", { durationMs: 400 });
} else if (result.action === "paused") {
  hs.alert.show("Paused", { durationMs: 400 });
}

hs.media.previousTrack();
hs.media.nextTrack();
```

Each call returns an object:

| Field | Description |
| --- | --- |
| `command` | `playPause`, `previousTrack`, or `nextTrack`. |
| `success` | Whether the operation reported success. |
| `action` | What happened (see below). |
| `statusBefore` | Playback status before `playPause` (`playing`, `paused`, `stopped`, `unknown`, etc.). |
| `statusAfter` | Playback status after `playPause`. |
| `backend` | `mediaSession` when a session was controlled, otherwise `sendInput`. |

`playPause` `action` values:

| `action` | Meaning |
| --- | --- |
| `played` | Playback is now playing (or was inferred to have started). |
| `paused` | Playback is now paused or stopped (or was inferred to have paused). |
| `toggled` | Status could not be determined clearly. |
| `playPause` | Media key fallback was used (`backend` is `sendInput`). |

`previousTrack` and `nextTrack` set `action` to `previousTrack` or `nextTrack`. When the media-key fallback is used, `statusBefore` and `statusAfter` are `unknown`.

### Example config

The default `config.js` created on first run combines application checks, alerts, hotkeys, and media controls:

```js
console.log("Reloading config");

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
```

## Projects

- `src/HsWin.App`: WPF tray application, toast window, native hotkeys and media control, startup integration, editor launching.
- `src/HsWin.Core`: config file creation, alert contracts, ClearScript runtime, and script-facing API wiring.
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
