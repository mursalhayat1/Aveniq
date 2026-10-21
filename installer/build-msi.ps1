<#
.SYNOPSIS
  Builds the per-machine Aveniq MSI from an already-published win-x64 payload.

.DESCRIPTION
  The payload is produced by build-release.ps1. This script only packages the
  existing files; it does not publish, install, or modify user data.

.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File installer\build-msi.ps1 -Version 0.8.0
#>
param(
    [string]$Version = "0.8.0",
    [string]$SourceDir,
    [string]$OutputDir,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repo = (Resolve-Path "$PSScriptRoot\..").Path

if (-not $SourceDir) {
    $SourceDir = Join-Path $repo "dist\Aveniq-$Version-win-x64"
}
if (-not (Test-Path -LiteralPath $SourceDir)) {
    throw "Published payload not found: $SourceDir. Run installer\build-release.ps1 first."
}
$SourceDir = (Resolve-Path -LiteralPath $SourceDir).Path
if (-not (Test-Path -LiteralPath (Join-Path $SourceDir "Aveniq.exe"))) {
    throw "The payload does not contain Aveniq.exe: $SourceDir"
}
if (-not (Test-Path -LiteralPath (Join-Path $SourceDir "cli\aveniq.exe"))) {
    throw "The payload does not contain cli\aveniq.exe: $SourceDir"
}

if (-not $OutputDir) {
    $OutputDir = Join-Path $repo "dist"
}
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$OutputDir = (Resolve-Path -LiteralPath $OutputDir).Path

$icon = Join-Path $repo "src\Aveniq.Desktop\Assets\aveniq.ico"
if (-not (Test-Path -LiteralPath $icon)) {
    throw "Application icon not found: $icon"
}

$project = Join-Path $PSScriptRoot "Aveniq.Msi.wixproj"
if (-not (Test-Path -LiteralPath $project)) {
    throw "WiX project not found: $project"
}

$msiName = "Aveniq-$Version-x64.msi"
$msi = Join-Path $OutputDir $msiName
$buildDir = Join-Path ([System.IO.Path]::GetTempPath()) ("Aveniq-msi-" + [guid]::NewGuid().ToString("N"))

Write-Host "== Aveniq $Version MSI ==" -ForegroundColor Cyan
Write-Host "Payload: $SourceDir"
try {
    New-Item -ItemType Directory -Force -Path $buildDir | Out-Null
    dotnet build $project -c $Configuration -t:Rebuild --nologo -v:minimal `
        "-p:ProductVersion=$Version" `
        "-p:PayloadDir=$SourceDir" `
        "-p:IconPath=$icon" `
        "-p:OutputPath=$buildDir\"
    if ($LASTEXITCODE -ne 0) {
        throw "WiX MSI build failed"
    }

    $builtMsi = Join-Path $buildDir $msiName
    if (-not (Test-Path -LiteralPath $builtMsi)) {
        throw "WiX did not produce $msiName"
    }
    # A real self-contained Aveniq MSI is well over 10 MB. Reject a stale/empty
    # intermediate rather than publishing a package that does not contain the payload.
    if ((Get-Item -LiteralPath $builtMsi).Length -lt 10MB) {
        throw "The built MSI is unexpectedly small; WiX may have reused stale output."
    }

    Copy-Item -LiteralPath $builtMsi -Destination $msi -Force
}
finally {
    if (Test-Path -LiteralPath $buildDir) {
        Remove-Item -LiteralPath $buildDir -Recurse -Force
    }
}

$hash = (Get-FileHash -LiteralPath $msi -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath "$msi.sha256" -Value "$hash  $msiName"
$size = [math]::Round((Get-Item -LiteralPath $msi).Length / 1MB, 1)
Write-Host "MSI: $msi ($size MB)" -ForegroundColor Green
Write-Host "SHA256: $hash" -ForegroundColor Green
