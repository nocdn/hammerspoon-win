# HammerspoonWin

HammerspoonWin is a tray-first Windows automation host inspired by Hammerspoon. It starts without a main window, loads JavaScript from `%APPDATA%\HammerspoonWin\config.js`, and exposes a small `hs` API to scripts.

## Current API

```js
hs.alert.show("Config loaded");
console.log("Config loaded");
hs.alert.show("Saved", "success", 2000);
hs.alert.show("Something failed", { type: "error", durationMs: 4000 });
hs.alert.show("Plain message", { type: "normal", durationMs: 1500 });
hs.hotkey.bind(["ctrl", "alt"], "R", () => hs.alert.show("Hotkey pressed"));
```

Defaults:

- `type`: `success`
- `durationMs`: `2000`

Alert types:

- `normal`: text only, no dot
- `success`: green dot
- `error`: red dot

## Projects

- `src/HammerspoonWin.App`: WPF tray application, toast window, startup integration, editor launching.
- `src/HammerspoonWin.Core`: config file creation, alert contracts, ClearScript runtime.
- `tests/HammerspoonWin.Core.Tests`: parser, config, and JavaScript bridge tests.

## Logs

- Runtime diagnostics: `%APPDATA%\HammerspoonWin\runtime-logs\MM-dd-yyyy-HH-mm.log`
- JavaScript console output: `%APPDATA%\HammerspoonWin\config-logs\MM-dd-yyyy-HH-mm.log`

The runtime diagnostics log rotates on every app launch. The JavaScript console log rotates on every config reload.

## Development

```powershell
dotnet build HammerspoonWin.slnx
dotnet test HammerspoonWin.slnx
dotnet run --project src\HammerspoonWin.App\HammerspoonWin.App.csproj
```
