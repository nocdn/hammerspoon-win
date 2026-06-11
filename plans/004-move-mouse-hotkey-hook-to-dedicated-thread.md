# Plan 004: Move Mouse Low-Level Hotkeys Onto A Dedicated Hook Thread

> **Executor instructions**: Follow this plan step by step. Run every verification command and confirm the expected result before moving to the next step. If anything in the "STOP conditions" section occurs, stop and report. When done, update the status row for this plan in `plans/README.md` unless a reviewer told you they maintain the index.
>
> **Drift check (run first)**: `git diff --stat e768767..HEAD -- AGENTS.md src/HsWin.App/Hotkeys/NativeMouseHotkeyHook.cs src/HsWin.App/Keyboard/NativeKeyboardEventService.cs tests/HsWin.App.Tests/NativeMouseHotkeyHookTests.cs`
> If any in-scope file changed since this plan was written, compare the "Current state" excerpts against the live code before proceeding; on a mismatch, treat it as a STOP condition.

## Status

- **Priority**: P2
- **Effort**: M
- **Risk**: MED
- **Depends on**: none
- **Category**: tech-debt
- **Planned at**: commit `e768767`, 2026-06-11

## Why This Matters

The repo explicitly requires low-level keyboard and mouse hooks to stay off the WPF/UI dispatcher. Keyboard events already use a dedicated message-pump thread. Mouse hotkeys still install `WH_MOUSE_LL` from the service's current thread, which is normally the UI dispatcher during app startup. That means UI work, toast layout, timers, or script callback pressure can delay physical mouse-button delivery. This plan brings mouse hotkeys in line with the keyboard hook architecture.

## Current State

Relevant files:

- `AGENTS.md` - repo architecture rule for low-level input hooks.
- `src/HsWin.App/Hotkeys/NativeMouseHotkeyHook.cs` - mouse-button hotkey implementation.
- `src/HsWin.App/Keyboard/NativeKeyboardEventService.cs` - existing dedicated hook-thread pattern to mirror.
- `tests/HsWin.App.Tests/NativeMouseHotkeyHookTests.cs` - currently only tests mouse button decoding.
- `tests/HsWin.App.Tests/NativeHotkeyServiceTests.cs` - tests thread invoker behavior for keyboard hotkey registration.

Current architecture rule:

```markdown
<!-- AGENTS.md:15 -->
Keep low-level input hooks off the WPF/UI dispatcher. Keyboard and mouse hooks should run on dedicated message-pump threads so startup UI work, toasts, timers, or script callbacks cannot delay physical input delivery.
```

Current mouse hook excerpts:

```csharp
// src/HsWin.App/Hotkeys/NativeMouseHotkeyHook.cs:39
_callbackContext = SynchronizationContext.Current;
```

```csharp
// src/HsWin.App/Hotkeys/NativeMouseHotkeyHook.cs:208
_hookHandle = User32.SetWindowsHookEx(WhMouseLl, _hookProcedure, Kernel32.GetModuleHandle(null), 0);
```

```csharp
// src/HsWin.App/Hotkeys/NativeMouseHotkeyHook.cs:174-190
private void DispatchCallback(Action callback)
{
    if (_callbackContext is not null)
    {
        var queuedAt = Stopwatch.GetTimestamp();
        _callbackContext.Post(_ =>
        {
            var startedAt = Stopwatch.GetTimestamp();
            _logger.Info($"Mouse hotkey callback started dispatchDelayMs={Stopwatch.GetElapsedTime(queuedAt).TotalMilliseconds:F3}.");
            callback();
            _logger.Info($"Mouse hotkey callback returned elapsedMs={Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds:F3}.");
        }, null);
        return;
    }
}
```

Keyboard hook pattern to mirror:

```csharp
// src/HsWin.App/Keyboard/NativeKeyboardEventService.cs:198-204
_hookThread = new Thread(() => HookThreadMain(ready))
{
    IsBackground = true,
    Name = "HsWin Keyboard Hook"
};
_hookThread.SetApartmentState(ApartmentState.STA);
_hookThread.Start();
```

```csharp
// src/HsWin.App/Keyboard/NativeKeyboardEventService.cs:228-242
var hookHandle = User32.SetWindowsHookEx(WhKeyboardLl, _hookProcedure, Kernel32.GetModuleHandle(null), 0);
...
while (User32.GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
{
    User32.TranslateMessage(ref message);
    User32.DispatchMessage(ref message);
}
```

## Commands You Will Need

| Purpose | Command | Expected On Success |
|---------|---------|---------------------|
| Targeted mouse tests | `dotnet test HsWin.slnx --filter "FullyQualifiedName~NativeMouseHotkeyHookTests"` | exit 0 |
| Targeted hotkey tests | `dotnet test HsWin.slnx --filter "FullyQualifiedName~NativeHotkeyServiceTests"` | exit 0 |
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

- `src/HsWin.App/Hotkeys/NativeMouseHotkeyHook.cs`
- `tests/HsWin.App.Tests/NativeMouseHotkeyHookTests.cs`
- Small internal test seams in `NativeMouseHotkeyHook` if needed

**Out of scope**:

- Keyboard hook behavior in `NativeKeyboardEventService.cs`, except as a reference.
- Public JavaScript hotkey APIs.
- Mouse movement APIs.
- Rewriting `NativeHotkeyService`.
- Changing callback semantics: callbacks should still post to the captured synchronization context when available.

## Git Workflow

- Branch: `codex/004-move-mouse-hook-to-dedicated-thread`
- Commit message style: `fix: run mouse hotkeys on a hook thread`
- Do not push or open a PR unless the operator instructed it.

## Steps

### Step 1: Add Test Seams Without Changing Behavior

Introduce the smallest internal seams needed to test hook-thread lifecycle without installing a real global mouse hook in unit tests.

Acceptable patterns:

- An internal `IMouseHookPlatform` with `SetWindowsHookEx`, `UnhookWindowsHookEx`, `CallNextHookEx`, `GetAsyncKeyState`, `GetMessage`, `TranslateMessage`, `DispatchMessage`, `PostThreadMessage`, and `GetCurrentThreadId` wrappers.
- Or a smaller internal hook-thread coordinator class whose behavior can be tested with fake delegates.

Keep production defaults private/internal and preserve current public constructor behavior.

Do not invoke real `SetWindowsHookEx` from tests.

**Verify**: `dotnet test HsWin.slnx --filter "FullyQualifiedName~NativeMouseHotkeyHookTests"` -> existing tests still pass.

### Step 2: Move Hook Installation Onto A Dedicated Message-Pump Thread

Refactor `NativeMouseHotkeyHook.EnsureHookInstalled` to follow the keyboard hook pattern:

- Add a `Thread? _hookThread`, `uint _hookThreadId`, `Exception? _hookInstallException`, and a small install timeout.
- Start a background STA thread named something like `"HsWin Mouse Hook"`.
- On that thread, call `SetWindowsHookEx(WhMouseLl, _hookProcedure, Kernel32.GetModuleHandle(null), 0)`.
- Publish `_hookHandle` and `_hookThreadId` only after successful installation.
- Signal a `ManualResetEventSlim` so the registering thread knows install success or failure.
- Run a message loop with `GetMessage`, `TranslateMessage`, and `DispatchMessage`.

Keep registration storage and duplicate checks protected by `_gate` as they are today.

**Verify**: `dotnet test HsWin.slnx --filter "FullyQualifiedName~NativeMouseHotkeyHookTests"` -> exit 0.

### Step 3: Uninstall And Stop The Hook Thread Reliably

Update `UninstallHook` to:

- Unhook the current mouse hook if present.
- Post `WM_QUIT` to the mouse hook thread if `_hookThreadId != 0`.
- Clear `_hookHandle`, `_hookThreadId`, and `_hookThread`.
- Log success/failure similarly to keyboard hook logging.

Use the keyboard implementation as a guide, but keep mouse-specific constants and messages.

**Verify**: `dotnet test HsWin.slnx --filter "FullyQualifiedName~NativeMouseHotkeyHookTests"` -> exit 0.

### Step 4: Preserve Callback Dispatch Semantics

Make sure `DispatchCallback` still posts to `_callbackContext` when one was captured, and only runs callbacks directly if no synchronization context exists.

Do not run JavaScript/script callbacks on the mouse hook thread. The hook thread should decide whether to swallow the button event and queue callbacks off the hook path.

Add tests around the coordinator/seams to prove:

- Registering the first mouse hotkey starts hook installation through the hook-thread path.
- Disposing the last registration unhooks and signals the hook thread to quit.
- Callback dispatch still uses the captured context if available.

**Verify**:

```powershell
dotnet test HsWin.slnx --filter "FullyQualifiedName~NativeMouseHotkeyHookTests"
dotnet test HsWin.slnx --filter "FullyQualifiedName~NativeHotkeyServiceTests"
```

Both commands exit 0.

### Step 5: Run Full Repo Verification And Manual Installer Handoff

```powershell
Get-Process -Name "Hammerspoon (Windows Edition)","HsWin.App" -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet build HsWin.slnx
dotnet test HsWin.slnx
.\scripts\Build-Installer.ps1
Start-Process -FilePath .\artifacts\installer\hswin-x64-setup.exe
```

**Verify**: build and tests exit 0; installer command creates `artifacts\installer\hswin-x64-setup.exe`; installer starts.

Manual smoke notes for the operator after install:

- Configure a mouse hotkey, for example `hs.hotkey.bind(["ctrl"], "mouse.back", () => hs.alert.show("Mouse"))`.
- Press the button chord and confirm the callback fires.
- Confirm normal mouse button behavior is swallowed only for matching registered hotkeys.
- Reload config and confirm old mouse hotkeys are disposed.

## Test Plan

- Existing decode tests remain passing.
- New lifecycle tests cover dedicated hook-thread installation and shutdown using fakes, not real global hooks.
- Existing `NativeHotkeyServiceTests` remain passing because keyboard hotkey registration should not regress.
- Full suite passes.

## Done Criteria

- [ ] `WH_MOUSE_LL` installation happens on a dedicated background message-pump thread.
- [ ] Mouse callbacks are not executed on the hook thread when a captured synchronization context exists.
- [ ] Disposing the last mouse registration unhooks and stops the hook thread.
- [ ] Existing mouse button decoding behavior is unchanged.
- [ ] `dotnet build HsWin.slnx` exits 0.
- [ ] `dotnet test HsWin.slnx` exits 0.
- [ ] `.\scripts\Build-Installer.ps1` exits 0.
- [ ] Installer is launched with `Start-Process`.
- [ ] Only in-scope files are modified unless a tiny shared test helper is required.
- [ ] `plans/README.md` status row for 004 is updated.

## STOP Conditions

Stop and report if:

- The only working approach you find would run script callbacks directly on the mouse hook thread.
- Tests would need to install real global mouse hooks.
- The change requires altering public `hs.hotkey` behavior.
- You cannot stop the hook thread deterministically during dispose.
- You discover Windows requires this specific low-level mouse hook to stay on the UI dispatcher in this app; include evidence if so.

## Maintenance Notes

The keyboard hook already solved most lifecycle questions this plan needs to answer. Reviewers should compare the final mouse lifecycle to `NativeKeyboardEventService` and make sure any differences are intentional. The most important review points are hook-thread shutdown, callback dispatch context, and avoiding lock-held callback execution on the hook path.
