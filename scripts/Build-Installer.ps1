[CmdletBinding()]
param(
    [string] $Configuration = "Release",
    [string] $RuntimeIdentifier = "win-x64",
    [string] $Version = "",
    [string] $VersionInfoVersion = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$scriptRoot = Split-Path -Parent $PSCommandPath
$repoRoot = Resolve-Path (Join-Path $scriptRoot "..")
$projectPath = Join-Path $repoRoot "src\HsWin.App\HsWin.App.csproj"
$installerScriptPath = Join-Path $repoRoot "installer\HsWin.iss"
$artifactsRoot = Join-Path $repoRoot "artifacts"
$publishDir = Join-Path $artifactsRoot "publish\HsWin.App\$Configuration\$RuntimeIdentifier"
$installerOutputDir = Join-Path $artifactsRoot "installer"

if ([string]::IsNullOrWhiteSpace($Version)) {
    $now = [System.DateTimeOffset]::Now
    $Version = "0.{0:MMdd}.{0:HHmm}.{0:ss}" -f $now
}

if ([string]::IsNullOrWhiteSpace($VersionInfoVersion)) {
    if ($Version -match '^\d+$') {
        $VersionInfoVersion = "0.0.0.$Version"
    }
    elseif ($Version -match '^\d+\.\d+\.\d+$') {
        $VersionInfoVersion = "$Version.0"
    }
    elseif ($Version -match '^\d+\.\d+\.\d+\.\d+$') {
        $VersionInfoVersion = $Version
    }
    else {
        $VersionInfoVersion = "0.0.0.0"
    }
}

function Assert-ChildPath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Parent,

        [Parameter(Mandatory = $true)]
        [string] $Child
    )

    $resolvedParent = [System.IO.Path]::GetFullPath($Parent).TrimEnd('\') + '\'
    $resolvedChild = [System.IO.Path]::GetFullPath($Child)

    if (-not $resolvedChild.StartsWith($resolvedParent, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to operate on '$resolvedChild' because it is outside '$resolvedParent'."
    }
}

function Get-InnoSetupCompiler {
    $candidates = @()

    if ($env:INNO_SETUP_ISCC) {
        $candidates += $env:INNO_SETUP_ISCC
    }

    $pathCommand = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
    if ($pathCommand) {
        $candidates += $pathCommand.Source
    }

    $candidates += @(
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 7\ISCC.exe"),
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
        "C:\Program Files\Inno Setup 7\ISCC.exe",
        "C:\Program Files (x86)\Inno Setup 7\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe",
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
    )

    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path -LiteralPath $candidate)) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    throw "Inno Setup Compiler (ISCC.exe) was not found. Install Inno Setup or set INNO_SETUP_ISCC to the full path of ISCC.exe."
}

Assert-ChildPath -Parent $repoRoot -Child $publishDir
Assert-ChildPath -Parent $repoRoot -Child $installerOutputDir

if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
New-Item -ItemType Directory -Path $installerOutputDir -Force | Out-Null

dotnet publish $projectPath `
    --configuration $Configuration `
    --runtime $RuntimeIdentifier `
    --self-contained true `
    --output $publishDir `
    -p:PublishSingleFile=false

$iscc = Get-InnoSetupCompiler

$previousVersion = $env:HSWIN_VERSION
$previousVersionInfoVersion = $env:HSWIN_VERSION_INFO_VERSION
$previousPublishDir = $env:HSWIN_PUBLISH_DIR
$previousOutputDir = $env:HSWIN_OUTPUT_DIR

try {
    $env:HSWIN_VERSION = $Version
    $env:HSWIN_VERSION_INFO_VERSION = $VersionInfoVersion
    $env:HSWIN_PUBLISH_DIR = $publishDir
    $env:HSWIN_OUTPUT_DIR = $installerOutputDir

    & $iscc $installerScriptPath
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup Compiler failed with exit code $LASTEXITCODE."
    }
}
finally {
    $env:HSWIN_VERSION = $previousVersion
    $env:HSWIN_VERSION_INFO_VERSION = $previousVersionInfoVersion
    $env:HSWIN_PUBLISH_DIR = $previousPublishDir
    $env:HSWIN_OUTPUT_DIR = $previousOutputDir
}

$installerPath = Join-Path $installerOutputDir "hswin-x64-setup.exe"
if (-not (Test-Path -LiteralPath $installerPath)) {
    throw "Expected installer was not created: $installerPath"
}

Write-Output $installerPath
