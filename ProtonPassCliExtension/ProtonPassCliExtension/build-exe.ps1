<#
.SYNOPSIS
    Builds x64 and ARM64 EXE installers (dotnet publish + Inno Setup) for WinGet.
.EXAMPLE
    .\build-exe.ps1 -Version 0.1.0
#>
param(
    [string]$Configuration = "Release",
    [string]$Version = "0.1.0"
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$iscc = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $iscc)) { throw "Inno Setup not found at $iscc (winget install JRSoftware.InnoSetup)" }

foreach ($arch in @("x64", "arm64")) {
    Write-Host "`n=== $arch ===" -ForegroundColor Cyan
    Remove-Item -Recurse -Force "publish" -ErrorAction SilentlyContinue

    # WindowsPackageType=None builds the unpackaged exe without touching the csproj, which the
    # loose-layout (Add-AppxPackage -Register) dev flow still relies on.
    dotnet publish -c $Configuration -r "win-$arch" -p:Platform=$arch -p:WindowsPackageType=None `
        -p:PublishProfile= --self-contained true -o publish
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $arch" }

    & $iscc "/DMyAppVersion=$Version" "/DArchitecturesAllowed=$arch" setup-template.iss
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed for $arch" }
}

Remove-Item -Recurse -Force "publish" -ErrorAction SilentlyContinue
Get-ChildItem Installer -Filter *.exe | ForEach-Object { Write-Host $_.FullName }
