# WinGet packaging

The package identifier is `nocdn.HammerspoonWindows`. Microsoft requires one version, installer, and default-locale manifest for every package release. The templates in this directory use the recommended WinGet 1.12 multi-file schema.

`scripts/Build-WinGetManifests.ps1` fills in the release version, immutable GitHub Release URL, and SHA-256 hash from the exact installer binary. The release workflow publishes the generated `manifests/n/nocdn/HammerspoonWindows/<version>` directory as both a workflow artifact and a ZIP attached to the GitHub release.

## First submission

After a release containing this packaging support succeeds:

1. Download and extract `winget-manifests-v<version>.zip` from that GitHub release.
2. Validate the extracted version directory:

   ```powershell
   winget validate --manifest .\manifests\n\nocdn\HammerspoonWindows\<version>
   ```

3. Enable local manifests from an elevated terminal and test the real installation:

   ```powershell
   winget settings --enable LocalManifestFiles
   winget install --manifest .\manifests\n\nocdn\HammerspoonWindows\<version>
   ```

4. Install Microsoft's submission tool, authenticate, and submit the same version directory without a trailing slash:

   ```powershell
   winget install Microsoft.WingetCreate
   wingetcreate token -s
   wingetcreate submit .\manifests\n\nocdn\HammerspoonWindows\<version>
   ```

The submission opens a pull request against `microsoft/winget-pkgs`. Monitor its validation labels and respond to any reviewer feedback. Once Microsoft merges and publishes it, users can install with:

```powershell
winget install --exact --id nocdn.HammerspoonWindows
```

## Later versions

The release artifact remains useful for local validation, but WingetCreate can submit updates directly once the initial package exists:

```powershell
wingetcreate update nocdn.HammerspoonWindows `
  --urls https://github.com/nocdn/hammerspoon-win/releases/download/v<version>/hswin-x64-setup.exe `
  --version <version> `
  --submit
```
