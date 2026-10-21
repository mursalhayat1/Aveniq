<#
.SYNOPSIS
  Builds the Aveniq release: self-contained win-x64 publish of the desktop app and the aveniq CLI into one folder,
  bundles the checksum-verified rclone binary, documentation and install/uninstall scripts, and zips the result.
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File installer\build-release.ps1
#>
param(
    [string]$Configuration = "Release",
    [string]$Version = "0.7.0",
    [string]$WinFspVersion = "2.1.25156",
    [switch]$SkipTests
)
$ErrorActionPreference = "Stop"
$repo = Resolve-Path "$PSScriptRoot\.."
$dist = Join-Path $repo "dist"
$name = "Aveniq-$Version-win-x64"
$stage = Join-Path $dist $name

Write-Host "== Aveniq $Version release build ==" -ForegroundColor Cyan
if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
New-Item -ItemType Directory -Force $stage | Out-Null

# 1. Verify the official rclone and the separately pinned community TeraBox backend before shipping either.
$rclone = Join-Path $repo "tools\rclone\rclone.exe"
$sums = Get-ChildItem (Join-Path $repo "tools\rclone") -Filter "SHA256SUMS.v*" | Select-Object -First 1
if (-not (Test-Path $rclone) -or -not $sums) { throw "tools\rclone\rclone.exe or SHA256SUMS missing. Run installer\fetch-rclone.ps1 first." }
$rcloneVersion = ($sums.Name -replace '^SHA256SUMS\.', '')
& $rclone version | Select-Object -First 1 | Write-Host

$teraboxRclone = Join-Path $repo "tools\rclone-terabox\rclone.exe"
$teraboxReadme = Join-Path $repo "tools\rclone-terabox\README.txt"
if (-not (Test-Path $teraboxRclone) -or -not (Test-Path $teraboxReadme)) {
    throw "Pinned TeraBox backend missing. Run installer\fetch-rclone-terabox.ps1 first."
}
$teraboxMetadata = Get-Content -LiteralPath $teraboxReadme -Raw
if ($teraboxMetadata -notmatch 'Verified commit:\s*56bf13885494819bc801302ddf881279bdac7262' -or
    $teraboxMetadata -notmatch 'Release archive SHA-256 .*:\s*d5a5d9383a607ccb1af9a871d4cb92906121f32cf111497adb94958b99315c69' -or
    $teraboxMetadata -notmatch 'Executable SHA-256:\s*e2667b5040f9dbdbd0a457ecbdeeff96670e93e338ae7afa1743b88c73f6800f') {
    throw "TeraBox backend provenance metadata is missing or does not match the pinned release."
}
$teraboxBinaryHash = (Get-FileHash -LiteralPath $teraboxRclone -Algorithm SHA256).Hash.ToLowerInvariant()
if ($teraboxBinaryHash -ne "e2667b5040f9dbdbd0a457ecbdeeff96670e93e338ae7afa1743b88c73f6800f") {
    throw "TeraBox backend executable SHA-256 mismatch."
}
$teraboxVersionOutput = (& $teraboxRclone version 2>&1 | Out-String)
if ($LASTEXITCODE -ne 0 -or $teraboxVersionOutput -notmatch "(?m)^rclone v1\.75\.2-extra(?:\s|$)") {
    throw "TeraBox backend version check failed."
}
$teraboxBackendOutput = (& $teraboxRclone help backends 2>&1 | Out-String)
if ($LASTEXITCODE -ne 0 -or $teraboxBackendOutput -notmatch "(?im)^\s*terabox\s" -or $teraboxBackendOutput -notmatch "(?im)^\s*drive\s") {
    throw "TeraBox backend must advertise both TeraBox and Google Drive."
}

# 2. Tests (unit tests always; integration tests need WinFsp and take several minutes).
if (-not $SkipTests) {
    dotnet test (Join-Path $repo "tests\Aveniq.Tests") -c $Configuration --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "Unit tests failed" }
}

# 3. Publish (self-contained so users do not need to install .NET).
# The CLI is a separate program with the assembly name "aveniq". Publishing it into the root would overwrite the
# desktop "Aveniq.exe" on case-insensitive Windows file systems, so keep it in its own cli\ folder.
$common = @("-c", $Configuration, "-r", "win-x64", "--self-contained", "true", "-p:PublishReadyToRun=true", "-p:Version=$Version", "--nologo", "-v", "q")
dotnet publish (Join-Path $repo "src\Aveniq.Desktop\Aveniq.Desktop.csproj") @common -o $stage
if ($LASTEXITCODE -ne 0) { throw "Desktop publish failed" }
$cliStage = Join-Path $stage "cli"
dotnet publish (Join-Path $repo "src\Aveniq.Cli\Aveniq.Cli.csproj") @common -o $cliStage
if ($LASTEXITCODE -ne 0) { throw "CLI publish failed" }

# 4. rclone + notices.
$toolDir = Join-Path $stage "tools\rclone"
New-Item -ItemType Directory -Force $toolDir | Out-Null
Copy-Item $rclone $toolDir -Force
Copy-Item $sums.FullName $toolDir -Force
$hash = (Get-FileHash (Join-Path $toolDir "rclone.exe") -Algorithm SHA256).Hash.ToLower()
Set-Content (Join-Path $toolDir "README.txt") @"
rclone $rcloneVersion (https://rclone.org), MIT License, Copyright (C) 2012 by Nick Craig-Wood.
Downloaded from https://downloads.rclone.org/$rcloneVersion/ - the zip's SHA256 was verified against the official
SHA256SUMS file (included). SHA256 of rclone.exe: $hash
"@
$teraboxToolDir = Join-Path $stage "tools\rclone-terabox"
New-Item -ItemType Directory -Force $teraboxToolDir | Out-Null
Copy-Item $teraboxRclone $teraboxToolDir -Force
Copy-Item $teraboxReadme $teraboxToolDir -Force
Copy-Item (Join-Path $repo "THIRD_PARTY_NOTICES.md") $stage -Force

# Optional publisher Google OAuth client (Desktop app client JSON from Google Cloud, renamed). It is only useful to
# everyone once the Google project is "In production"; in Testing mode only listed test users can sign in with it.
$publisherKey = Join-Path $PSScriptRoot "google-client.json"
if (Test-Path $publisherKey) {
    Copy-Item $publisherKey (Join-Path $stage "google-client.json") -Force
    Write-Host "Bundled publisher Google key (installer\google-client.json)." -ForegroundColor Yellow
}

# 5. Docs and scripts.
foreach ($f in "README.md", "KNOWN_LIMITATIONS.md", "COMPATIBILITY.md", "BENCHMARKS.md", "TEST_REPORT.md", "BUILD_STATUS.md", "ARCHITECTURE.md") {
    if (Test-Path (Join-Path $repo $f)) { Copy-Item (Join-Path $repo $f) $stage -Force }
}
New-Item -ItemType Directory -Force (Join-Path $stage "docs") | Out-Null
Copy-Item (Join-Path $repo "docs\*") (Join-Path $stage "docs") -Recurse -Force
Copy-Item (Join-Path $PSScriptRoot "Install-Aveniq.ps1") $stage -Force
Copy-Item (Join-Path $PSScriptRoot "Uninstall-Aveniq.ps1") $stage -Force
Set-Content (Join-Path $stage "Install.cmd") "@echo off`r`npowershell -NoProfile -ExecutionPolicy Bypass -File `"%~dp0Install-Aveniq.ps1`" %*`r`npause"
Remove-Item (Join-Path $stage "*.pdb") -ErrorAction SilentlyContinue

# 6. Smoke test of the published binaries (no UI): both programs must be present, and the CLI must run.
if (-not (Test-Path (Join-Path $stage "Aveniq.exe"))) { throw "Desktop executable missing from the published folder" }
$cliExe = Join-Path $cliStage "aveniq.exe"
if (-not (Test-Path $cliExe)) { throw "CLI executable missing from the published folder" }
$smokeDataRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("Aveniq-release-smoke-" + [guid]::NewGuid())
$previousAveniqHome = $env:AVENIQ_HOME
try {
    $env:AVENIQ_HOME = $smokeDataRoot
    & $cliExe deps
    if ($LASTEXITCODE -ne 0) { throw "aveniq deps failed in the published folder" }
}
finally {
    if ($null -eq $previousAveniqHome) {
        Remove-Item Env:AVENIQ_HOME -ErrorAction SilentlyContinue
    } else {
        $env:AVENIQ_HOME = $previousAveniqHome
    }
    if (Test-Path $smokeDataRoot) { Remove-Item $smokeDataRoot -Recurse -Force }
}

# 7. Zip + checksum.
$zip = Join-Path $dist "$name.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zip -CompressionLevel Optimal
$zipHash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLower()
Set-Content "$zip.sha256" "$zipHash  $name.zip"
$size = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host "Release: $zip ($size MB)`nSHA256: $zipHash" -ForegroundColor Green

# 8. Single-file setup.exe (Inno Setup 6) with the official WinFsp installer inside.
$iscc = @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    Write-Host "Inno Setup not found: skipped setup.exe (install it with: winget install JRSoftware.InnoSetup)." -ForegroundColor Yellow
    return
}
$winfspDir = Join-Path $repo "tools\winfsp"
$winfspMsi = Join-Path $winfspDir "winfsp-$WinFspVersion.msi"
if (-not (Test-Path $winfspMsi)) {
    New-Item -ItemType Directory -Force $winfspDir | Out-Null
    $tag = "v" + ($WinFspVersion -replace '\.\d+$', '')
    Invoke-WebRequest "https://github.com/winfsp/winfsp/releases/download/$tag/winfsp-$WinFspVersion.msi" -OutFile $winfspMsi -UseBasicParsing
}
# Ship only the genuine installer: it must carry a valid Authenticode signature from WinFsp's publisher.
$sig = Get-AuthenticodeSignature $winfspMsi
if ($sig.Status -ne "Valid" -or $sig.SignerCertificate.Subject -notmatch "NAVIMATICS LLC") {
    Remove-Item $winfspMsi -Force
    throw "WinFsp installer signature check failed ($($sig.Status)); deleted it."
}
& $iscc /Q "/DAppVersion=$Version" "/DSourceDir=$stage" "/DWinFspMsi=$winfspMsi" "/O$dist" (Join-Path $PSScriptRoot "Aveniq.iss")
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed" }
$setup = Join-Path $dist "Aveniq-$Version-setup.exe"
$setupHash = (Get-FileHash $setup -Algorithm SHA256).Hash.ToLower()
Set-Content "$setup.sha256" "$setupHash  Aveniq-$Version-setup.exe"
Write-Host "Installer: $setup ($([math]::Round((Get-Item $setup).Length / 1MB, 1)) MB)`nSHA256: $setupHash" -ForegroundColor Green
