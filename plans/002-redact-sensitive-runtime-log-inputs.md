# Plan 002: Redact Sensitive Command And URL Data From Runtime Logs

> **Executor instructions**: Follow this plan step by step. Run every verification command and confirm the expected result before moving to the next step. If anything in the "STOP conditions" section occurs, stop and report. When done, update the status row for this plan in `plans/README.md` unless a reviewer told you they maintain the index.
>
> **Drift check (run first)**: `git diff --stat e768767..HEAD -- src/HsWin.Core/Logging src/HsWin.Core/Scripting src/HsWin.Core/Http src/HsWin.App/Shell tests/HsWin.Core.Tests tests/HsWin.App.Tests README.md`
> If any in-scope file changed since this plan was written, compare the "Current state" excerpts against the live code before proceeding; on a mismatch, treat it as a STOP condition.

## Status

- **Priority**: P1
- **Effort**: S-M
- **Risk**: LOW
- **Depends on**: none
- **Category**: security
- **Planned at**: commit `e768767`, 2026-06-11

## Why This Matters

Runtime diagnostics live under `%APPDATA%\HsWin\runtime-logs`. The current logs include full shell command strings and full HTTP URLs. User automation commonly puts bearer tokens, API keys, signed URLs, or passwords in command arguments or URL query strings. The host should preserve enough diagnostics to debug behavior without persisting likely secrets in long-lived log files.

## Current State

Relevant files:

- `src/HsWin.Core/Scripting/ShellScriptApi.cs` - logs `hs.execute()` requests.
- `src/HsWin.Core/Scripting/TaskScriptApi.cs` - logs `hs.task.run()` requests.
- `src/HsWin.App/Shell/ProcessShellService.cs` - logs command completion, timeout, and failure.
- `src/HsWin.Core/Scripting/HttpScriptApi.cs` - logs `hs.http.request()` startup.
- `src/HsWin.Core/Http/SystemHttpService.cs` - logs HTTP completion and cancellation.
- `src/HsWin.Core/Logging` - best place for a reusable redaction helper because both Core and App can reference it.
- `tests/HsWin.Core.Tests/ScriptRuntimeTests.cs` - has `CapturingRuntimeLogger` at line 2151.
- `README.md` - Logs section should mention redaction after this behavior changes.

Current logging excerpts:

```csharp
// src/HsWin.Core/Scripting/ShellScriptApi.cs:22
_logger.Info($"Script hs.execute() requested command='{normalizedCommand}' timeoutMs={parsedOptions.TimeoutMs}.");
```

```csharp
// src/HsWin.Core/Scripting/TaskScriptApi.cs:51
_logger.Info($"Script hs.task.run() started command='{normalizedCommand}' timeoutMs={parsedOptions.TimeoutMs}.");
```

```csharp
// src/HsWin.App/Shell/ProcessShellService.cs:37-49
_logger.Warning($"Command timed out command='{command}' timeoutMs={options.TimeoutMs}.");
_logger.Info($"Command completed command='{command}' exitCode={process.ExitCode} success={success}.");
_logger.Error($"Command failed command='{command}'.", exception);
```

```csharp
// src/HsWin.Core/Scripting/HttpScriptApi.cs:62
_logger.Info($"Script hs.http.request() started method='{parsedOptions.Method}' url='{parsedOptions.Url}'.");
```

```csharp
// src/HsWin.Core/Http/SystemHttpService.cs:76,92
_logger.Info($"HTTP request completed method='{_options.Method}' url='{_options.Url}' statusCode={(int)response.StatusCode}.");
_logger.Info($"HTTP request canceled method='{_options.Method}' url='{_options.Url}'.");
```

Repo conventions:

- `HsWin.Core` owns script-facing behavior and logging contracts.
- `HsWin.App` owns process execution.
- Tests use focused xUnit classes and small fake loggers instead of snapshot-heavy assertions.
- Recent commit style is conventional, e.g. `fix: improve keyboard remaps and startup input responsiveness`.

## Commands You Will Need

| Purpose | Command | Expected On Success |
|---------|---------|---------------------|
| Targeted core tests | `dotnet test HsWin.slnx --filter "FullyQualifiedName~ScriptRuntimeTests"` | exit 0 |
| Targeted app tests | `dotnet test HsWin.slnx --filter "FullyQualifiedName~ProcessShellService"` | exit 0 if you add app tests; if no tests match, use full app test project |
| Full build | `dotnet build HsWin.slnx` | exit 0, no warnings as errors |
| Full tests | `dotnet test HsWin.slnx` | exit 0, all tests pass |
| Installer | `.\scripts\Build-Installer.ps1` | exit 0, prints `artifacts\installer\hswin-x64-setup.exe` |
| Manual handoff | `Start-Process -FilePath .\artifacts\installer\hswin-x64-setup.exe` | installer starts for the user |

Before final verification:

```powershell
Get-Process -Name "Hammerspoon (Windows Edition)","HsWin.App" -ErrorAction SilentlyContinue | Stop-Process -Force
```

## Scope

**In scope**:

- `src/HsWin.Core/Logging/LogSanitizer.cs` or a similarly named small helper
- `src/HsWin.Core/Scripting/ShellScriptApi.cs`
- `src/HsWin.Core/Scripting/TaskScriptApi.cs`
- `src/HsWin.Core/Scripting/HttpScriptApi.cs`
- `src/HsWin.Core/Http/SystemHttpService.cs`
- `src/HsWin.App/Shell/ProcessShellService.cs`
- `tests/HsWin.Core.Tests/*` focused tests
- `tests/HsWin.App.Tests/*` focused tests if needed for `ProcessShellService`
- `README.md` Logs section

**Out of scope**:

- Redacting JavaScript `console.*` output. That is explicitly user-authored config logging.
- Changing `ShellExecutionResult.Command`; scripts may depend on the returned result shape.
- Removing useful status fields such as method, host, path, timeout, exit code, success, or elapsed time.
- Changing how commands execute.
- Blocking URLs or adding an HTTP allowlist.

## Git Workflow

- Branch: `codex/002-redact-runtime-log-inputs`
- Commit message style: `fix: redact sensitive runtime log inputs`
- Do not push or open a PR unless the operator instructed it.

## Steps

### Step 1: Add A Small Shared Log Sanitizer

Create a small helper in `src/HsWin.Core/Logging`, for example `LogSanitizer`.

Required behavior:

- `DescribeCommand(string command)` must not return the raw command.
- It should return useful metadata, for example command length and a short SHA-256 fingerprint.
- `DescribeUrl(string url)` should preserve scheme, host, port, and path when parseable, but redact query values. A safe format is `https://host/path?<redacted-query>` or `https://host/path?keys=token,model` with values omitted.
- If the URL is not parseable, return metadata without the raw string, for example `invalid-url length=N`.
- Do not log real secrets in tests or fixtures.

Add focused unit tests in the most appropriate test project. If you place the helper in Core, prefer `tests/HsWin.Core.Tests`.

**Verify**: `dotnet test HsWin.slnx --filter "FullyQualifiedName~LogSanitizer"` -> exit 0 and tests prove raw command/query values do not appear.

### Step 2: Use The Sanitizer In Core Script APIs

Update:

- `ShellScriptApi.ExecuteCommandJson`
- `TaskScriptApi.Run`
- `HttpScriptApi.Request`
- `SystemHttpService.HttpRequestTask.SendAsync`

Replace logs that embed raw command or raw URL with sanitized descriptions.

Example target shape:

```csharp
var commandDescription = LogSanitizer.DescribeCommand(normalizedCommand);
_logger.Info($"Script hs.execute() requested command={commandDescription} timeoutMs={parsedOptions.TimeoutMs}.");
```

For HTTP logs, preserve `method` and status code, but use sanitized URL text.

Add `ScriptRuntimeTests` coverage using `CapturingRuntimeLogger`:

- `hs.execute()` with a command containing a fake token-like argument logs the command description but not the fake token-like value.
- `hs.http.get()` with query parameters logs host/path or redacted URL text but not query parameter values.

Use fake placeholder values only. Do not put real secrets in tests.

**Verify**: `dotnet test HsWin.slnx --filter "FullyQualifiedName~ScriptRuntimeTests"` -> exit 0 and the new redaction tests pass.

### Step 3: Use The Sanitizer In App Shell Execution Logs

Update `src/HsWin.App/Shell/ProcessShellService.cs` so timeout, completion, and failure logs use the same command description instead of raw `command`.

If adding focused tests is straightforward, add `tests/HsWin.App.Tests/ProcessShellServiceTests.cs` with a fake logger and a command that exits quickly. If testing actual process execution becomes fragile, add sanitizer tests plus a simple source-level coverage through a small extracted internal method. Keep the test deterministic.

**Verify**: `dotnet test HsWin.slnx --filter "FullyQualifiedName~HsWin.App.Tests"` -> exit 0.

### Step 4: Update README Logs Documentation

In `README.md`, update the Logs section to say runtime diagnostics intentionally redact shell command text and URL query values while preserving command fingerprints and request metadata. Keep the wording brief.

Do not change examples in the Current API unless the script-facing API itself changed.

**Verify**: `git diff -- README.md src/HsWin.Core src/HsWin.App/Shell tests` -> diff is limited to redaction helper, log call sites, focused tests, and README log wording.

### Step 5: Run Full Repo Verification

```powershell
Get-Process -Name "Hammerspoon (Windows Edition)","HsWin.App" -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet build HsWin.slnx
dotnet test HsWin.slnx
.\scripts\Build-Installer.ps1
Start-Process -FilePath .\artifacts\installer\hswin-x64-setup.exe
```

**Verify**: build and tests exit 0; installer command creates `artifacts\installer\hswin-x64-setup.exe`; installer starts.

## Test Plan

- Unit tests for command description redaction.
- Unit tests for URL query-value redaction.
- Script runtime test proving `hs.execute` request logs do not include the raw command text.
- Script runtime test proving `hs.http` request logs do not include URL query values.
- App-layer coverage for `ProcessShellService` if deterministic; otherwise isolate the log formatting method and test it directly.

## Done Criteria

- [ ] No runtime log call in the in-scope files writes raw shell commands.
- [ ] No runtime log call in the in-scope HTTP files writes raw URL query values.
- [ ] Logs still include useful metadata: command fingerprint/length, HTTP method, host/path or URL shape, status, timeout, exit code.
- [ ] README Logs section mentions runtime redaction.
- [ ] `dotnet build HsWin.slnx` exits 0.
- [ ] `dotnet test HsWin.slnx` exits 0.
- [ ] `.\scripts\Build-Installer.ps1` exits 0.
- [ ] Installer is launched with `Start-Process`.
- [ ] `plans/README.md` status row for 002 is updated.

## STOP Conditions

Stop and report if:

- A proposed implementation would change script return objects, especially `ShellExecutionResult.Command`.
- URL redaction cannot be implemented without sometimes logging raw query values.
- Tests would need real secret-looking values copied from a user's machine or config.
- The change appears to require altering `IRuntimeLogger` or the log file format globally.

## Maintenance Notes

This plan does not make runtime logs secret-proof; exception messages, user-authored alert text, and JavaScript console logs can still contain sensitive data. Reviewers should verify that this change removes the host's automatic persistence of raw commands and URL query values without making diagnostics useless.
