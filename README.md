# Hammerspoon (Windows Edition)

Hammerspoon (Windows Edition) is a tray-first Windows automation host inspired by Hammerspoon. It starts without a main window, loads JavaScript from `%APPDATA%\HsWin\config.js`, and exposes a small `hs` API to scripts.

## Current API

```js
hs.alert.show("Config loaded");
console.log("Config loaded");
hs.alert.show("Saved", "success", 2000);
hs.alert.show("Something failed", { type: "error", durationMs: 4000 });
hs.alert.show("Plain message", { type: "normal", durationMs: 1500 });
hs.hotkey.bind(["ctrl", "alt"], "R", () => hs.alert.show("Hotkey pressed"));
hs.application.isRunning("r5apex");
hs.application.runningApplications();
hs.hotkey.bind(["ctrl", "alt"], "mouse.middle", () => hs.alert.show("Middle mouse"));
hs.hotkey.bind([], "mouse.back", () => hs.alert.show("Thumb back"));
hs.hotkey.bind([], "mouse.forward", () => hs.alert.show("Thumb forward"));

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
    hs.alert.show(result.action === "played" ? "Played" : "Paused", { durationMs: 400 });
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
```

Defaults:

- `type`: `success`
- `durationMs`: `2000`

Alert types:

- `normal`: text only, no dot
- `success`: green dot
- `error`: red dot

## Projects

- `src/HsWin.App`: WPF tray application, toast window, startup integration, editor launching.
- `src/HsWin.Core`: config file creation, alert contracts, ClearScript runtime.
- `tests/HsWin.Core.Tests`: parser, config, and JavaScript bridge tests.

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
