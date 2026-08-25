<#
.SYNOPSIS
  Publishes KEYRA (win-x64 self-contained) and builds a per-user Setup.exe locally.
  Does not use GitHub Actions.

.PARAMETER Version
  SemVer for the installer file name. Defaults to Directory.Build.props.

.EXAMPLE
  pwsh scripts/publish.ps1
  pwsh scripts/publish.ps1 -Version 1.2.0
#>
[CmdletBinding()]
param(
    [string] $Version
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $repoRoot

if (-not $Version) {
    $props = Get-Content -LiteralPath (Join-Path $repoRoot 'Directory.Build.props') -Raw
    if ($props -notmatch '<Version>(\d+\.\d+\.\d+)</Version>') {
        throw 'Could not read <Version> from Directory.Build.props. Pass -Version 1.2.0'
    }
    $Version = $Matches[1]
}

$publishDir = Join-Path $repoRoot 'dist\win-x64'
$installerDir = Join-Path $repoRoot 'dist\installer'
$issPath = Join-Path $repoRoot 'keyra-setup.iss'
$csproj = Join-Path $repoRoot 'src\SshKeyManager\SshKeyManager.csproj'

Write-Host "=== KEYRA $Version : publish ==="
dotnet publish $csproj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $publishDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$appExe = Join-Path $publishDir 'SshKeyManager.exe'
if (-not (Test-Path -LiteralPath $appExe)) {
    throw "Published exe not found: $appExe"
}

function Find-Iscc {
    $pf86 = ${env:ProgramFiles(x86)}
    foreach ($path in @(
            $(if ($pf86) { Join-Path $pf86 'Inno Setup 6\ISCC.exe' }),
            $(if ($env:ProgramFiles) { Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe' }),
            $(if ($env:LOCALAPPDATA) { Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe' })
        )) {
        if ($path -and (Test-Path -LiteralPath $path)) { return $path }
    }
    $cmd = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}

function Install-IsccFromNuget {
    $toolsDir = Join-Path $repoRoot '.tools\innosetup'
    $cached = Join-Path $toolsDir 'tools\ISCC.exe'
    if (Test-Path -LiteralPath $cached) { return $cached }

    Write-Host "=== Downloading Inno Setup compiler (NuGet Tools.InnoSetup) ==="
    New-Item -ItemType Directory -Force -Path $toolsDir | Out-Null
    $nupkg = Join-Path $toolsDir 'Tools.InnoSetup.zip'
    Invoke-WebRequest -Uri 'https://www.nuget.org/api/v2/package/Tools.InnoSetup/6.4.3' -OutFile $nupkg -UseBasicParsing
    Expand-Archive -LiteralPath $nupkg -DestinationPath $toolsDir -Force
    if (-not (Test-Path -LiteralPath $cached)) {
        throw "ISCC.exe missing after NuGet extract: $cached"
    }
    return $cached
}

$iscc = Find-Iscc
if (-not $iscc) {
    $iscc = Install-IsccFromNuget
}

Write-Host "=== Compiling installer with $iscc ==="
New-Item -ItemType Directory -Force -Path $installerDir | Out-Null

$iconLine = ''
$icon = Join-Path $repoRoot 'src\SshKeyManager\Assets\keyra-icon.ico'
if (Test-Path -LiteralPath $icon) {
    $iconLine = 'SetupIconFile=src\SshKeyManager\Assets\keyra-icon.ico'
}
$appId = '{' + '{8F3A1C2E-9B47-4D6A-A1E8-0C5B7F2D4E91}'
$issLines = @(
    '#ifndef MyAppVersion'
    '#define MyAppVersion "0.0.0"'
    '#endif'
    '#define MyAppName "KEYRA"'
    '#define MyAppPublisher "KEYRA"'
    '#define MyAppExeName "SshKeyManager.exe"'
    '#define MyAppVersionInfo MyAppVersion + ".0"'
    ''
    '[Setup]'
    "AppId=$appId"
    'AppName={#MyAppName}'
    'AppVersion={#MyAppVersion}'
    'AppVerName={#MyAppName} {#MyAppVersion}'
    'AppPublisher={#MyAppPublisher}'
    'AppCopyright=Copyright (c) 2026 KEYRA contributors'
    'VersionInfoVersion={#MyAppVersionInfo}'
    'VersionInfoProductName={#MyAppName}'
    'VersionInfoProductVersion={#MyAppVersionInfo}'
    'DefaultDirName={localappdata}\Programs\KEYRA'
    'DefaultGroupName={#MyAppName}'
    'DisableProgramGroupPage=yes'
    'LicenseFile=LICENSE'
    'OutputDir=dist\installer'
    'OutputBaseFilename=KEYRA-{#MyAppVersion}-win-x64-setup'
    $iconLine
    'UninstallDisplayIcon={app}\{#MyAppExeName}'
    'Compression=lzma2/ultra64'
    'SolidCompression=yes'
    'WizardStyle=modern'
    'PrivilegesRequired=lowest'
    'ArchitecturesAllowed=x64compatible'
    'ArchitecturesInstallIn64BitMode=x64compatible'
    'MinVersion=10.0'
    'CloseApplications=yes'
    'RestartApplications=no'
    'UninstallDisplayName={#MyAppName}'
    'UsePreviousAppDir=yes'
    ''
    '[Languages]'
    'Name: "english"; MessagesFile: "compiler:Default.isl"'
    'Name: "polish"; MessagesFile: "compiler:Languages\Polish.isl"'
    ''
    '[Tasks]'
    'Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked'
    ''
    '[Files]'
    'Source: "dist\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"'
    ''
    '[Icons]'
    'Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Comment: "SSH key vault and SSH client"'
    'Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Comment: "SSH key vault and SSH client"; Tasks: desktopicon'
    ''
    '[Run]'
    'Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, ''&'', ''&&'')}}"; Flags: nowait postinstall skipifsilent'
)
[System.IO.File]::WriteAllText($issPath, (($issLines -join "`r`n") + "`r`n"))

& $iscc /Q "/DMyAppVersion=$Version" $issPath
if ($LASTEXITCODE -ne 0) {
    throw "ISCC.exe exited with code $LASTEXITCODE"
}

$setup = Join-Path $installerDir "KEYRA-$Version-win-x64-setup.exe"
$zip = Join-Path $repoRoot "KEYRA-$Version-win-x64.zip"
if (-not (Test-Path -LiteralPath $setup)) {
    throw "Installer was not produced: $setup"
}

if (Test-Path -LiteralPath $zip) {
    Remove-Item -LiteralPath $zip -Force
}
Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zip -Force

Write-Host ""
Write-Host "Gotowe."
Write-Host "  Instalator: $setup"
Write-Host "  ZIP:        $zip"
Write-Host "Wgraj te dwa pliki recznie na GitHub -> Releases -> Edit przy Build-1.2.0 -> Attach."
