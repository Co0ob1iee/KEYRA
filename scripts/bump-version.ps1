<#
.SYNOPSIS
  Bumps KEYRA SemVer in Directory.Build.props and prepends a CHANGELOG stub.

.PARAMETER Part
  Which SemVer component to increment: patch, minor, or major.

.EXAMPLE
  pwsh scripts/bump-version.ps1 -Part patch
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('patch', 'minor', 'major')]
    [string] $Part
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$propsPath = Join-Path $repoRoot 'Directory.Build.props'
$changelogPath = Join-Path $repoRoot 'CHANGELOG.md'

if (-not (Test-Path -LiteralPath $propsPath)) {
    throw "Directory.Build.props not found: $propsPath"
}

$props = Get-Content -LiteralPath $propsPath -Raw
if ($props -notmatch '<Version>(\d+)\.(\d+)\.(\d+)</Version>') {
    throw 'Could not parse <Version>MAJOR.MINOR.PATCH</Version> from Directory.Build.props'
}

$major = [int]$Matches[1]
$minor = [int]$Matches[2]
$patch = [int]$Matches[3]

switch ($Part) {
    'major' { $major++; $minor = 0; $patch = 0 }
    'minor' { $minor++; $patch = 0 }
    'patch' { $patch++ }
}

$newVersion = "$major.$minor.$patch"
$fourPart = "$major.$minor.$patch.0"

$props = [regex]::Replace($props, '<Version>\d+\.\d+\.\d+</Version>', "<Version>$newVersion</Version>")
$props = [regex]::Replace($props, '<AssemblyVersion>\d+\.\d+\.\d+\.\d+</AssemblyVersion>', "<AssemblyVersion>$fourPart</AssemblyVersion>")
$props = [regex]::Replace($props, '<FileVersion>\d+\.\d+\.\d+\.\d+</FileVersion>', "<FileVersion>$fourPart</FileVersion>")
$props = [regex]::Replace($props, '<InformationalVersion>[^<]+</InformationalVersion>', "<InformationalVersion>$newVersion</InformationalVersion>")

Set-Content -LiteralPath $propsPath -Value $props -NoNewline

$today = Get-Date -Format 'yyyy-MM-dd'
$stub = @"
## [$newVersion] - $today

### Added

### Changed

### Fixed

"@

if (Test-Path -LiteralPath $changelogPath) {
    $changelog = Get-Content -LiteralPath $changelogPath -Raw
    # Insert new version section after [Unreleased] body, before the next ## [version] heading.
    $pattern = '(?ms)(^## \[Unreleased\].*?)(?=^## \[)'
    if ([regex]::IsMatch($changelog, $pattern)) {
        $newChangelog = [regex]::Replace($changelog, $pattern, "`$1$stub`r`n", 1)
    }
    else {
        $newChangelog = $changelog.TrimEnd() + "`r`n`r`n" + $stub
    }

    Set-Content -LiteralPath $changelogPath -Value ($newChangelog.TrimEnd() + "`r`n") -NoNewline
}
else {
    $initial = @"
# Changelog

All notable changes to KEYRA are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

$stub
"@
    Set-Content -LiteralPath $changelogPath -Value $initial -NoNewline
}

Write-Host "Bumped KEYRA to $newVersion ($Part)."
Write-Host "  Updated: Directory.Build.props"
Write-Host "  Updated: CHANGELOG.md (stub for [$newVersion])"
Write-Host "Next: fill CHANGELOG notes, then tag v$newVersion and publish (see PUBLISH.md)."
