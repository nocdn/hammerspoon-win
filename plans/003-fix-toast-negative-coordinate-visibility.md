# Plan 003: Treat Negative-Monitor Toast Positions As Visible

> **Executor instructions**: Follow this plan step by step. Run every verification command and confirm the expected result before moving to the next step. If anything in the "STOP conditions" section occurs, stop and report. When done, update the status row for this plan in `plans/README.md` unless a reviewer told you they maintain the index.
>
> **Drift check (run first)**: `git diff --stat e768767..HEAD -- src/HsWin.App/ToastPresenter.cs tests/HsWin.App.Tests/ToastPresenterTests.cs`
> If any in-scope file changed since this plan was written, compare the "Current state" excerpts against the live code before proceeding; on a mismatch, treat it as a STOP condition.

## Status

- **Priority**: P1
- **Effort**: S
- **Risk**: LOW
- **Depends on**: none
- **Category**: bug
- **Planned at**: commit `e768767`, 2026-06-11

## Why This Matters

Windows virtual desktop coordinates can be negative when a monitor is left of or above the primary display. `ToastPresenter` uses a sentinel coordinate of `-1000000` to keep the warm toast hidden, but its visibility check effectively treats any `Left <= 0` as offscreen. A visible toast centered on a left-hand monitor can therefore skip the normal exit animation/state path and be treated like the hidden warm window.

## Current State

Relevant files:

- `src/HsWin.App/ToastPresenter.cs` - owns toast window reuse, hidden sentinel movement, and show/hide timing.
- `tests/HsWin.App.Tests/ToastPresenterTests.cs` - fake toast view tests for prewarm, reuse, hide, and dispose.

Current implementation excerpts:

```csharp
// src/HsWin.App/ToastPresenter.cs:14
private const double HiddenWindowCoordinate = -1_000_000;
```

```csharp
// src/HsWin.App/ToastPresenter.cs:134
var wasOnScreen = IsPositionedOnScreen(window);
if (!wasOnScreen)
{
    MoveOffscreen(window);
}
```

```csharp
// src/HsWin.App/ToastPresenter.cs:192-197
if (!IsPositionedOnScreen(_window))
{
    CancelExitAndReset(_window);
    MoveOffscreen(_window);
    return;
}
```

```csharp
// src/HsWin.App/ToastPresenter.cs:220-221
private static bool IsPositionedOnScreen(IToastView window) =>
    window.Left > HiddenWindowCoordinate + 1_000_000;
```

The expression `HiddenWindowCoordinate + 1_000_000` is `0`, so any negative `Left` is treated as not positioned on screen.

Existing test pattern:

- `ToastPresenterTests` uses `FakeToastView` and injected `positionWindow`.
- Extend that fake rather than adding screenshot tests.

## Commands You Will Need

| Purpose | Command | Expected On Success |
|---------|---------|---------------------|
| Targeted tests | `dotnet test HsWin.slnx --filter "FullyQualifiedName~ToastPresenterTests"` | exit 0 |
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

- `src/HsWin.App/ToastPresenter.cs`
- `tests/HsWin.App.Tests/ToastPresenterTests.cs`

**Out of scope**:

- `ToastWindow` layout, animation timings, fonts, or visual styling.
- Multi-monitor API changes.
- Screenshot tests.
- Any changes to alert API parsing.

## Git Workflow

- Branch: `codex/003-fix-toast-negative-coordinate-visibility`
- Commit message style: `fix: handle negative toast coordinates`
- Do not push or open a PR unless the operator instructed it.

## Steps

### Step 1: Add A Regression Test For Negative Visible Coordinates

In `tests/HsWin.App.Tests/ToastPresenterTests.cs`, extend `FakeToastView` to track exit animation calls if needed:

- `BeginExitAnimationCount`
- `PrepareForShowCount` if useful

Add a test such as `HideAnimatesToastPositionedOnNegativeMonitor`.

Suggested structure:

1. Create `ToastPresenter` with a `positionWindow` callback that sets `window.Left = -1600` and `window.Top = 900`.
2. Call `presenter.Show(AlertRequest.Create("Visible", AlertKind.Normal, 1000));`.
3. Call `presenter.Show(AlertRequest.Create("Hide", AlertKind.Normal, 0));`.
4. Assert the fake window began exit animation once.

This test should fail against the current implementation because `Left = -1600` is treated as offscreen.

**Verify**: `dotnet test HsWin.slnx --filter "FullyQualifiedName~ToastPresenterTests"` -> the new test should fail before the implementation change. If it unexpectedly passes, STOP and report.

### Step 2: Replace Threshold Visibility With Sentinel Detection

Update `ToastPresenter` so it distinguishes the hidden sentinel from real virtual desktop coordinates.

Target behavior:

- A window at exactly the hidden sentinel set by `MoveOffscreen` is hidden/offscreen.
- A window at negative desktop coordinates such as `Left = -1600`, `Top = 900` is considered positioned onscreen for toast lifecycle purposes.

One safe shape is to replace `IsPositionedOnScreen` with a helper that checks for the sentinel coordinate pair:

```csharp
private static bool IsHiddenOffscreen(IToastView window) =>
    Math.Abs(window.Left - HiddenWindowCoordinate) < 0.5
    && Math.Abs(window.Top - HiddenWindowCoordinate) < 0.5;
```

Then use `!IsHiddenOffscreen(window)` where the code currently asks whether the toast is positioned on screen.

Do not use `Left > 0`, `Top > 0`, screen bounds, or `Screen.FromPoint` here. The presenter already has a sentinel; use it directly.

**Verify**: `dotnet test HsWin.slnx --filter "FullyQualifiedName~ToastPresenterTests"` -> exit 0.

### Step 3: Preserve Prewarm And Reuse Behavior

Run existing tests and inspect the changed logic:

- Prewarm should still show the warm window offscreen.
- Showing a visible toast after prewarm should still reuse the same window.
- `durationMs == 0` should still hide without creating a window if none exists.

If any existing test becomes ambiguous, add a focused assertion rather than weakening behavior.

**Verify**: `dotnet test HsWin.slnx --filter "FullyQualifiedName~ToastPresenterTests"` -> exit 0 with the existing tests and the new negative-coordinate test.

### Step 4: Run Full Repo Verification

```powershell
Get-Process -Name "Hammerspoon (Windows Edition)","HsWin.App" -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet build HsWin.slnx
dotnet test HsWin.slnx
.\scripts\Build-Installer.ps1
Start-Process -FilePath .\artifacts\installer\hswin-x64-setup.exe
```

**Verify**: build and tests exit 0; installer command creates `artifacts\installer\hswin-x64-setup.exe`; installer starts.

## Test Plan

- New app-layer test for visible negative `Left` coordinate.
- Existing `ToastPresenterTests` for prewarm, reuse, zero-duration hide, and dispose remain passing.
- No screenshot tests required.

## Done Criteria

- [ ] Toast lifecycle treats `Left = -1600` and a normal `Top` value as visible/positioned.
- [ ] Hidden warm window detection still works for the sentinel coordinates.
- [ ] `dotnet build HsWin.slnx` exits 0.
- [ ] `dotnet test HsWin.slnx` exits 0.
- [ ] `.\scripts\Build-Installer.ps1` exits 0.
- [ ] Installer is launched with `Start-Process`.
- [ ] Only the in-scope files are modified.
- [ ] `plans/README.md` status row for 003 is updated.

## STOP Conditions

Stop and report if:

- `ToastPresenter` no longer uses `HiddenWindowCoordinate` as the hide mechanism.
- Fixing the bug appears to require changing `ToastWindow` rendering or animation internals.
- The new test cannot be written with `FakeToastView` and would require fragile screenshot automation.
- Existing prewarm/reuse tests fail twice after a reasonable fix attempt.

## Maintenance Notes

Virtual desktop coordinates can be negative on the X axis and the Y axis. Reviewers should reject future visibility checks that assume positive screen coordinates. Keep hidden-window state tied to the explicit sentinel or a dedicated state flag, not monitor geometry guesses.
