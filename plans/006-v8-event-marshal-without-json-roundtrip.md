# Plan 006: Pass event fields to V8 without the JSON string round-trip

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report — do not improvise. When done, update the status row for this plan
> in `plans/README.md` — unless a reviewer dispatched you and told you they
> maintain the index.
>
> **Drift check (run first)**: `git diff --stat 9244f32..HEAD -- src/HsWin.Core/Scripting/KeyboardScriptApi.cs src/HsWin.Core/Scripting/MouseScriptApi.cs src/HsWin.Core/Scripting/Resources/bootstrap.js`
> If any in-scope file changed since this plan was written, compare the
> "Current state" excerpts against the live code before proceeding; on a
> mismatch, treat it as a STOP condition.

## Status

- **Priority**: P1
- **Effort**: M
- **Risk**: MED (script-facing event object shape must stay byte-identical)
- **Depends on**: none (but execute after `plans/005-trim-hook-path-per-event-work.md` when possible — it touches the same keyboard path and both plans rewrite `KeyboardScriptApi.Watch`'s neighborhood)
- **Category**: perf
- **Planned at**: commit `9244f32`, 2026-08-21

## Why this matters

Every keyboard/scroll event delivered to a script callback currently crosses
the .NET↔V8 boundary as a **JSON string**: the host serializes the snapshot
with `System.Text.Json`, ClearScript marshals the string into V8, and
bootstrap.js `JSON.parse`s it — per keystroke, per wheel notch. For blocking
keyboard watches this serialization runs **on the WH_KEYBOARD_LL hook
thread**, and it is paid even when the script gate is busy and the callback
then fails open (the args are built before the gate is tried). The shipped
default config has a blocking keyboard watch on all keys, so every keystroke
system-wide pays serialize + string-marshal + parse + string garbage.
Replacing the string with a handful of primitive arguments (numbers, small
strings, booleans) marshals cheaply and lets bootstrap build the same frozen
plain JS object directly — no serializer, no parser, no throwaway string.

## Current state

Files and their roles:

- `src/HsWin.Core/Scripting/KeyboardScriptApi.cs` — host side of
  `hs.keyboard.watch`; `Watch` (line 31) is the hot path.
- `src/HsWin.Core/Scripting/MouseScriptApi.cs` — host side of
  `hs.mouse.watchScroll` (~line 89 does `ScriptJson.Serialize` per event).
- `src/HsWin.Core/Scripting/Resources/bootstrap.js` — builds the frozen `hs`
  global; JS-side adapters around every host API.
- `src/HsWin.Core/Scripting/ScriptJson.cs` — shared JSON serializer options
  (camelCase) — used by many cold APIs; do not change its options.
- `src/HsWin.Core/Scripting/ScriptCallbackInvoker.cs` — gate + V8 invoke;
  `TryInvokeScriptCallbackFailOpen(scriptFunction, out result, params args)`
  is the hook-path entry (line 77).
- Tests: `tests/HsWin.Core.Tests/ScriptRuntimeTests.cs` (2761 lines) —
  engine-level tests that run real config scripts and assert callback
  behavior.

Excerpts (as of `9244f32`):

`KeyboardScriptApi.cs:38-65` — per-event serialize, then fail-open gate:
```csharp
var registration = _keyboardEvents.Watch(
    parsedOptions,
    keyboardEvent =>
    {
        var eventJson = ScriptJson.Serialize(keyboardEvent);
        // Blocking watchers run on WH_KEYBOARD_LL — fail-open if the script gate is busy
        // so a wedged UI callback cannot freeze the physical keyboard.
        if (parsedOptions.Blocking)
        {
            if (!_callbacks.TryInvokeScriptCallbackFailOpen(scriptFunction, out var blockingResult, eventJson))
            {
                return false; // pass key through
            }
            return Convert.ToBoolean(blockingResult, CultureInfo.InvariantCulture);
        }
        var result = _callbacks.InvokeScriptCallback(scriptFunction, eventJson);
        ...
```

`bootstrap.js` (near the top, line 27):
```js
const parseJson = (json) => JSON.parse(json);
```
`bootstrap.js` keyboard adapter (~line 712):
```js
return host.Keyboard.Watch((eventJson) => callback(parseJson(eventJson)) === true, options);
```
`bootstrap.js` scroll adapter (~line 703):
```js
return host.Mouse.WatchScroll((eventJson) => {
  callback(parseJson(eventJson));
  return false;
}, options);
```

Snapshot shapes to preserve (camelCase JSON is what JS sees today):

- `KeyboardEventSnapshot` (HsWin.Core): `type` ("keydown"/"keyup"), `keyCode`
  (uint), `key` (string), `modifiers` (string[] — **order ctrl, alt, shift,
  win**), `modifierFlags` (uint), `isKeyDown`, `isKeyUp`, `isModifier`,
  `isInjected`, `isExtended` (bools).
- `MouseScrollEventSnapshot`: `type` ("scroll"), `axis` ("vertical"/
  "horizontal"), `direction` ("up"/"down"/"left"/"right"), `delta` (int),
  `notches` (double), `isVertical`, `isHorizontal`, `isInjected` (bools),
  `modifiers` (string[]), `modifierFlags` (uint), `x`, `y` (ints).
- `HotkeyModifiers` flags (HsWin.Core/Hotkeys): `Alt = 0x0001`,
  `Control = 0x0002`, `Shift = 0x0004`, `Win = 0x0008`,
  `NoRepeat = 0x4000`.

IMPORTANT marshaling constraint: a .NET `string[]` marshaled into ClearScript
is a **host array object**, not a native JS array — `Array.isArray()` would
be `false`, unlike today's `JSON.parse` result. That is why the plan passes
`modifierFlags` (a number) and rebuilds the `modifiers` array **in JS** from
the flags. Do not pass `string[]` across the boundary for the event object.

Repo conventions: script-facing behavior is guarded by engine-level tests in
`tests/HsWin.Core.Tests/ScriptRuntimeTests.cs` (find keyboard watch tests via
`grep -n "keyboard.watch" tests/HsWin.Core.Tests/ScriptRuntimeTests.cs`).
bootstrap.js is an embedded resource compiled into HsWin.Core — changes are
picked up on build automatically.

## Commands you will need

| Purpose | Command | Expected on success |
|---------|---------|---------------------|
| Build | `dotnet build HsWin.slnx` | exit 0, 0 errors |
| All tests | `dotnet test HsWin.slnx` | exit 0, all pass |
| Focused tests | `dotnet test tests/HsWin.Core.Tests --filter "FullyQualifiedName~ScriptRuntimeTests"` | exit 0, all pass |
| Config lint smoke | `dotnet run --project src/HsWin.Cli -- config lint` | exit 0 (run from repo root; uses default config template) |

## Scope

**In scope** (the only files you should modify):
- `src/HsWin.Core/Scripting/KeyboardScriptApi.cs`
- `src/HsWin.Core/Scripting/MouseScriptApi.cs`
- `src/HsWin.Core/Scripting/Resources/bootstrap.js`
- `tests/HsWin.Core.Tests/ScriptRuntimeTests.cs` (extend)

**Out of scope** (do NOT touch):
- All other `*ScriptApi.cs` JSON paths (hotkey held events, window focus,
  clipboard, audio level, http, tasks) — cold or lower-frequency; follow-up
  work once this pattern lands.
- `ScriptJson.cs`, `ScriptCallbackInvoker.cs` (the fail-open ordering tweak
  becomes unnecessary once serialization is gone; do not restructure the
  gate).
- `KeyboardEventSnapshot` / `MouseScrollEventSnapshot` record definitions.
- Host-side watch plumbing (`IKeyboardEventService`, dispatchers) — Plan 005
  owns that.

## Git workflow

- Branch: `perf/006-v8-event-fields` (do not push or open a PR unless
  instructed).
- Commit per step. Message style: conventional commits, e.g.
  `perf: pass keyboard/scroll event fields to V8 without JSON round-trip`.

## Steps

### Step 1: bootstrap.js — field-based event builders

Add near `parseJson` (top of the IIFE):

```js
const MODIFIER_FLAG_CONTROL = 0x0002;
const MODIFIER_FLAG_ALT = 0x0001;
const MODIFIER_FLAG_SHIFT = 0x0004;
const MODIFIER_FLAG_WIN = 0x0008;

const modifierNamesFromFlags = (flags) => {
  const names = [];
  if (flags & MODIFIER_FLAG_CONTROL) { names.push("ctrl"); }
  if (flags & MODIFIER_FLAG_ALT) { names.push("alt"); }
  if (flags & MODIFIER_FLAG_SHIFT) { names.push("shift"); }
  if (flags & MODIFIER_FLAG_WIN) { names.push("win"); }
  return names;
};

const buildKeyboardEvent = (type, keyCode, key, modifierFlags, isKeyDown, isKeyUp, isModifier, isInjected, isExtended) =>
  Object.freeze({
    type,
    keyCode,
    key,
    modifiers: modifierNamesFromFlags(modifierFlags),
    modifierFlags,
    isKeyDown,
    isKeyUp,
    isModifier,
    isInjected,
    isExtended
  });

const buildScrollEvent = (axis, direction, delta, notches, isVertical, isHorizontal, isInjected, modifierFlags, x, y) =>
  Object.freeze({
    type: "scroll",
    axis,
    direction,
    delta,
    notches,
    isVertical,
    isHorizontal,
    isInjected,
    modifiers: modifierNamesFromFlags(modifierFlags),
    modifierFlags,
    x,
    y
  });
```

Then change ONLY the two adapters:

```js
// keyboard.watch
return host.Keyboard.Watch(
  (type, keyCode, key, modifierFlags, isKeyDown, isKeyUp, isModifier, isInjected, isExtended) =>
    callback(buildKeyboardEvent(type, keyCode, key, modifierFlags, isKeyDown, isKeyUp, isModifier, isInjected, isExtended)) === true,
  options);

// mouse.watchScroll
return host.Mouse.WatchScroll(
  (axis, direction, delta, notches, isVertical, isHorizontal, isInjected, modifierFlags, x, y) => {
    callback(buildScrollEvent(axis, direction, delta, notches, isVertical, isHorizontal, isInjected, modifierFlags, x, y));
    return false;
  }, options);
```

Before finalizing, diff the property set and value types against a real
`JSON.parse` output of the current snapshots (the test in Step 3 enforces
this mechanically). Keep `parseJson` — other adapters still use it.

**Verify**: `dotnet build HsWin.slnx` → exit 0 (bootstrap.js is embedded; no
JS syntax check exists, so Step 3's engine tests are the real gate).

### Step 2: Host passes fields instead of a JSON string

In `KeyboardScriptApi.Watch`, replace the callback body:

```csharp
keyboardEvent =>
{
    if (parsedOptions.Blocking)
    {
        if (!_callbacks.TryInvokeScriptCallbackFailOpen(
                scriptFunction,
                out var blockingResult,
                keyboardEvent.Type,
                keyboardEvent.KeyCode,
                keyboardEvent.Key,
                keyboardEvent.ModifierFlags,
                keyboardEvent.IsKeyDown,
                keyboardEvent.IsKeyUp,
                keyboardEvent.IsModifier,
                keyboardEvent.IsInjected,
                keyboardEvent.IsExtended))
        {
            return false; // pass key through
        }
        return Convert.ToBoolean(blockingResult, CultureInfo.InvariantCulture);
    }

    var result = _callbacks.InvokeScriptCallback(
        scriptFunction,
        keyboardEvent.Type,
        keyboardEvent.KeyCode,
        keyboardEvent.Key,
        keyboardEvent.ModifierFlags,
        keyboardEvent.IsKeyDown,
        keyboardEvent.IsKeyUp,
        keyboardEvent.IsModifier,
        keyboardEvent.IsInjected,
        keyboardEvent.IsExtended);
    ...
```

(Field order must match the bootstrap adapter parameter order exactly.)
Do the equivalent in `MouseScriptApi`'s scroll-watch callback
(`grep -n "ScriptJson.Serialize" src/HsWin.Core/Scripting/MouseScriptApi.cs`
to find it) passing: `Axis, Direction, Delta, Notches, IsVertical,
IsHorizontal, IsInjected, ModifierFlags, X, Y` — again in the bootstrap
parameter order. Delete the now-dead `ScriptJson.Serialize` calls in these
two callbacks only.

Note `keyboardEvent.KeyCode` is `uint` and `ModifierFlags` is `uint` —
ClearScript marshals them as JS numbers (fine). Booleans marshal natively.

**Verify**: `dotnet build HsWin.slnx` → exit 0;
`dotnet test tests/HsWin.Core.Tests --filter "FullyQualifiedName~ScriptRuntimeTests"` → all pass.

### Step 3: Engine-level regression tests

In `tests/HsWin.Core.Tests/ScriptRuntimeTests.cs`, extend the existing
keyboard-watch and scroll-watch tests (find with
`grep -n "watchScroll\|keyboard.watch" tests/HsWin.Core.Tests/ScriptRuntimeTests.cs`).
Each test must assert the JS-visible event object **deep-equals** the old
JSON shape:

- keyboard: `type`, `keyCode`, `key`, `modifiers` (deep-equal array,
  including order `["ctrl","alt","shift","win"]` when all held),
  `modifierFlags`, `isKeyDown`, `isKeyUp`, `isModifier`, `isInjected`,
  `isExtended`; `Object.freeze` still applies (mutation attempt does not
  change it); `Array.isArray(event.modifiers)` is `true`.
- scroll: `type === "scroll"`, `axis`, `direction`, `delta`, `notches`,
  `isVertical`, `isHorizontal`, `isInjected`, `modifiers`, `modifierFlags`,
  `x`, `y`.
- blocking keyboard watch with a busy gate still fails open (there is an
  existing test pattern for fail-open — `grep -n "fail" tests/HsWin.Core.Tests/ScriptRuntimeTests.cs`).

Use the test style already present in that file (run a config script through
the runtime, capture callback results into JS-observable state, assert from
C#).

**Verify**: `dotnet test HsWin.slnx` → all pass including the new assertions.

## Test plan

Covered by Step 3. Additional explicit cases:

1. A keypress with modifiers (e.g. ctrl+shift+k) produces `modifiers:
   ["ctrl","shift"]` and correct `modifierFlags`.
2. A scroll event with no modifiers produces `modifiers: []`,
   `modifierFlags: 0`.
3. Non-blocking watch returning `true` still logs the usage warning path
   (behavior unchanged).
4. `hspn config lint` still passes (bootstrap is also executed by the
   linter): `dotnet run --project src/HsWin.Cli -- config lint` → exit 0.

**Verification**: `dotnet test HsWin.slnx` → all pass.

## Done criteria

Machine-checkable. ALL must hold:

- [ ] `dotnet build HsWin.slnx` exits 0
- [ ] `dotnet test HsWin.slnx` exits 0; new field-equality tests pass
- [ ] `grep -n "ScriptJson.Serialize" src/HsWin.Core/Scripting/KeyboardScriptApi.cs` returns no matches
- [ ] `grep -n "ScriptJson.Serialize" src/HsWin.Core/Scripting/MouseScriptApi.cs` returns no matches
- [ ] `grep -c "parseJson" src/HsWin.Core/Scripting/Resources/bootstrap.js` still ≥ 1 (cold adapters keep it)
- [ ] `git status` shows changes only in the in-scope list
- [ ] `plans/README.md` status row updated

## STOP conditions

Stop and report back (do not improvise) if:

- The excerpts don't match live code (drift).
- Any ClearScript marshaling surprise appears (e.g. `uint` args arrive in JS
  as something other than a number) that cannot be fixed by switching the
  argument type in the host call.
- An existing ScriptRuntimeTests assertion depends on receiving a JSON
  *string* (not an object) — that would mean a script-facing contract the
  audit missed.
- A step's verification fails twice after a reasonable fix attempt.

## Maintenance notes

- The field list in the host callback, the bootstrap adapter parameter list,
  and the snapshot record must stay in sync — a future field addition to
  `KeyboardEventSnapshot` must be added in all three places or scripts will
  silently see `undefined`. Reviewers should check parameter order first.
- Follow-up candidates using this exact pattern (deferred on purpose):
  held-hotkey event objects (`HotkeyScriptApi.SerializeMouseEvent`), window
  focus-watch events, clipboard change events, audio level events (~20/s).
- The `modifiers`-from-flags helper in bootstrap must be updated if
  `HotkeyModifiers` ever gains a flag; the enum values are duplicated in JS
  by design (documented by the constant names).
- Per `AGENTS.md`, after landing, run the full handoff loop (kill running
  instances, build, test, installer) before handing to the user.
