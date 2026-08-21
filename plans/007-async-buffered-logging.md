# Plan 007: Make script console logging async and stop reopening log files per batch

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report — do not improvise. When done, update the status row for this plan
> in `plans/README.md` — unless a reviewer dispatched you and told you they
> maintain the index.
>
> **Drift check (run first)**: `git diff --stat 9244f32..HEAD -- src/HsWin.Core/Logging/ReloadScriptConsoleLogger.cs src/HsWin.App/FileLogger.cs src/HsWin.Core/Scripting/KeyboardScriptApi.cs src/HsWin.App/Keyboard/NativeKeyboardEventService.cs`
> If any in-scope file changed since this plan was written, compare the
> "Current state" excerpts against the live code before proceeding; on a
> mismatch, treat it as a STOP condition.

## Status

- **Priority**: P1
- **Effort**: S
- **Risk**: LOW
- **Depends on**: none
- **Category**: perf
- **Planned at**: commit `9244f32`, 2026-08-21

## Why this matters

Two logging paths do synchronous, per-write file I/O in places where it
hurts:

1. **`console.log` from scripts** does `File.AppendAllText` (open → write →
   flush → close) per line under a global lock, **on the calling thread**.
   Script callbacks run on the WPF dispatcher while holding the global script
   callback gate, so one `console.log` inside a timer or hotkey callback
   serializes the UI thread and every other script callback behind disk I/O.
2. **The runtime `FileLogger`** (already queue + background thread) still
   opens a new `FileStream` + `StreamWriter`, flushes, and closes per drained
   batch — under sparse traffic that is one open/close cycle per log line,
   capping drain throughput while per-event hook diagnostics (addressed in
   Step 3) can enqueue faster than it drains.

Both fixes are behavior-preserving buffering changes. Step 3 additionally
removes two per-keystroke Info-log sites that pay string interpolation inline
on the WH_KEYBOARD_LL hook thread.

## Current state

Files and their roles:

- `src/HsWin.Core/Logging/ReloadScriptConsoleLogger.cs` — implements
  `IScriptConsoleLogger`; one file per config reload under
  `%APPDATA%\HsWin\config-logs`; also implemented/used by
  `src/HsWin.Core/Config/ConfigLintRuntimeServices.cs` for lint-time console
  capture (`grep -rn "IScriptConsoleLogger" src` to see all implementations
  and consumers before changing anything).
- `src/HsWin.Core/Scripting/ConsoleScriptApi.cs` — `console.log/warn/error`
  entry; calls `IScriptConsoleLogger.Write(level, message)`.
- `src/HsWin.App/FileLogger.cs` — runtime logger (queue +
  `HsWin Runtime Logger` background thread).
- `src/HsWin.Core/Scripting/KeyboardScriptApi.cs` — `Remap` callback logs per
   matched keystroke.
- `src/HsWin.App/Keyboard/NativeKeyboardEventService.cs` — `LogKeyboardEventDispatch`
  logs per swallowed/navigation keystroke.
- Tests: `tests/HsWin.App.Tests/FileLoggerTests.cs`,
  `tests/HsWin.Core.Tests/DatedLogFileNameTests.cs` (naming helpers).

Excerpts (as of `9244f32`):

`ReloadScriptConsoleLogger.cs:47-64` — synchronous append per line:
```csharp
public void Write(string level, string message)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(level);
    lock (_gate)
    {
        if (_currentLogFilePath is null) { BeginReload("config.js"); }
        var logFilePath = _currentLogFilePath
            ?? throw new InvalidOperationException("Console log file was not created.");
        File.AppendAllText(
            logFilePath,
            $"{DateTimeOffset.Now:O} [{level}] {message}{Environment.NewLine}");
    }
}
```
(`BeginReload` assigns `_currentLogFilePath` synchronously — the tray
"current log path" must be known immediately after reload; preserve that.)

`FileLogger.cs:123-144` — open/write/flush/close per batch:
```csharp
private void WriteBatch(LogEntry firstEntry)
{
    lock (_fallbackGate)
    {
        using var stream = new FileStream(_logFilePath, FileMode.Append, FileAccess.Write,
            FileShare.ReadWrite, bufferSize: 16 * 1024, FileOptions.SequentialScan);
        using var writer = new StreamWriter(stream);
        WriteEntry(writer, firstEntry);
        while (_entries.TryTake(out var nextEntry)) { WriteEntry(writer, nextEntry); }
        writer.Flush();
    }
}
```

`KeyboardScriptApi.cs:95-97` — per matched remap keystroke, on the hook
thread inside a blocking watcher:
```csharp
_logger.Info(
    $"Keyboard remap matched source='{sourceName}' target='{targetName}' type='{keyboardEvent.Type}' " +
    $"sourceVk=0x{sourceVirtualKey:X2} targetVk=0x{targetVirtualKey:X2}.");
```

`NativeKeyboardEventService.cs:382-397` — logs every swallowed event and
every PgUp/PgDn/Home/End keystroke (`IsNavigationDiagnosticKey`, line 399):
```csharp
if (!shouldSwallow && !IsNavigationDiagnosticKey(snapshot.KeyCode)) { return; }
_logger.Info($"Keyboard event key='{snapshot.Key}' type='{snapshot.Type}' vk=0x{...} ...");
```

Repo conventions: background-writer pattern with a never-throw policy already
exists in `FileLogger` (`Write` try/catch → `WriteFallback`; comment:
"Logging must never break the tray app or script reload loop"). Match it.
Tests are xUnit; `FileLoggerTests.cs` shows how to test loggers without
fragile timing (look at how it asserts file contents after `Dispose`).

## Commands you will need

| Purpose | Command | Expected on success |
|---------|---------|---------------------|
| Build | `dotnet build HsWin.slnx` | exit 0, 0 errors |
| All tests | `dotnet test HsWin.slnx` | exit 0, all pass |
| Focused | `dotnet test tests/HsWin.App.Tests --filter "FullyQualifiedName~FileLoggerTests"` | exit 0 |
| Focused | `dotnet test tests/HsWin.Core.Tests --filter "FullyQualifiedName~ReloadScriptConsoleLogger"` | exit 0 (after Step 1 adds tests) |

## Scope

**In scope** (the only files you should modify):
- `src/HsWin.Core/Logging/ReloadScriptConsoleLogger.cs`
- `src/HsWin.App/FileLogger.cs`
- `src/HsWin.Core/Scripting/KeyboardScriptApi.cs` (log-site removal only)
- `src/HsWin.App/Keyboard/NativeKeyboardEventService.cs` (log-site removal only)
- Tests: `tests/HsWin.Core.Tests/ReloadScriptConsoleLoggerTests.cs` (create),
  `tests/HsWin.App.Tests/FileLoggerTests.cs` (extend)

**Out of scope** (do NOT touch):
- `IRuntimeLogger` interface and its implementations — no new log levels;
  the log-site removals in Step 3 are deletions, not level changes.
- `LogSanitizer`, `DatedLogFileName` (naming stays as-is).
- Toast logging, hotkey-fire logging (human-rate, fine).
- Log retention/cleanup (separate deferred finding).

## Git workflow

- Branch: `perf/007-async-logging` (do not push or open a PR unless
  instructed).
- Commit per step, e.g. `perf: buffer script console logging off the calling thread`.

## Steps

### Step 1: Queued background writer for ReloadScriptConsoleLogger

Rework `ReloadScriptConsoleLogger` to mirror FileLogger's shape:

- Keep `_gate` only for `_currentLogFilePath` state and queue.
- `BeginReload`: assign the new path synchronously (as today, via
  `DatedLogFileName.CreateUniquePath`) and enqueue the "[reload] Started …"
  line; the **previous** file's writer is flushed and closed when rotation
  happens.
- `Write`: enqueue `(timestamp, level, message)`; never touch the disk on
  the calling thread. Keep the lazy `BeginReload("config.js")` behavior when
  `Write` is called before any reload.
- A single background thread (`IsBackground = true`, named
  `"HsWin Script Console Logger"`) drains the queue and writes to a
  long-lived `StreamWriter` held open for the current file; flush when the
  queue is momentarily empty (`GetConsumingEnumerable` + `TryTake` loop like
  `FileLogger.WriteBatch`) — with sparse logging that gives near-persistent
  durability without per-line flush.
- Add `IDisposable` if not present (check the `IScriptConsoleLogger`
  interface first — if it is not `IDisposable`, implement `IDisposable`
  additionally on the class and let the owner dispose it; `ScriptRuntime`
  teardown is the caller — `grep -rn "ReloadScriptConsoleLogger\|IScriptConsoleLogger" src/HsWin.App src/HsWin.Core` to find the composition root and wire disposal where the reload engine is torn down).
- Timestamps: capture `DateTimeOffset.Now` on the calling thread when
  enqueued (preserves per-line ordering semantics under the queue).
- Also expose a synchronous `Flush()` used by `BeginReload` (optionally) and
  `Dispose` so no tail lines are lost at reload/exit.

The lint-time implementation of `IScriptConsoleLogger` (if a separate
in-memory one exists in `ConfigLintRuntimeServices.cs`) must keep working
unchanged — do not change the interface.

**Verify**: `dotnet build HsWin.slnx` → exit 0;
new tests (Step 1b) pass:
`dotnet test tests/HsWin.Core.Tests --filter "FullyQualifiedName~ReloadScriptConsoleLoggerTests"` → all pass.

**Step 1b — new tests** in `tests/HsWin.Core.Tests/ReloadScriptConsoleLoggerTests.cs`:
1. Writes appear in the file after `Dispose` (flush path), with `level` and
   message intact, in enqueue order.
2. `BeginReload` rotates to a new file: lines before rotation in file A,
   after in file B, both complete after dispose.
3. `Write` before any `BeginReload` lazily creates a file (current
   behavior).
4. `CurrentLogFilePath` is set synchronously immediately after
   `BeginReload` returns (no await/dispose needed).
5. Many rapid writes (e.g. 500) all land exactly once (no drops under the
   bounded queue; if you bound the queue, use `BlockingCollection` default
   unbounded like FileLogger and note it).

### Step 2: Long-lived writer in FileLogger

In `FileLogger`: hold one `StreamWriter` open for the logger's lifetime,
created lazily on the worker thread (`_logFilePath`, `FileMode.Append`,
`FileShare.ReadWrite`, 16KB buffer). `WriteBatch` writes entries + drains
the queue into that writer, then `writer.Flush()` once when the drain is
empty. On any write failure: dispose the writer, fall back to
`WriteFallback` for that entry, and recreate the writer on the next batch
(keep the existing never-throw policy). `Dispose` flushes and closes after
the worker drains (the existing `_worker.Join(2s)` covers it).

**Verify**: `dotnet test tests/HsWin.App.Tests --filter "FullyQualifiedName~FileLoggerTests"` → all pass;
`dotnet build HsWin.slnx` → exit 0. Extend `FileLoggerTests` if it asserts
file-open-per-batch behavior (it should not — but check
`grep -n "FileShare\|FileStream" tests/HsWin.App.Tests/FileLoggerTests.cs`).

### Step 3: Remove the two per-keystroke Info log sites

(a) Delete the `_logger.Info($"Keyboard remap matched …")` at
`KeyboardScriptApi.cs:95-97` (per matched keystroke on the hook thread).
Keep the registration-time Info log (line 108-110) — it is once per remap
and states the mapping.

(b) In `NativeKeyboardEventService.LogKeyboardEventDispatch` (line 382-402):
delete the navigation-diagnostic branch (`IsNavigationDiagnosticKey` and its
helper) so the method only logs **swallowed** events — and rate-limit even
those is out of scope; a plain removal of the nav-key branch plus keeping
the swallow log is the minimum. If existing tests assert the nav-key log
lines exist, update them to assert the swallow-only behavior
(`grep -rn "IsNavigationDiagnosticKey\|navigation" tests`).

Rationale: these messages fire at typing speed; even queued, the
interpolation is paid inline on the hook thread and the log files fill with
noise. Swallowed-event logging stays because swallow events are actionable
diagnostics (something ate your key).

**Verify**: `dotnet build HsWin.slnx` → exit 0;
`dotnet test HsWin.slnx` → all pass.

## Test plan

Covered in Step 1b plus:

- Existing `FileLoggerTests` must pass unchanged (except assertions tied to
  per-batch opens, if any).
- Search for tests asserting the removed log lines:
  `grep -rn "remap matched\|Keyboard event key=" tests` → update or remove
  those assertions to match the new behavior.

**Verification**: `dotnet test HsWin.slnx` → all pass.

## Done criteria

Machine-checkable. ALL must hold:

- [ ] `dotnet build HsWin.slnx` exits 0
- [ ] `dotnet test HsWin.slnx` exits 0 with new logger tests
- [ ] `grep -n "File.AppendAllText" src/HsWin.Core/Logging/ReloadScriptConsoleLogger.cs` returns no matches
- [ ] `grep -n "new FileStream" src/HsWin.App/FileLogger.cs` matches only the lazy long-lived writer creation and the fallback path (not the per-batch path)
- [ ] `grep -rn "remap matched" src` returns no matches
- [ ] `git status` shows changes only in the in-scope list
- [ ] `plans/README.md` status row updated

## STOP conditions

Stop and report back (do not improvise) if:

- `IScriptConsoleLogger` has other implementers whose behavior would change
  (the interface is shared with lint-time capture) and the fix cannot stay
  inside `ReloadScriptConsoleLogger`.
- Tests exist that depend on console-log lines being on disk **before**
  `Write` returns (synchronous-read-after-log semantics) — that would be a
  real contract; report it rather than weakening the test.
- A step's verification fails twice after a reasonable fix attempt.

## Maintenance notes

- Reviewer focus: console-log rotation must flush the old file completely
  before writing the new one, or reload log tails will interleave; the
  dispose path must not deadlock with the script gate (never take the
  callback gate from the logger thread).
- Follow-up deferred: bound the FileLogger queue with drop-oldest policy;
  log retention (delete runtime-logs/config-logs older than N days).
- Per `AGENTS.md`, after landing, run the full handoff loop (kill running
  instances, build, test, installer) before handing to the user.
