<#
.SYNOPSIS
    Generates the three winget manifest files (version, installer, defaultLocale) for a release.
.DESCRIPTION
    Writes winget/manifests/k/kud3n013/ProtonPassCliExtension/<version>/. InstallerSha256 is the real SHA256 of the
    installers: by default they are downloaded from the GitHub release (so the hash is of the *uploaded* file);
    use -ArtifactsDir to hash local builds instead (e.g. to try `winget validate` before a release exists).
    The output folder is gitignored because the hashes belong to one specific upload.
.EXAMPLE
    .\scripts\new-winget-manifest.ps1 -Version 1.0.0
    .\scripts\new-winget-manifest.ps1 -Version 1.0.0 -ArtifactsDir .\artifacts
#>
param(
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version,
    [string]$ArtifactsDir,
    [string]$OutRoot
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
if (-not $OutRoot) { $OutRoot = Join-Path $root "winget\manifests" }

$id = "kud3n013.ProtonPassCliExtension"
$repo = "https://github.com/kud3n013/command-palette-pass-cli-extention"
$tag = "v$Version"
$schema = "1.10.0"
$packageName = "Pass CLI for Command Palette (unofficial)"
# Inno registers its uninstall entry as "<AppId>_is1" (AppId from installer/ProtonPassCliExtension.iss).
$productCode = "{2A158EE8-5834-42CE-9837-A00C5987E3D6}_is1"

$outDir = Join-Path $OutRoot "k\kud3n013\ProtonPassCliExtension\$Version"
New-Item -ItemType Directory -Force $outDir | Out-Null

function Get-InstallerInfo([string]$arch) {
    $name = "ProtonPassCliExtension-$Version-$arch.exe"
    $url = "$repo/releases/download/$tag/$name"
    if ($ArtifactsDir) {
        $file = Join-Path $ArtifactsDir $name
        if (-not (Test-Path $file)) { throw "Missing $file" }
    } else {
        $file = Join-Path ([IO.Path]::GetTempPath()) "winget-$([guid]::NewGuid())-$name"
        Write-Host "Downloading $url"
        Invoke-WebRequest -Uri $url -OutFile $file -MaximumRedirection 5
    }
    $hash = (Get-FileHash $file -Algorithm SHA256).Hash.ToUpper()
    if (-not $ArtifactsDir) { Remove-Item $file -Force }
    [pscustomobject]@{ Arch = $arch; Url = $url; Sha = $hash }
}

$x64 = Get-InstallerInfo "x64"
$arm64 = Get-InstallerInfo "arm64"
$releaseDate = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd")

$files = @{}

$files["$id.yaml"] = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.version.$schema.schema.json
PackageIdentifier: $id
PackageVersion: $Version
DefaultLocale: en-US
ManifestType: version
ManifestVersion: $schema
"@

$files["$id.installer.yaml"] = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.installer.$schema.schema.json
PackageIdentifier: $id
PackageVersion: $Version
InstallerLocale: en-US
MinimumOSVersion: 10.0.19041.0
InstallerType: inno
Scope: user
InstallModes:
- silent
- silentWithProgress
UpgradeBehavior: install
ProductCode: '$productCode'
ReleaseDate: $releaseDate
Dependencies:
  PackageDependencies:
  - PackageIdentifier: Proton.ProtonPass.CLI
Installers:
- Architecture: x64
  InstallerUrl: $($x64.Url)
  InstallerSha256: $($x64.Sha)
- Architecture: arm64
  InstallerUrl: $($arm64.Url)
  InstallerSha256: $($arm64.Sha)
ManifestType: installer
ManifestVersion: $schema
"@

$files["$id.locale.en-US.yaml"] = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.defaultLocale.$schema.schema.json
PackageIdentifier: $id
PackageVersion: $Version
PackageLocale: en-US
Publisher: kud3n013
PublisherUrl: https://github.com/kud3n013
PublisherSupportUrl: $repo/issues
PackageName: $packageName
PackageUrl: $repo
License: MIT
LicenseUrl: $repo/blob/main/LICENSE
ShortDescription: Unofficial PowerToys Command Palette extension for the Proton Pass CLI.
Description: |-
  An unofficial PowerToys Command Palette extension that wraps the Proton Pass CLI (pass-cli) so you can search
  your vaults and copy passwords, usernames and TOTP codes from the keyboard.
  The Proton Pass CLI is required and must be installed and logged in ("pass-cli login"). This project is not
  affiliated with, endorsed by, or supported by Proton AG.
Moniker: cmdpal-pass-cli
Tags:
- windows-commandpalette-extension
- command-palette
- powertoys
- password-manager
- proton-pass
ReleaseNotesUrl: $repo/releases/tag/$tag
ManifestType: defaultLocale
ManifestVersion: $schema
"@

foreach ($name in $files.Keys) {
    # winget-pkgs manifests are UTF-8 without BOM.
    [IO.File]::WriteAllText((Join-Path $outDir $name), ($files[$name] -replace "`r`n", "`n") + "`n", (New-Object Text.UTF8Encoding($false)))
}

Write-Host "Wrote manifests to $outDir"
Write-Host "  x64   $($x64.Sha)"
Write-Host "  arm64 $($arm64.Sha)"
