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
- Prioritize a friendly, obvious JavaScript scripting experience. Users should not need timing hacks, Windows-hook knowledge, or defensive workarounds in their config scripts; handle fallbacks, edge cases, retries, and platform quirks inside the host wherever practical, and cover those behaviors with substantial focused tests.
- Keep low-level input hooks off the WPF/UI dispatcher. Keyboard and mouse hooks should run on dedicated message-pump threads so startup UI work, toasts, timers, or script callbacks cannot delay physical input delivery.
- Runtime diagnostics live under `%APPDATA%\HsWin\runtime-logs`; JavaScript `console.*` output lives under `%APPDATA%\HsWin\config-logs`.
- The CLI includes `hspn config lint`, which catches config syntax/API mistakes such as invalid timer intervals. After making user config changes, run `hspn config lint` against the changed config; if lint reports anything, fix the config and rerun lint before handing work back. Also run a `hspn -h` so that you know some of the features of the CLI.
- **After every code change**, before handing work back, run this full verification loop (do not skip steps):
  1. **Kill** any running `Hammerspoon (Windows Edition)` or `HsWin.App` instances so the old build is not left running.
  2. **Build:** `dotnet build HsWin.slnx`
  3. **Test:** `dotnet test HsWin.slnx`
  4. **Installer:** `.\scripts\Build-Installer.ps1` (only if build and tests pass).
  5. **Launch for manual test:** start `artifacts\installer\hswin-x64-setup.exe` for the user to install and try the change. Do **not** use `dotnet run` as the primary handoff.
  This loop applies to all app/UI changes the user will want to see in the running product—not only release-sized work.
- The installer uses Inno Setup, so `ISCC.exe` must be installed or `INNO_SETUP_ISCC` must point to it.
- GitHub Actions release publishing lives in `.github/workflows/release.yml`; releases are normal non-prerelease releases, tagged with the Actions run number.
- Make sure all code that you write, change, modify, add, etc is maintainable and high quality. We don't want technical debt so don't be lazy.
