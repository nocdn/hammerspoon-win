# Agent Notes

## Development Rules

- Keep adding tests as new features are implemented. Every new public script-facing API, parser behavior, lifecycle behavior, or non-trivial bug fix should come with focused tests.
- Keep the architecture split clean:
  - `HammerspoonWin.Core` owns config paths, JavaScript runtime behavior, API contracts, and testable logic.
  - `HammerspoonWin.App` owns WPF windows, tray integration, Windows startup integration, and process/editor launching.
  - `HammerspoonWin.Core.Tests` covers the script-facing contracts and core lifecycle behavior.
  - `HammerspoonWin.App.Tests` covers WPF-side constants and app-layer behavior that can be tested without fragile screenshots.
- Prefer small composable services over large app-wide classes.
- Keep the JavaScript API Hammerspoon-like where it makes sense, using the `hs` global.
- Runtime diagnostics live under `%APPDATA%\HammerspoonWin\runtime-logs`; JavaScript `console.*` output lives under `%APPDATA%\HammerspoonWin\config-logs`.
- Build and test before handing work back:
  - Kill any currently running `HammerspoonWin.App` instances.
  - `dotnet build HammerspoonWin.slnx`
  - `dotnet test HammerspoonWin.slnx`
  - If build and tests pass after code changes, launch the app for manual testing with `dotnet run --project .\src\HammerspoonWin.App\HammerspoonWin.App.csproj`.
