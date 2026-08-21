# Plan 005: Trim per-event work in the keyboard and mouse hook paths

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report — do not improvise. When done, update the status row for this plan
> in `plans/README.md` — unless a reviewer dispatched you and told you they
> maintain the index.
>
> **Drift check (run first)**: `git diff --stat 9244f32..HEAD -- src/HsWin.App/Keyboard/ src/HsWin.App/Hotkeys/NativeMouseHotkeyHook.cs src/HsWin.Core/Keyboard/KeyboardKeyRules.cs src/HsWin.Core/Scripting/HotkeyScriptApi.cs`
> If any in-scope file changed since this plan was written, compare the
> "Current state" excerpts against the live code before proceeding; on a
> mismatch, treat it as a STOP condition.

## Status

- **Priority**: P1
- **Effort**: S-M
- **Risk**: MED (native input paths; behavior must be identical)
- **Depends on**: none
- **Category**: perf
- **Planned at**: commit `9244f32`, 2026-08-21

## Why this matters

Every keystroke and mouse-wheel event on the whole system passes through this
app's `WH_KEYBOARD_LL` / `WH_MOUSE_LL` callbacks, and the host priority handler
(emergency stop) keeps the keyboard hook permanently installed — so the
per-event cost is an always-on tax on system input latency and GC. Today each
keystroke with any watcher allocates: a subscription-array copy, 2 strings for
the key display name, a `List<string>` + `string[]` for modifier names, a
preformatted diagnostic string that is almost never logged, a dispatch-scope
object with a `List<DeferredAction>`, and two `AsyncLocal` writes (each
allocating an ExecutionContext node). The mouse hook additionally copies its
scroll-subscription array per wheel event (high-resolution wheels fire
hundreds–thousand events/sec) and runs a LINQ `FirstOrDefault` with a closure
per button-down. None of this changes behavior; removing it cuts allocations on
the hottest path in the app. The shipped default config registers a blocking
keyboard watch on **all** keys, so default installs pay this on every keystroke.

## Current state

Files and their roles:

- `src/HsWin.App/Keyboard/NativeKeyboardEventService.cs` — WH_KEYBOARD_LL
  service; `HookCallback` (line 164) is the hot path.
- `src/HsWin.App/Hotkeys/NativeMouseHotkeyHook.cs` — shared WH_MOUSE_LL host
  for mouse-button hotkeys and scroll watches; `HandleScrollMessage` (line
  294) and `TryHandleMouseButtonEvent` (line 327) are the hot paths.
- `src/HsWin.App/Keyboard/KeyboardHookDispatchScope.cs` — per-dispatch deferral
  scope; `Enter` is called per keystroke.
- `src/HsWin.Core/Keyboard/KeyboardKeyRules.cs` — key/modifier naming rules
  used per event.
- `src/HsWin.Core/Scripting/HotkeyScriptApi.cs` — script `hs.hotkey.bind`
  implementation; held bindings register unfiltered watches.
- Tests: `tests/HsWin.App.Tests/NativeKeyboardEventServiceTests.cs`,
  `NativeMouseHotkeyHookTests.cs`, `tests/HsWin.Core.Tests/KeyboardKeyRulesTests.cs`.

Excerpts (as of `9244f32`):

`NativeKeyboardEventService.cs:179-221` — per keystroke under `_gate`:
```csharp
lock (_gate)
{
    if (!isInjected) { _modifierTracker.Apply(hookData.VkCode, isKeyUp); }
    hostPriorityHandler = _hostPriorityHandler;
    if (hostPriorityHandler is null && _subscriptions.Count == 0)
    {
        return User32.CallNextHookEx(_hookHandle, code, wParam, lParam);
    }
    snapshot = CreateSnapshot(hookData.VkCode, isKeyUp, isInjected);
    subscriptions = _subscriptions.Count == 0 ? [] : [.. _subscriptions];
}
// ...
using (KeyboardHookDispatchScope.Enter(_logger, FormatKeyboardEvent(snapshot, hookData, message)))
{
    shouldSwallow = _watchDispatcher.Dispatch(snapshot, subscriptions);
```
Note: the snapshot is built **before** the subscription count check, so with
the always-on host priority handler every keystroke builds a snapshot even
with zero watchers (fine to keep, provided the snapshot itself is cheap —
that is what Step 2 fixes).

`NativeKeyboardEventService.cs:229-243` — `CreateSnapshot` calls
`KeyboardKeyRules.GetDisplayName(virtualKey)` and
`KeyboardKeyRules.GetModifierNames(pressedModifiers)` per event.

`KeyboardKeyRules.cs:93-108` — letter keys allocate twice per call:
```csharp
if (virtualKey is >= 'A' and <= 'Z' or >= '0' and <= '9')
{
    return ((char)virtualKey).ToString().ToLowerInvariant();
}
if (virtualKey is >= 0x70 and <= 0x87) { return $"f{virtualKey - 0x70 + 1}"; }
```

`KeyboardKeyRules.cs:136-160` — `GetModifierNames` allocates a
`List<string>(4)` plus a `string[]` per call, for one of only 16 possible
`HotkeyModifiers` combinations.

`KeyboardHookDispatchScope.cs:7-26` — per keystroke:
```csharp
private static readonly AsyncLocal<KeyboardHookDispatchScope?> CurrentScope = new();
// Enter → new scope object + List<DeferredAction> + 2 AsyncLocal writes (set + restore)
```

`NativeMouseHotkeyHook.cs:298-319` — per wheel event under the mouse `_gate`:
```csharp
subscriptions = [.. _scrollSubscriptions];
```

`NativeMouseHotkeyHook.cs:352-354` — per button-down:
```csharp
match = _registrations.Values.FirstOrDefault(registration =>
    registration.Hotkey.MouseButton == mouseButtonEvent.Button
    && registration.Hotkey.Modifiers == pressedModifiers);
```
Registrations are already unique per `(MouseButton, HotkeyModifiers)` — see
the duplicate check `HasDuplicateRegistration` (line 389).

`HotkeyScriptApi.cs:80-83` — held hotkeys watch **all** keys (no `KeyFilter`),
so `HeldHotkeyHandler.Handle` runs once per binding per keystroke:
```csharp
var handler = new HeldHotkeyHandler(definition, parsedOptions, pressedFunction, releasedFunction, _callbacks);
var watch = _keyboardEvents.Watch(
    new KeyboardEventWatchOptions(parsedOptions.IncludeInjected, parsedOptions.Blocking),
    handler.Handle);
```
For contrast, `KeyboardScriptApi.Remap` (line 82-87) already passes
`KeyFilter: new HashSet<uint> { sourceVirtualKey }`, and
`KeyboardWatchDispatcher.ShouldSkip` (line 54-59) skips filtered keys with an
O(1) `HashSet.Contains` before any callback work. `HeldHotkeyHandler.Handle`
(around line 152) only ever acts when `snapshot.KeyCode ==
_definition.VirtualKey` or, when active, on modifier key-ups via
`ShouldRelease` — so a filter of {definition VK + all 11 modifier VKs listed
in `KeyboardKeyRules`} preserves every reachable path.

Repo conventions: match existing style (file-scoped namespaces, `lock` on a
private `_gate` object, `Volatile.Read` for cross-thread reads — see
`MouseScrollWatchDispatcher.cs:159`). Tests use xUnit; model new tests on
`tests/HsWin.Core.Tests/KeyboardKeyRulesTests.cs` and
`tests/HsWin.App.Tests/NativeMouseHotkeyHookTests.cs`.

## Commands you will need

| Purpose | Command | Expected on success |
|---------|---------|---------------------|
| Build | `dotnet build HsWin.slnx` | exit 0, 0 errors |
| All tests | `dotnet test HsWin.slnx` | exit 0, all pass |
| Focused tests | `dotnet test tests/HsWin.App.Tests --filter "FullyQualifiedName~NativeKeyboardEventServiceTests"` | exit 0, all pass |
| Focused tests | `dotnet test tests/HsWin.Core.Tests --filter "FullyQualifiedName~KeyboardKeyRulesTests"` | exit 0, all pass |

## Scope

**In scope** (the only files you should modify):
- `src/HsWin.App/Keyboard/NativeKeyboardEventService.cs`
- `src/HsWin.App/Keyboard/KeyboardHookDispatchScope.cs`
- `src/HsWin.App/Hotkeys/NativeMouseHotkeyHook.cs`
- `src/HsWin.Core/Keyboard/KeyboardKeyRules.cs`
- `src/HsWin.Core/Scripting/HotkeyScriptApi.cs`
- Test files: `tests/HsWin.Core.Tests/KeyboardKeyRulesTests.cs`,
  `tests/HsWin.App.Tests/NativeKeyboardEventServiceTests.cs`,
  `tests/HsWin.App.Tests/NativeMouseHotkeyHookTests.cs` (extend, or add
  sibling files following the same naming)

**Out of scope** (do NOT touch):
- `src/HsWin.App/Hotkeys/NativeMouseHotkeyHook.cs` `ReadPressedModifiers` /
  the 5× `IsKeyDown` cross-lock — deferred deliberately (locking redesign,
  needs its own careful plan).
- `Marshal.PtrToStructure` → unsafe pointer read micro-optimization — skip.
- `KeyboardWatchDispatcher.cs`, `MouseScrollWatchDispatcher.cs` — their
  dispatch logic is already O(1)-filtered.
- Scroll-callback queue backpressure (dispatcher `Post` per event) — separate
  finding, deferred.
- Any script-facing JS API shape.

## Git workflow

- Branch: `perf/005-hook-path-allocations` (repo works on `main`; short-lived
  topic branches are fine — do not push or open a PR unless instructed).
- Commit per step. Message style: conventional commits, e.g.
  `perf: remove per-keystroke allocations on keyboard hook path`
  (matches `git log` style: `feat:`, `fix:`).

## Steps

### Step 1: Immutable subscription snapshots in both hook services

In `NativeKeyboardEventService`, keep `_subscriptions` as the list mutated
under `_gate`, and add `private KeyboardWatchSubscription[] _subscriptionSnapshot = [];`
maintained inside the same lock at every mutation point (`Watch`, `RemoveSubscription`,
`Dispose`). `HookCallback` reads
`subscriptions = Volatile.Read(ref _subscriptionSnapshot);` and no longer
copies anything. Apply the identical pattern in `NativeMouseHotkeyHook` for
`_scrollSubscriptions` in `HandleScrollMessage` (mutations happen in the
scroll watch register/unregister paths — find them with
`grep -n "_scrollSubscriptions" src/HsWin.App/Hotkeys/NativeMouseHotkeyHook.cs`).
Prepend order (`options.Prepend` inserts at index 0) must be preserved in the
snapshot — rebuild the snapshot from the list after each mutation.

**Verify**: `dotnet build HsWin.slnx` → exit 0;
`dotnet test HsWin.slnx --filter "FullyQualifiedName~NativeKeyboardEventServiceTests|FullyQualifiedName~NativeMouseHotkeyHookTests"` → all pass.

### Step 2: Allocation-free key and modifier naming

In `KeyboardKeyRules`:
- Add a static `string?[] DisplayNameByVk = new string?[256]`, populated in a
  static constructor for: letters `'a'`–`'z'`, digits `'0'`–`'9'` (pre-lowercased
  literals, no `ToString()` at fill time — e.g. `"a"`), F-keys `"f1"`–`"f24"`
  (0x70–0x87), and the existing `KeyNames` dictionary entries.
- Rewrite `GetDisplayName` to `TryGetValue`-style lookup in the table first;
  keep the `$"vk:0x{virtualKey:X2}"` fallback for unknown VKs (rare, still allocates — acceptable).
- Replace `GetModifierNames` with a cached table: `static readonly string[][] ModifierNamesByFlags`
  indexed by `(int)modifiers & 0xF` (16 combinations of Alt/Control/Shift/Win).
  Build each array in the established order **ctrl, alt, shift, win** to match
  today's output exactly. Note `HotkeyModifiers` also has `NoRepeat = 0x4000`
  — mask it out before indexing (`modifiers & (HotkeyModifiers.Alt | HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Win)`).
  Keep returning `string[]` (callers serialize it); the array is now shared,
  so document with a one-line comment: *returned array is cached and shared —
  callers must not mutate*. Grep callers to confirm none mutate
  (`grep -rn "GetModifierNames" src tests`).

**Verify**: `dotnet test HsWin.slnx --filter "FullyQualifiedName~KeyboardKeyRulesTests"` → all pass. Then run the full suite:
`dotnet test HsWin.slnx` → all pass.

### Step 3: Lazy diagnostic strings in the keyboard hook path

`FormatKeyboardEvent(snapshot, hookData, message)` is currently evaluated
eagerly at `NativeKeyboardEventService.cs:218` but consumed only when
(a) `TryDefer` logs, or (b) `KeyboardHookDispatchScope.Dispose` logs — both
rare. Change `KeyboardHookDispatchScope.Enter` to accept the raw inputs
(`KeyboardEventSnapshot snapshot, KeyboardHookStruct hookData, int message`)
instead of a preformatted string, store them, and format lazily (a small
private `FormatSource()` using the same format string as
`FormatKeyboardEvent` today — keep byte-identical output). Delete the now
unneeded eager call. `LogKeyboardEventDispatch` keeps its own interpolation
(it only runs for swallowed events and navigation keys).

**Verify**: `dotnet build HsWin.slnx` → exit 0;
`dotnet test HsWin.slnx` → all pass.

### Step 4: Make the dispatch scope allocation-free on the common path

`KeyboardHookDispatchScope` currently allocates a scope object + `List<DeferredAction>`
+ two `AsyncLocal` writes per keystroke even though deferral almost never
happens. The hook dispatch is synchronous on one dedicated hook thread, and
all `TryDefer` call sites (`src/HsWin.App/Input/KeyboardInputService.cs:41,54,72`)
run synchronously inside that dispatch. Replace the `AsyncLocal` with
`[ThreadStatic] private static KeyboardHookDispatchScope? CurrentScope` set in
`Enter` and restored in `Dispose`, and make the `List<DeferredAction>`
lazy (`private List<DeferredAction>? _deferredActions;` allocated on first
`TryDefer`). Keep the scope object itself per dispatch (it is small) unless
you can reuse it cheaply — do not over-engineer.

Before doing this, re-verify the assumption yourself:
`grep -rn "KeyboardHookDispatchScope.TryDefer\|KeyboardHookDispatchScope.CurrentDeferredActionCount" src tests`
— every call site must be reachable only synchronously under
`_watchDispatcher.Dispatch` on the hook thread. If any call site is on
another thread (e.g. something scheduled via the UI dispatcher), **STOP** —
the AsyncLocal is load-bearing there and this step must be skipped (leave a
report; Steps 1-3 and 5 still stand alone).

**Verify**: `dotnet test HsWin.slnx` → all pass, including the remap/deferral
tests (`grep -rn "TryDefer\|DeferredAction" tests` to find them; if no test
exercises deferral through `NativeKeyboardEventService`, add one modeled on
the existing tests in `tests/HsWin.Core.Tests/ScriptRuntimeTests.cs` that
register `hs.keyboard.remap` — see the Test plan below).

### Step 5: Keyed mouse registration lookup + held-hotkey key filters

(a) In `NativeMouseHotkeyHook`, add
`private readonly Dictionary<(HotkeyMouseButton Button, HotkeyModifiers Modifiers), RegistrationState> _registrationsByHotkey = new();`
maintained next to `_registrations` (an id-keyed dictionary) in
register/unregister paths. Replace the `FirstOrDefault` scan at line 352 with
a `TryGetValue`. `HasDuplicateRegistration` becomes a `ContainsKey` check.
Keep `_registrations` for unregistration by id (store the same
`RegistrationState` in both, or store id→state and hotkey→id).

(b) In `HotkeyScriptApi` (line 81-83), pass a key filter so unrelated
keystrokes never reach `HeldHotkeyHandler.Handle`:
```csharp
var keyFilter = new HashSet<uint>(KeyboardKeyRules.GetModifierVirtualKeys(HotkeyModifiers.Alt | HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Win))
{
    definition.VirtualKey,
    KeyboardKeyRules.VkLeftShift, KeyboardKeyRules.VkRightShift,
    KeyboardKeyRules.VkLeftControl, KeyboardKeyRules.VkRightControl,
    KeyboardKeyRules.VkLeftMenu, KeyboardKeyRules.VkRightMenu,
    KeyboardKeyRules.VkLeftWin, KeyboardKeyRules.VkRightWin,
    KeyboardKeyRules.VkShift, KeyboardKeyRules.VkControl, KeyboardKeyRules.VkMenu
};
var watch = _keyboardEvents.Watch(
    new KeyboardEventWatchOptions(parsedOptions.IncludeInjected, parsedOptions.Blocking, KeyFilter: keyFilter),
    handler.Handle);
```
(Adjust to the real constructor signature of `KeyboardEventWatchOptions`;
`KeyboardScriptApi.Remap` at line 82-87 is the working example of passing
`KeyFilter`. The modifier VKs must be included because `ShouldRelease` can
match modifier key-ups while the held key is active.) Before finalizing, read
`HeldHotkeyHandler.Handle`/`ShouldRelease` in the live file and confirm the
filter covers every VK the handler can act on; if `ShouldRelease` can act on
arbitrary keys, include those too or **STOP** and report.

**Verify**: `dotnet build HsWin.slnx` → exit 0;
`dotnet test HsWin.slnx` → all pass.

## Test plan

New/extended tests (xUnit, matching existing style):

1. `KeyboardKeyRulesTests`: `GetDisplayName` returns identical values to
   before for letters, digits, F1/F24, named keys (0x08–0xDE table), and the
   `vk:0x` fallback; `GetModifierNames` returns the exact same arrays
   (including order: ctrl, alt, shift, win) for all 16 flag combinations and
   for a value with `NoRepeat` set; two calls return the same reference
   (cache hit).
2. `NativeKeyboardEventServiceTests` (or new file): registering/unregistering
   watchers while events fire still dispatches to exactly the live set
   (snapshot correctness); `Prepend` ordering is preserved in dispatch order.
3. `NativeMouseHotkeyHookTests`: a mouse-button hotkey with modifiers fires
   only on matching button+modifiers (keyed lookup equivalence), duplicate
   registration still rejected, unregister-while-pressed does not leak.
4. Held hotkey: script-level test (extend `tests/HsWin.Core.Tests/ScriptRuntimeTests.cs`
   — find the existing `hs.hotkey.bind` held tests with
   `grep -n "bindHeld\|held" tests/HsWin.Core.Tests/ScriptRuntimeTests.cs`)
   asserting: press/release still fires callbacks; an unrelated keystroke
   (e.g. 'k') does not invoke the held handler; modifier key-up after press
   still triggers the release path if it did before.
5. Deferral (Step 4): a `hs.keyboard.remap` test asserting the remapped input
   is deferred and injected after `CallNextHookEx` (existing behavior — this
   is a regression guard).

**Verification**: `dotnet test HsWin.slnx` → all pass, including the new tests.

## Done criteria

Machine-checkable. ALL must hold:

- [ ] `dotnet build HsWin.slnx` exits 0
- [ ] `dotnet test HsWin.slnx` exits 0 with the new tests above
- [ ] `grep -n "\[\.\. _subscriptions\]\|\[\.\. _scrollSubscriptions\]" src/HsWin.App -r` returns no matches
- [ ] `grep -rn "ToLowerInvariant" src/HsWin.Core/Keyboard/KeyboardKeyRules.cs` returns no matches (letter names are precomputed)
- [ ] `grep -n "AsyncLocal" src/HsWin.App/Keyboard/KeyboardHookDispatchScope.cs` returns no matches (or Step 4 was STOP-skipped and the report says so)
- [ ] `grep -n "FirstOrDefault" src/HsWin.App/Hotkeys/NativeMouseHotkeyHook.cs` returns no matches in the button-down path
- [ ] `git status` shows changes only in the in-scope list
- [ ] `plans/README.md` status row updated

## STOP conditions

Stop and report back (do not improvise) if:

- The code at the "Current state" locations doesn't match the excerpts
  (codebase has drifted).
- Step 4's assumption breaks: any `TryDefer`/`CurrentDeferredActionCount` call
  site is reachable from a non-hook thread or asynchronously after
  `Dispatch` returns.
- Step 5(b): `HeldHotkeyHandler.ShouldRelease` can act on VKs outside
  {definition VK + modifier VKs} — the filter would change behavior.
- A step's verification fails twice after a reasonable fix attempt.
- The fix appears to require touching an out-of-scope file.

## Maintenance notes

- The subscription-snapshot pattern must be kept in sync at **every**
  mutation site; a future watcher-registration change that forgets the
  snapshot rebuild would dispatch to stale watchers. Reviewers should check
  mutation-site coverage first.
- `GetModifierNames` now returns shared cached arrays — any future caller
  that mutates the result would corrupt the cache for the whole app. The
  comment in the source guards this; reviewer should verify no `.Add`/sort on
  returned arrays.
- Deferred follow-ups from the audit (deliberately NOT in this plan):
  redesign of `ReadPressedModifiers`' 5× cross-service lock per mouse event
  (publish modifier state as volatile fields updated under the keyboard gate);
  scroll-callback backpressure/coalescing on the UI dispatcher queue;
  `Marshal.PtrToStructure` → unsafe pointer read.
- Per `AGENTS.md`, after landing, run the full handoff loop (kill running
  instances, build, test, installer) before handing to the user.
