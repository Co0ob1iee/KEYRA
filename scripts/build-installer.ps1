<#
.SYNOPSIS
  Compiles the KEYRA Inno Setup installer from a published win-x64 folder.

.PARAMETER Version
  SemVer used in AppVersion and the Setup.exe file name (e.g. 1.1.0).
  Defaults to GITHUB_REF_NAME without a leading 'v', then Directory.Build.props.

.EXAMPLE
  pwsh scripts/build-installer.ps1 -Version 1.1.0
#>
[CmdletBinding()]
param(
    [string] $Version,
    [string] $SourceDir,
    [string] $IssPath
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot

if (-not $Version) {
    if ($env:GITHUB_REF_NAME -match '^v?.+$') {
        $Version = $env:GITHUB_REF_NAME.TrimStart('v')
    }
    if (-not $Version) {
        $propsPath = Join-Path $repoRoot 'Directory.Build.props'
        $props = Get-Content -LiteralPath $propsPath -Raw
        if ($props -notmatch '<Version>(\d+\.\d+\.\d+)</Version>') {
            throw 'Could not determine version (pass -Version or set Directory.Build.props).'
        }
        $Version = $Matches[1]
    }
}

if (-not $SourceDir) {
    $SourceDir = Join-Path $repoRoot 'dist\win-x64'
}
if (-not $IssPath) {
    $IssPath = Join-Path $repoRoot 'installer\keyra.iss'
}

if (-not (Test-Path -LiteralPath $IssPath)) {
    throw "Inno Setup script not found: $IssPath"
}

$appExe = Join-Path $SourceDir 'SshKeyManager.exe'
if (-not (Test-Path -LiteralPath $appExe)) {
    throw "Published app not found: $appExe. Run dotnet publish into dist\win-x64 first."
}

function Find-Iscc {
    $pf86 = ${env:ProgramFiles(x86)}
    $candidates = @()
    if ($pf86) {
        $candidates += (Join-Path $pf86 'Inno Setup 6\ISCC.exe')
    }
    if ($env:ProgramFiles) {
        $candidates += (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    }
    foreach ($path in $candidates) {
        if (Test-Path -LiteralPath $path) {
            return $path
        }
    }
    $cmd = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
    if ($cmd) {
        return $cmd.Source
    }
    return $null
}

$iscc = Find-Iscc
if (-not $iscc) {
    throw 'Inno Setup 6 compiler (ISCC.exe) not found. CI installs it via Chocolatey (innosetup). Locally: https://jrsoftware.org/isinfo.php'
}

$outDir = Join-Path $repoRoot 'dist\installer'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

Write-Host "Compiling KEYRA $Version installer with $iscc"

& $iscc /Q "/DMyAppVersion=$Version" $IssPath
if ($LASTEXITCODE -ne 0) {
    throw "ISCC.exe exited with code $LASTEXITCODE"
}

$setup = Join-Path $outDir "KEYRA-$Version-win-x64-setup.exe"
if (-not (Test-Path -LiteralPath $setup)) {
    throw "Expected installer was not produced: $setup"
}

Write-Host "Installer: $setup"
