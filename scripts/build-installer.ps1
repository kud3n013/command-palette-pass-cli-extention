<#
.SYNOPSIS
    Builds the unpackaged extension and wraps it in an Inno Setup installer.
.DESCRIPTION
    For each platform: dotnet publish (Release, self-contained, unpackaged) then iscc.exe.
    Writes ProtonPassCliExtension-<version>-<arch>.exe and SHA256SUMS.txt to artifacts/.
.EXAMPLE
    .\scripts\build-installer.ps1 -Platform x64
    .\scripts\build-installer.ps1 -Version 1.0.0 -Platform all
#>
param(
    # Defaults to <Version> in ProtonPassCliExtension/Directory.Build.props.
    [string]$Version,
    [ValidateSet("x64", "arm64", "all")]
    [string]$Platform = "all",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

$global:LASTEXITCODE = 0
$projectVersion = & (Join-Path $PSScriptRoot "check-version.ps1")
if ($LASTEXITCODE -ne 0) { throw "Version check failed" }
if (-not $Version) { $Version = $projectVersion }
elseif ($Version -ne $projectVersion) { throw "-Version $Version does not match the project version $projectVersion; bump Directory.Build.props instead." }

$iscc = @(
    (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
    (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe"),
    (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe")
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 (ISCC.exe) not found. Install it: winget install JRSoftware.InnoSetup" }

$project = Join-Path $root "ProtonPassCliExtension\ProtonPassCliExtension\ProtonPassCliExtension.csproj"
$artifacts = Join-Path $root "artifacts"
New-Item -ItemType Directory -Force $artifacts | Out-Null

$platforms = if ($Platform -eq "all") { @("x64", "arm64") } else { @($Platform) }
foreach ($arch in $platforms) {
    Write-Host "`n=== $arch ($Version) ===" -ForegroundColor Cyan
    $publishDir = Join-Path $artifacts "publish-$arch"
    Remove-Item -Recurse -Force $publishDir -ErrorAction SilentlyContinue

    # WindowsPackageType=None gives the unpackaged exe the installer needs, and PublishProfile= ignores the
    # packaged pubxml. They are passed here instead of edited into the csproj so the packaged dev flow
    # (Add-AppxPackage -Register) keeps working.
    dotnet publish $project -c $Configuration -r "win-$arch" -p:Platform=$arch -p:WindowsPackageType=None `
        -p:PublishProfile= --self-contained true -o $publishDir
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $arch" }

    & $iscc "/DAppVersion=$Version" "/DArch=$arch" "/DPublishDir=$publishDir" "/DOutputDir=$artifacts" `
        (Join-Path $root "installer\ProtonPassCliExtension.iss")
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed for $arch" }

    Remove-Item -Recurse -Force $publishDir
}

$installers = Get-ChildItem $artifacts -Filter "ProtonPassCliExtension-$Version-*.exe" | Sort-Object Name
$sums = $installers | ForEach-Object { "{0}  {1}" -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower(), $_.Name }
Set-Content -Path (Join-Path $artifacts "SHA256SUMS.txt") -Value $sums -Encoding ascii

Write-Host "`nArtifacts:" -ForegroundColor Green
$sums | ForEach-Object { Write-Host "  $_" }
