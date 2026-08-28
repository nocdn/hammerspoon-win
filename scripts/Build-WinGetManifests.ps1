[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $Version,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $InstallerPath,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $InstallerUrl,

    [string] $OutputDirectory = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$packageIdentifier = "nocdn.HammerspoonWindows"
$scriptRoot = Split-Path -Parent $PSCommandPath
$repoRoot = (Resolve-Path (Join-Path $scriptRoot "..")).Path
$templateRoot = Join-Path $repoRoot "packaging\winget\templates"
$resolvedInstallerPath = (Resolve-Path -LiteralPath $InstallerPath).Path
$normalizedVersion = $Version.Trim()

if ($normalizedVersion -notmatch '^[0-9A-Za-z][0-9A-Za-z._+-]*$') {
    throw "WinGet package version '$Version' contains unsupported characters."
}

$installerUri = $null
if (-not [Uri]::TryCreate($InstallerUrl, [UriKind]::Absolute, [ref] $installerUri) -or
    $installerUri.Scheme -ne [Uri]::UriSchemeHttps) {
    throw "WinGet installer URL must be an absolute HTTPS URL."
}

if ([System.IO.Path]::GetExtension($resolvedInstallerPath) -ne ".exe") {
    throw "Expected an .exe installer, but received '$resolvedInstallerPath'."
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts\winget"
}

$manifestDirectory = Join-Path $OutputDirectory "manifests\n\nocdn\HammerspoonWindows\$normalizedVersion"
New-Item -ItemType Directory -Path $manifestDirectory -Force | Out-Null

$installerSha256 = (Get-FileHash -LiteralPath $resolvedInstallerPath -Algorithm SHA256).Hash
$replacements = [ordered]@{
    "{{PACKAGE_VERSION}}" = $normalizedVersion
    "{{INSTALLER_URL}}" = $installerUri.AbsoluteUri
    "{{INSTALLER_SHA256}}" = $installerSha256
}

$templates = [ordered]@{
    "$packageIdentifier.yaml" = "$packageIdentifier.yaml"
    "$packageIdentifier.installer.yaml" = "$packageIdentifier.installer.yaml"
    "$packageIdentifier.locale.en-US.yaml" = "$packageIdentifier.locale.en-US.yaml"
}

$utf8WithoutBom = [System.Text.UTF8Encoding]::new($false)

foreach ($entry in $templates.GetEnumerator()) {
    $templatePath = Join-Path $templateRoot $entry.Value
    if (-not (Test-Path -LiteralPath $templatePath -PathType Leaf)) {
        throw "WinGet manifest template was not found: $templatePath"
    }

    $content = [System.IO.File]::ReadAllText($templatePath)
    foreach ($replacement in $replacements.GetEnumerator()) {
        $content = $content.Replace($replacement.Key, $replacement.Value)
    }

    if ($content -match '{{[A-Z0-9_]+}}') {
        throw "WinGet manifest template '$templatePath' contains an unresolved token '$($Matches[0])'."
    }

    $outputPath = Join-Path $manifestDirectory $entry.Key
    [System.IO.File]::WriteAllText($outputPath, $content, $utf8WithoutBom)
}

Write-Output (Resolve-Path -LiteralPath $manifestDirectory).Path
