# Agent Notes

## Development Rules

- Keep adding tests as new features are implemented. Every new public script-facing API, parser behavior, lifecycle behavior, or non-trivial bug fix should come with focused tests.
- Also when you add, change or remove features, make sure to update the README, the "Current API" section which shows anyone how to use the APIs (examples), and also lets them see what APIs are available.
- Keep the architecture split clean:
  - `HsWin.Core` owns config paths, JavaScript runtime behavior, API contracts, and testable logic.
  - `HsWin.App` owns WPF windows, tray integration, Windows startup integration, and process/editor launching.
  - `HsWin.Core.Tests` covers the script-facing contracts and core lifecycle behavior.
  - `HsWin.App.Tests` covers WPF-side constants and app-layer behavior that can be tested without fragile screenshots.
- Prefer small composable services over large app-wide classes.
- Keep the JavaScript API Hammerspoon-like where it makes sense, using the `hs` global.
- Runtime diagnostics live under `%APPDATA%\HsWin\runtime-logs`; JavaScript `console.*` output lives under `%APPDATA%\HsWin\config-logs`.
- Build and test before handing work back:
  - Kill any currently running `Hammerspoon (Windows Edition)` or `HsWin.App` instances.
  - `dotnet build HsWin.slnx`
  - `dotnet test HsWin.slnx`
  - If build and tests pass after code changes, create the installer with `.\scripts\Build-Installer.ps1`.
  - Launch the generated installer from `artifacts\installer` for manual testing instead of starting the app with `dotnet run`.
- The installer uses Inno Setup, so `ISCC.exe` must be installed or `INNO_SETUP_ISCC` must point to it.
- GitHub Actions release publishing lives in `.github/workflows/release.yml`; releases are normal non-prerelease releases, tagged with the Actions run number.
