# Plan 001: Fix Documented `hs.http` `files` Array Parsing

> **Executor instructions**: Follow this plan step by step. Run every verification command and confirm the expected result before moving to the next step. If anything in the "STOP conditions" section occurs, stop and report. When done, update the status row for this plan in `plans/README.md` unless a reviewer told you they maintain the index.
>
> **Drift check (run first)**: `git diff --stat e768767..HEAD -- src/HsWin.Core/Scripting/HttpScriptApi.cs src/HsWin.Core/Scripting/ScriptArgumentReader.cs tests/HsWin.Core.Tests/ScriptRuntimeTests.cs README.md`
> If any in-scope file changed since this plan was written, compare the "Current state" excerpts against the live code before proceeding; on a mismatch, treat it as a STOP condition.

## Status

- **Priority**: P1
- **Effort**: S
- **Risk**: LOW
- **Depends on**: none
- **Category**: bug
- **Planned at**: commit `e768767`, 2026-06-11

## Why This Matters

The README promises that `hs.http` accepts `files` as a single path, an object map, or an array of file parts. The implementation handles `multipart` arrays correctly, but `files` checks "options object" before indexed values. In ClearScript, JavaScript arrays are `ScriptObject`s, so a documented `files: [{ name, path }]` call can be interpreted as an object map instead of file-part array. Fixing this makes the public API match the docs and gives future parser changes a regression test.

## Current State

Relevant files:

- `src/HsWin.Core/Scripting/HttpScriptApi.cs` - parses script HTTP options into `HttpRequestOptions`.
- `src/HsWin.Core/Scripting/ScriptArgumentReader.cs` - shared helper for JavaScript objects, dictionaries, and indexed values.
- `tests/HsWin.Core.Tests/ScriptRuntimeTests.cs` - existing script-facing HTTP bridge tests.
- `README.md` - public API reference. It already documents the behavior this plan fixes.

Current implementation excerpts:

```csharp
// src/HsWin.Core/Scripting/ScriptArgumentReader.cs:14
public static bool IsOptionsObject(object? value)
{
    return value is ScriptObject or IReadOnlyDictionary<string, object?> or IDictionary;
}
```

```csharp
// src/HsWin.Core/Scripting/HttpScriptApi.cs:164-193
private static IReadOnlyList<HttpMultipartPart> ReadMultipartParts(object? multipartValue, object? filesValue)
{
    var parts = new List<HttpMultipartPart>();
    foreach (var partValue in ScriptArgumentReader.EnumerateIndexedValues(multipartValue))
    {
        parts.Add(ReadMultipartPart(partValue));
    }

    if (ScriptArgumentReader.IsMissing(filesValue))
    {
        return parts;
    }

    if (filesValue is string)
    {
        parts.Add(new HttpMultipartPart("file", null, ScriptArgumentReader.RequireNonWhiteSpaceString(filesValue, "file"), null, null));
        return parts;
    }

    if (ScriptArgumentReader.IsOptionsObject(filesValue))
    {
        foreach (var item in ReadFileMap(filesValue!))
        {
            parts.Add(new HttpMultipartPart(item.Key, null, item.Value, null, null));
        }

        return parts;
    }

    foreach (var fileValue in ScriptArgumentReader.EnumerateIndexedValues(filesValue))
    {
        parts.Add(ReadMultipartPart(fileValue));
    }
}
```

```markdown
<!-- README.md:141 -->
`files` can be a single path, an object such as `{ file: path }`, or an array of file parts.
```

Existing test pattern:

- `tests/HsWin.Core.Tests/ScriptRuntimeTests.cs:572` has `ReloadExposesHttpRequestWithMultipartFileUpload`, which uses `multipart: [...]` and a `CapturingHttpService`.
- Match that style: load a JavaScript snippet through `ScriptRuntime`, inspect the captured request, and assert parsed multipart parts.

## Commands You Will Need

| Purpose | Command | Expected On Success |
|---------|---------|---------------------|
| Targeted tests | `dotnet test HsWin.slnx --filter "FullyQualifiedName~ScriptRuntimeTests"` | exit 0, relevant `ScriptRuntimeTests` pass |
| Full build | `dotnet build HsWin.slnx` | exit 0, no warnings as errors |
| Full tests | `dotnet test HsWin.slnx` | exit 0, all tests pass |
| Installer | `.\scripts\Build-Installer.ps1` | exit 0, prints `artifacts\installer\hswin-x64-setup.exe` |
| Manual handoff | `Start-Process -FilePath .\artifacts\installer\hswin-x64-setup.exe` | installer starts for the user |

Before final verification, follow repo instructions and stop old app instances:

```powershell
Get-Process -Name "Hammerspoon (Windows Edition)","HsWin.App" -ErrorAction SilentlyContinue | Stop-Process -Force
```

## Scope

**In scope**:

- `src/HsWin.Core/Scripting/HttpScriptApi.cs`
- `src/HsWin.Core/Scripting/ScriptArgumentReader.cs` only if a tiny helper is genuinely needed
- `tests/HsWin.Core.Tests/ScriptRuntimeTests.cs`
- `README.md` only if the final supported behavior differs from the existing docs

**Out of scope**:

- `src/HsWin.Core/Http/SystemHttpService.cs` - transport behavior is not part of this parser bug.
- Adding new HTTP API options.
- Changing `multipart` semantics.
- Changing response parsing or JSON handling.

## Git Workflow

- Branch: `codex/001-fix-http-files-array-parsing`
- Commit message style: conventional commits, for example `fix: parse http files arrays`
- Do not push or open a PR unless the operator instructed it.

## Steps

### Step 1: Add A Regression Test For `files` Arrays

In `tests/HsWin.Core.Tests/ScriptRuntimeTests.cs`, add a test near `ReloadExposesHttpRequestWithMultipartFileUpload`. Suggested name:

`ReloadExposesHttpRequestWithFilesArrayUpload`

Use the existing `CapturingHttpService` and `QueuedScriptCallbackScheduler` pattern. The JavaScript should call:

```js
hs.http.post("https://api.example.test/upload", {
  files: [
    { name: "clip", path: "C:\\Temp\\clip.wav", fileName: "clip.wav", contentType: "audio/wav" },
    { name: "metadata", value: "not-a-file" }
  ]
}, () => {});
```

Assert that `request.Options.Multipart` contains:

- first part: `Name == "clip"`, `Path == @"C:\Temp\clip.wav"`, `FileName == "clip.wav"`, `ContentType == "audio/wav"`
- second part: `Name == "metadata"`, `Value == "not-a-file"`

Also add or confirm a separate test for `files: { clip: "C:\\Temp\\clip.wav" }` so the object-map form remains supported.

**Verify**: `dotnet test HsWin.slnx --filter "FullyQualifiedName~ScriptRuntimeTests"` -> the new array test should fail before the implementation change or expose the current wrong parse. If it unexpectedly passes, STOP and report that the bug may already have been fixed.

### Step 2: Parse Indexed `files` Values Before Object Maps

Update `ReadMultipartParts` so JavaScript arrays in the `files` option are handled as indexed file parts before falling through to object-map parsing.

The safest shape is:

- Keep the existing single-string path behavior first.
- If `filesValue` is a `ScriptObject` with one or more `PropertyIndices`, enumerate indexed values and pass each one to `ReadMultipartPart`.
- Preserve the existing object-map behavior for plain JavaScript objects and C# dictionaries.
- Preserve existing support for non-string .NET enumerables by leaving the final `EnumerateIndexedValues` branch.

Do not treat every `IDictionary` as enumerable first; dictionary enumeration would produce key/value entries instead of file paths.

**Verify**: `dotnet test HsWin.slnx --filter "FullyQualifiedName~ScriptRuntimeTests"` -> exit 0, including the new `files` array test and the existing multipart test.

### Step 3: Confirm README Alignment

Read the Current API section around `hs.http`. If your implementation supports exactly what is already documented, do not change README. If you intentionally support a narrower or broader `files` shape, update `README.md` in the Current API section with a small example.

**Verify**: `git diff -- README.md src/HsWin.Core/Scripting/HttpScriptApi.cs tests/HsWin.Core.Tests/ScriptRuntimeTests.cs` -> diff only covers the parser, focused tests, and any necessary README clarification.

### Step 4: Run Full Repo Verification

Follow the repo handoff loop:

```powershell
Get-Process -Name "Hammerspoon (Windows Edition)","HsWin.App" -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet build HsWin.slnx
dotnet test HsWin.slnx
.\scripts\Build-Installer.ps1
Start-Process -FilePath .\artifacts\installer\hswin-x64-setup.exe
```

**Verify**: build and tests exit 0; installer command creates `artifacts\installer\hswin-x64-setup.exe`; installer starts.

## Test Plan

- New `ScriptRuntimeTests` coverage for `files: [...]` from JavaScript.
- Existing `multipart: [...]` test must still pass.
- Existing or new test for `files: { name: path }` object map must pass.
- Full test suite must pass.

## Done Criteria

- [ ] `files: [{ name, path, fileName, contentType }]` parses into `HttpMultipartPart` with the expected fields.
- [ ] Existing `multipart` parsing still passes.
- [ ] Existing `files` object-map behavior still passes.
- [ ] `dotnet build HsWin.slnx` exits 0.
- [ ] `dotnet test HsWin.slnx` exits 0.
- [ ] `.\scripts\Build-Installer.ps1` exits 0.
- [ ] Installer is launched with `Start-Process`.
- [ ] No files outside the in-scope list are modified unless README needed a documented API clarification.
- [ ] `plans/README.md` status row for 001 is updated.

## STOP Conditions

Stop and report if:

- `ReadMultipartParts` no longer has distinct `multipart` and `files` parsing paths.
- ClearScript no longer exposes JavaScript array indices through `ScriptObject.PropertyIndices`; do not guess a replacement.
- The fix requires changing `HttpRequestOptions` or `SystemHttpService`.
- Targeted tests still fail twice after the implementation change.

## Maintenance Notes

The risky part of this parser is that JavaScript arrays are also host objects. Future parser changes should add tests from real JavaScript snippets, not just C# dictionaries or arrays, because C# inputs do not exercise the ClearScript object model.
