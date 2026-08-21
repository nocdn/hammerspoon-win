# Plan 009: Prefilter the previous-instance startup scan instead of walking every process

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report — do not improvise. When done, update the status row for this plan
> in `plans/README.md` — unless a reviewer dispatched you and told you they
> maintain the index.
>
> **Drift check (run first)**: `git diff --stat 9244f32..HEAD -- src/HsWin.App/PreviousInstanceCleaner.cs src/HsWin.App/SingleInstanceGuard.cs`
> If any in-scope file changed since this plan was written, compare the
> "Current state" excerpts against the live code before proceeding; on a
> mismatch, treat it as a STOP condition.

## Status

- **Priority**: P2
- **Effort**: S
- **Risk**: LOW-MED (startup correctness for zombie-instance cleanup)
- **Depends on**: none
- **Category**: perf
- **Planned at**: commit `9244f32`, 2026-08-21

## Why this matters

`SingleInstanceGuard.Acquire` runs in the `AppController` constructor on the
UI thread before the tray icon, any service, or the config exists — and even
on the happy path (mutex acquired on the first try, no other instance) it
runs `PreviousInstanceCleaner.TerminatePreviousInstances`, which does
`Process.GetProcesses()` and queries `ProcessName`/`SessionId` for **every**
process on the system, plus `MainModule.FileName` reads for name matches.
That is typically tens of milliseconds (worse on heavy machines, and
`MainModule` can stall on protected processes) added to time-to-tray-icon on
**every launch** of a login-start app — all to find processes that can only
ever match two known executable names. Prefiltering by those names turns the
scan into a couple of `Process.GetProcessesByName` calls that only allocate
`Process` objects for actual candidates, preserving the matcher's decisions
exactly.

## Current state

Files and their roles:

- `src/HsWin.App/SingleInstanceGuard.cs` — named-mutex single-instance guard;
  calls `TerminatePreviousInstances` on both the contended path and after a
  clean first acquisition (post-acquire cleanup for mutex-less zombies).
- `src/HsWin.App/PreviousInstanceCleaner.cs` — `TerminatePreviousInstances`
  (line 14) scans all processes; `PreviousInstanceProcessMatcher` (line 161)
  holds the pure decision logic.
- `src/HsWin.App/AppController.cs:76` — `SingleInstanceGuard.Acquire(_logger)`
  inside the constructor (UI thread, `App.xaml.cs:16`).
- Tests: `tests/HsWin.App.Tests/PreviousInstanceProcessMatcherTests.cs`
  (pure matcher decisions — must keep passing unchanged),
  `tests/HsWin.App.Tests/SingleInstanceGuardTests.cs` (uses an injected
  `terminatePreviousInstances` delegate — behavior-preserving refactor keeps
  these passing).

Excerpts (as of `9244f32`):

`PreviousInstanceCleaner.cs:25-64` — the scan:
```csharp
foreach (var process in Process.GetProcesses())
{
    using (process)
    {
        inspected++;
        var processId = process.Id;
        if (processId == currentProcessId) { continue; }
        var processName = TryGetProcessName(process);
        var sessionId = TryGetSessionId(process);
        var shouldTerminate = PreviousInstanceProcessMatcher.ShouldTerminate(
            processId, currentProcessId, processName, sessionId, currentSessionId,
            candidateExecutablePath: null, currentExecutablePath);
        string? executablePath = null;
        if (!shouldTerminate && PreviousInstanceProcessMatcher.ShouldReadExecutablePath(processName, currentExecutablePath))
        {
            pathChecks++;
            executablePath = TryGetExecutablePath(process);
            shouldTerminate = PreviousInstanceProcessMatcher.ShouldTerminate(
                processId, currentProcessId, processName, sessionId, currentSessionId,
                executablePath, currentExecutablePath);
        }
        if (!shouldTerminate) { continue; }
        // ... stop logic (kept as-is)
```

`PreviousInstanceProcessMatcher.cs` (inside PreviousInstanceCleaner.cs:161-208) —
the only two ways a process can be terminated:
```csharp
private static readonly string[] KnownProcessNames =
[
    AppBranding.DisplayName,   // "Hammerspoon (Windows Edition)"
    "HsWin.App"
];
// ShouldTerminate returns true only when:
//  - candidateProcessId != currentProcessId, AND
//  - same session (or unknown), AND
//  - (name ∈ KnownProcessNames  OR  executablePath == currentExecutablePath)
// ShouldReadExecutablePath gates the MainModule read: candidate name must equal
// Path.GetFileNameWithoutExtension(currentExecutablePath)
```

Key deduction making the prefilter behavior-preserving: the path-equality
branch can only be reached for processes whose name equals
`Path.GetFileNameWithoutExtension(currentExecutablePath)` (via
`ShouldReadExecutablePath`), so the candidate set is exactly
`KnownProcessNames ∪ { currentExecutableName }` — all obtainable with
`Process.GetProcessesByName(name)` (case-insensitive on Windows).

`AppBranding.DisplayName` is defined in `src/HsWin.App/AppBranding.cs` (read
it to confirm the exact value).

Repo conventions: keep the existing structured counters
(`inspected`/`pathChecks`/`matched`/`stopped`/`failed`) and the timing Info
log — they are how the improvement will be measured in runtime-logs.

## Commands you will need

| Purpose | Command | Expected on success |
|---------|---------|---------------------|
| Build | `dotnet build HsWin.slnx` | exit 0, 0 errors |
| All tests | `dotnet test HsWin.slnx` | exit 0, all pass |
| Focused | `dotnet test tests/HsWin.App.Tests --filter "FullyQualifiedName~PreviousInstanceProcessMatcherTests|FullyQualifiedName~SingleInstanceGuardTests"` | exit 0, all pass |

## Scope

**In scope** (the only files you should modify):
- `src/HsWin.App/PreviousInstanceCleaner.cs`
- `tests/HsWin.App.Tests/` (only if adding/refining cleaner-level tests;
  matcher tests stay untouched)

**Out of scope** (do NOT touch):
- `SingleInstanceGuard.cs` — sequencing (sync scan before services start) is
  deliberate: zombies may hold global hotkeys/RegisterHotKey slots, so the
  cleanup must finish before hotkey registration. Do NOT move it to a
  background thread.
- `PreviousInstanceProcessMatcher` decision logic.
- `AppController.cs`, `App.xaml.cs`.

## Git workflow

- Branch: `perf/009-instance-scan` (do not push or open a PR unless
  instructed).
- Commit message e.g. `perf: prefilter previous-instance scan by known process names`.

## Steps

### Step 1: Candidate-name prefilter

Expose the candidate names from the matcher (e.g. add
`public static IReadOnlyList<string> CandidateProcessNames(string? currentExecutablePath)`
returning `KnownProcessNames` plus
`Path.GetFileNameWithoutExtension(currentExecutablePath)` when non-blank and
not already present (ordinal-ignore-case dedupe)).

Rewrite the enumeration in `TerminatePreviousInstances` to:

```csharp
var candidateNames = PreviousInstanceProcessMatcher.CandidateProcessNames(currentExecutablePath);
foreach (var candidateName in candidateNames)
{
    foreach (var process in Process.GetProcessesByName(candidateName))
    {
        // same per-process body as today: inspect, matcher calls, stop logic
    }
}
```

- Keep the per-process body (matcher calls, `MainModule` read only under
  `ShouldReadExecutablePath`, stop/timeout logic, counters) byte-for-byte in
  behavior. `inspected` now counts only candidates — keep the counter name
  and add nothing new unless a field would be misleading; the summary log
  line can stay as-is.
- `Process.GetProcessesByName` returns the current process too — the
  existing `processId == currentProcessId` guard already handles it.
- Dedupe candidate names so a process matching two names is not inspected
  twice (its stop path is idempotent, but the counters should stay honest).
- Keep `TryGetProcessName`/`TryGetSessionId`/`TryGetExecutablePath` helpers
  and the summary Info/Error logs unchanged in shape.

**Verify**: `dotnet build HsWin.slnx` → exit 0;
`dotnet test tests/HsWin.App.Tests --filter "FullyQualifiedName~PreviousInstanceProcessMatcherTests|FullyQualifiedName~SingleInstanceGuardTests"` → all pass.

### Step 2: Tests

The matcher tests must pass **unchanged** (pure logic untouched). Add one
cleaner-level test if feasible without spawning real processes:

- `CandidateProcessNames`: returns the two known names; when
  `currentExecutablePath` is `"C:\x\Hammerspoon (Windows Edition).exe"` it
  does not duplicate the display name; when null/empty returns just the two
  known names (this is a pure static function — easy xUnit cases, model on
  `PreviousInstanceProcessMatcherTests.cs`).

Full `TerminatePreviousInstances` process interaction is not unit-testable
without real processes — rely on the guard tests (injected delegate) and a
manual launch check: after landing, launch the app twice quickly; the second
launch's runtime-logs `Single instance post-acquire cleanup elapsedMs=…`
line should now read ~0–5ms (vs tens of ms before), and killing a real
previous instance still works (start one instance, rename… no — simply start
the app, then start it again from the installer/CLI: the running one must be
stopped by the new one, same as before).

**Verify**: `dotnet test HsWin.slnx` → all pass.

## Test plan

Covered by Step 2. Explicitly:

1. `CandidateProcessNames` with null path → `[DisplayName, "HsWin.App"]`.
2. `CandidateProcessNames` with a path whose base name equals DisplayName →
   still two entries (dedupe).
3. `CandidateProcessNames` with an unrelated path (e.g. dev build
   `HsWin.App.exe` under a different name) → three entries when distinct.
4. Matcher tests unchanged (run them; do not edit).

## Done criteria

Machine-checkable. ALL must hold:

- [ ] `dotnet build HsWin.slnx` exits 0
- [ ] `dotnet test HsWin.slnx` exits 0 with the new `CandidateProcessNames` tests
- [ ] `grep -n "Process.GetProcesses()" src/HsWin.App/PreviousInstanceCleaner.cs` returns no matches (only `GetProcessesByName` remains)
- [ ] `git diff --stat` shows no changes to `PreviousInstanceProcessMatcher` decision methods (`ShouldTerminate`, `ShouldReadExecutablePath`) beyond the added helper
- [ ] `git status` shows changes only in the in-scope list
- [ ] `plans/README.md` status row updated

## STOP conditions

Stop and report back (do not improvise) if:

- The matcher in the live file differs from the excerpt (e.g. termination no
  longer requires a known name or path equality — the prefilter deduction
  would be invalid).
- `AppBranding.DisplayName` is not the process executable base name in
  installed builds (check the installer script
  `scripts/Build-Installer.ps1` / `installer/` for the real executable
  name); if the installed executable name differs from both
  `KnownProcessNames` entries, the path-equality branch is reachable for a
  name outside the prefilter set — STOP and report instead of widening the
  filter on your own judgment.
- A step's verification fails twice after a reasonable fix attempt.

## Maintenance notes

- If the product is ever renamed or a second executable name is introduced,
  `KnownProcessNames` (and therefore the prefilter) must be updated in
  lockstep — the single-instance cleanup silently misses unknown names.
- Reviewer focus: the per-process body should be a **move**, not a rewrite;
  diff it against the old body for behavioral drift (especially the
  `pathChecks` gating and stop-timeout handling).
- Per `AGENTS.md`, after landing, run the full handoff loop (kill running
  instances, build, test, installer) before handing to the user.
