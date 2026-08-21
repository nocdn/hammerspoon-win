# Plan 008: Cut process-table costs in default config and runningApplications

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report — do not improvise. When done, update the status row for this plan
> in `plans/README.md` — unless a reviewer dispatched you and told you they
> maintain the index.
>
> **Drift check (run first)**: `git diff --stat 9244f32..HEAD -- src/HsWin.Core/Config/ConfigFileService.cs src/HsWin.Core/Applications/ src/HsWin.Core/Scripting/ApplicationScriptApi.cs src/HsWin.Core/Scripting/Resources/bootstrap.js README.md`
> If any in-scope file changed since this plan was written, compare the
> "Current state" excerpts against the live code before proceeding; on a
> mismatch, treat it as a STOP condition.

## Status

- **Priority**: P1
- **Effort**: S
- **Risk**: MED (one documented script-facing behavior change:
  `runningApplications()` detail fields become opt-in)
- **Depends on**: none
- **Category**: perf
- **Planned at**: commit `9244f32`, 2026-08-21

## Why this matters

Two process-table costs ship in the default experience:

1. The **default config** polls `hs.application.isRunning(...)` from a
   1-second `hs.timer.doEvery` loop, forever. Each call runs
   `Process.GetProcessesByName` (a full process-table snapshot) **on the WPF
   UI dispatcher** plus an Info log line per second — constant CPU, log
   noise, and UI-thread work for a value that is only consumed when the user
   presses the Apex hotkeys.
2. `hs.application.runningApplications()` reads `MainWindowTitle` and
   `MainModule?.FileName` for **every** process on the system.
   `Process.MainModule` enumerates the loaded modules of each process and
   throws for elevated/protected processes (caught, but the throw is
   expensive). On a typical 100–300-process machine this costs tens to
   hundreds of milliseconds — all while holding the global script callback
   gate, stalling every hotkey, timer, and watcher callback in the app.

Fixes: refresh-on-demand in the default config, and make the expensive
per-process fields opt-in (`includeDetails`), defaulting to the cheap
pid+processName snapshot that name-matching use cases need.

## Current state

Files and their roles:

- `src/HsWin.Core/Config/ConfigFileService.cs` — owns the embedded default
  `config.js` template written on first launch (the template is a string
  constant inside this file; the poll is around line 103-109).
- `src/HsWin.Core/Applications/ProcessApplicationProvider.cs` — process
  queries behind `IApplicationProvider`.
- `src/HsWin.Core/Applications/IApplicationProvider.cs` — interface used by
  the script API (check exact members before changing).
- `src/HsWin.Core/Scripting/ApplicationScriptApi.cs` — host side of
  `hs.application.*`.
- `src/HsWin.Core/Scripting/Resources/bootstrap.js` — JS adapters
  (`application` object, ~line 583-593).
- `tests/HsWin.Core.Tests/ConfigFileServiceTests.cs` — asserts on the default
  template content; MUST be read before editing the template
  (`grep -n "doEvery\|apex" tests/HsWin.Core.Tests/ConfigFileServiceTests.cs`).
- `tests/HsWin.Core.Tests/ScriptRuntimeTests.cs` — engine-level API tests.

Excerpts (as of `9244f32`):

Default config (`ConfigFileService.cs` ~line 103-109):
```js
apex.refresh();
hs.timer.doEvery(1000, () => apex.refresh());

hs.hotkey.bind(["ctrl", "alt", "shift"], "F12", () => {
  const isRunning = apex.refresh();
  ...
});
hs.hotkey.bind([], "`", () => {
  if (apex.isRunning) { ... hs.media.playPause(); }
});
hs.hotkey.bind([], "delete", () => {
  if (apex.isRunning) { ... }
});
```

`ProcessApplicationProvider.cs:32-52`:
```csharp
public IReadOnlyList<ApplicationSnapshot> GetRunningApplications()
{
    using var processes = new ProcessCollection(Process.GetProcesses());
    var snapshots = processes
        .Select(CreateSnapshot)
        ...
}
private static ApplicationSnapshot CreateSnapshot(Process process)
{
    return new ApplicationSnapshot(
        process.Id,
        process.ProcessName,
        ReadSafe(() => string.IsNullOrWhiteSpace(process.MainWindowTitle) ? null : process.MainWindowTitle),
        ReadSafe(() => process.MainModule?.FileName));
}
```

`ApplicationScriptApi.cs:46-51`:
```csharp
public string GetRunningApplicationsJson()
{
    var applications = _applications.GetRunningApplications();
    _logger.Info($"Script hs.application.runningApplications() returned {applications.Count} processes.");
    return ScriptJson.Serialize(applications);
}
```

`bootstrap.js:588-591`:
```js
runningApplications() {
  return parseJson(host.Applications.GetRunningApplicationsJson());
},
```

Check `ApplicationSnapshot`'s definition for the exact JSON field names
(`grep -rn "record ApplicationSnapshot" src`) — JS sees camelCase of the
record properties via `ScriptJson.Options`.

Repo conventions: options parsing goes through
`ScriptArgumentReader.GetPropertyValue(value, "camelName")` (see
`HotkeyScriptApi.ParseHeldOptions` line 108-120 for the pattern, including
alias support). README "Current API" documents every script API — it MUST be
updated when `runningApplications` gains an option (AGENTS.md rule).

## Commands you will need

| Purpose | Command | Expected on success |
|---------|---------|---------------------|
| Build | `dotnet build HsWin.slnx` | exit 0, 0 errors |
| All tests | `dotnet test HsWin.slnx` | exit 0, all pass |
| Focused | `dotnet test tests/HsWin.Core.Tests --filter "FullyQualifiedName~ConfigFileServiceTests"` | exit 0 |
| Lint smoke | `dotnet run --project src/HsWin.Cli -- config lint` | exit 0 |

## Scope

**In scope** (the only files you should modify):
- `src/HsWin.Core/Config/ConfigFileService.cs` (default template only)
- `src/HsWin.Core/Applications/ProcessApplicationProvider.cs`
- `src/HsWin.Core/Applications/IApplicationProvider.cs` (only if the member
  signature must change)
- `src/HsWin.Core/Scripting/ApplicationScriptApi.cs`
- `src/HsWin.Core/Scripting/Resources/bootstrap.js` (adapter only)
- `README.md` (Current API section for `runningApplications`)
- Tests: `ConfigFileServiceTests.cs`, `ScriptRuntimeTests.cs` (extend);
  new `tests/HsWin.Core.Tests/ProcessApplicationProviderTests.cs` if
  feasible with a fake (see Test plan)

**Out of scope** (do NOT touch):
- `hs.application.isRunning` semantics (no caching — staleness would be a
  behavior change; the default-config fix removes the polling instead).
- `ProcessNameMatcher`, `IsRunning` implementation.
- Any other script API.

## Git workflow

- Branch: `perf/008-process-table` (do not push or open a PR unless
  instructed).
- Commits: e.g. `perf: stop default config polling process table every second`,
  `feat: make runningApplications details opt-in`.

## Steps

### Step 1: Default config — refresh on demand, drop the 1-second poll

In the default template inside `ConfigFileService.cs`:

- Delete `hs.timer.doEvery(1000, () => apex.refresh());`.
- Keep the initial `apex.refresh();` (runs once at load).
- Change the two consumers to refresh on press so behavior stays correct
  without the poll:
```js
hs.hotkey.bind([], "`", () => {
  if (apex.refresh()) { hs.alert.show("Play/Pause", { durationMs: 400 }); hs.media.playPause(); }
});
hs.hotkey.bind([], "delete", () => {
  if (apex.refresh()) { hs.media.previousTrack(); }
});
```
(Match the surrounding style of the live template exactly; keep the F12
hotkey as-is since it already refreshes.)
- Update `tests/HsWin.Core.Tests/ConfigFileServiceTests.cs` if it asserts the
  template contains the timer or specific hotkey bodies — the test should now
  assert the timer is **absent** and the refresh-on-press lines are present.

**Verify**: `dotnet test tests/HsWin.Core.Tests --filter "FullyQualifiedName~ConfigFileServiceTests"` → all pass.

### Step 2: Opt-in details for runningApplications

- `IApplicationProvider`: change `GetRunningApplications()` to
  `GetRunningApplications(bool includeDetails)` (single implementation today —
  check `grep -rn "GetRunningApplications" src tests` for all call sites and
  update them).
- `ProcessApplicationProvider.CreateSnapshot`: when `includeDetails` is
  false, pass `null` for the title and path parameters without touching
  `MainWindowTitle`/`MainModule` (keep `ReadSafe` for the true case).
- `ApplicationScriptApi`: `public string GetRunningApplicationsJson(object? options = null)`
  parsing `includeDetails` (aliases: `"details"`) via
  `ScriptArgumentReader.GetPropertyValue`, default **false**, converting with
  `Convert.ToBoolean(value, CultureInfo.InvariantCulture)` inside a
  try/catch matching `ConvertOptionalBoolean` in `HotkeyScriptApi.cs:122-127`
  (invalid types throw `ArgumentException("options.includeDetails must be a boolean", ...)`).
  Log the choice in the existing Info line
  (`... processes includeDetails={includeDetails}.`).
- `bootstrap.js`:
```js
runningApplications(options) {
  return parseJson(host.Applications.GetRunningApplicationsJson(options));
},
```
- `README.md` Current API section: update the `hs.application.runningApplications()`
  entry — new signature `runningApplications(options?)`, document
  `{ includeDetails: true }` returning `title`/`path` per app, and that the
  default returns `pid`/`processName` only (with a one-line rationale: fast
  snapshot without per-process module reads). Match the README's existing
  example format.

**Verify**: `dotnet build HsWin.slnx` → exit 0;
`dotnet test HsWin.slnx` → all pass.

### Step 3: Tests

1. `ScriptRuntimeTests.cs`: a script calling
   `hs.application.runningApplications()` receives an array whose entries
   have numeric `pid` and string `processName`, and whose `title`/`path` are
   `null`/absent; a call with `{ includeDetails: true }` includes the fields
   (fields may legitimately be `null` for elevated processes — assert
   presence of the *key* or accept null). Note: engine tests that assert the
   old always-present `title` key must be updated.
2. Options parsing: `includeDetails: "yes"` throws a script error with the
   parameter name in the message (follow the existing pattern for bad
   boolean options — see how other API option tests assert errors).
3. Template test updated per Step 1.

**Verify**: `dotnet test HsWin.slnx` → all pass;
`dotnet run --project src/HsWin.Cli -- config lint` → exit 0.

## Test plan

Covered by Step 3. If `ProcessApplicationProvider` can be tested without a
real process table (it cannot easily — `Process.GetProcesses` is not
interface-driven), skip provider unit tests and rely on the engine-level
tests plus a manual timing note in the PR description (before/after
`runningApplications()` duration from the runtime log line).

## Done criteria

Machine-checkable. ALL must hold:

- [ ] `dotnet build HsWin.slnx` exits 0
- [ ] `dotnet test HsWin.slnx` exits 0 with new/updated tests
- [ ] `grep -n "doEvery(1000" src/HsWin.Core/Config/ConfigFileService.cs` returns no matches
- [ ] `grep -n "MainModule" src/HsWin.Core/Applications/ProcessApplicationProvider.cs` shows the read is conditional on `includeDetails`
- [ ] README "Current API" documents the `runningApplications(options?)` signature
- [ ] `dotnet run --project src/HsWin.Cli -- config lint` exits 0
- [ ] `git status` shows changes only in the in-scope list
- [ ] `plans/README.md` status row updated

## STOP conditions

Stop and report back (do not improvise) if:

- Existing engine tests (or the lint analyzer in
  `src/HsWin.Core/Config/`) treat `title`/`path` as always present in a way
  that cannot be updated without weakening a real contract.
- `IApplicationProvider` has other implementations/callers beyond
  `ProcessApplicationProvider`/`ApplicationScriptApi`/`ConfigLintRuntimeServices`
  that would need broader changes.
- The default template's Apex block differs materially from the excerpt
  (drift).
- A step's verification fails twice after a reasonable fix attempt.

## Maintenance notes

- Breaking-change note for the changelog/PR: scripts relying on
  `.title`/`.path` from `runningApplications()` must pass
  `{ includeDetails: true }`. The default config does not use these fields.
- If a future Hammerspoon-parity API adds per-app window titles
  (`hs.window` already owns window queries), keep this API cheap — do not
  reintroduce eager `MainModule` reads.
- Per `AGENTS.md`, after landing, run the full handoff loop (kill running
  instances, build, test, installer) before handing to the user.
