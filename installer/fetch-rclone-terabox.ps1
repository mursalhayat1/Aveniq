<#
.SYNOPSIS
  Fetches the pinned community rclone build with the TeraBox backend into its own tools directory.
.DESCRIPTION
  Verifies the release tag, commit, and GitHub-published archive SHA-256. Never replaces the official rclone binary
  or an existing TeraBox executable with different contents.
#>
$ErrorActionPreference = "Stop"
$repo = Resolve-Path "$PSScriptRoot\.."
$tag = "v1.75.2-extra"
$commit = "56bf13885494819bc801302ddf881279bdac7262"
$assetName = "rclone-windows-amd64.zip"
$archiveSha256 = "d5a5d9383a607ccb1af9a871d4cb92906121f32cf111497adb94958b99315c69"
$binarySha256 = "e2667b5040f9dbdbd0a457ecbdeeff96670e93e338ae7afa1743b88c73f6800f"
$releaseUrl = "https://api.github.com/repos/gulp79/rclone-extra/releases/tags/$tag"
$commitUrl = "https://api.github.com/repos/gulp79/rclone-extra/commits/$tag"
$installDir = Join-Path $repo "tools\rclone-terabox"
$installExe = Join-Path $installDir "rclone.exe"

$work = Join-Path ([IO.Path]::GetTempPath()) ("Aveniq-rclone-terabox-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $work | Out-Null
try {
    $headers = @{ "User-Agent" = "Aveniq-build" }
    $release = Invoke-RestMethod -Uri $releaseUrl -Headers $headers
    if ($release.tag_name -ne $tag) { throw "Unexpected TeraBox rclone release tag." }
    $resolvedCommit = Invoke-RestMethod -Uri $commitUrl -Headers $headers
    if ($resolvedCommit.sha -ne $commit) { throw "The pinned TeraBox rclone tag no longer resolves to the verified commit." }

    $asset = $release.assets | Where-Object { $_.name -eq $assetName } | Select-Object -First 1
    if (-not $asset -or $asset.digest -ne "sha256:$archiveSha256") {
        throw "The TeraBox release asset does not match the pinned GitHub SHA-256."
    }

    $archive = Join-Path $work $assetName
    $extractDir = Join-Path $work "extracted"
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $archive -UseBasicParsing
    $downloadHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($downloadHash -ne $archiveSha256) { throw "Downloaded TeraBox archive SHA-256 mismatch." }

    Expand-Archive -LiteralPath $archive -DestinationPath $extractDir
    $binaries = @(Get-ChildItem -LiteralPath $extractDir -Filter "rclone.exe" -File -Recurse)
    if ($binaries.Count -ne 1) { throw "Expected exactly one rclone.exe in the pinned release archive." }
    $sourceExe = $binaries[0].FullName
    $sourceHash = (Get-FileHash -LiteralPath $sourceExe -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($sourceHash -ne $binarySha256) { throw "Extracted TeraBox executable SHA-256 mismatch." }

    $versionOutput = (& $sourceExe version 2>&1 | Out-String)
    if ($LASTEXITCODE -ne 0 -or $versionOutput -notmatch "(?m)^rclone v1\.75\.2-extra(?:\s|$)") {
        throw "The downloaded binary does not identify as rclone v1.75.2-extra."
    }
    $backendOutput = (& $sourceExe help backends 2>&1 | Out-String)
    if ($LASTEXITCODE -ne 0 -or $backendOutput -notmatch "(?im)^\s*terabox\s" -or $backendOutput -notmatch "(?im)^\s*drive\s") {
        throw "The downloaded binary does not advertise both TeraBox and Google Drive backends."
    }

    if (Test-Path -LiteralPath $installExe) {
        $installedHash = (Get-FileHash -LiteralPath $installExe -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($installedHash -ne $sourceHash) {
            throw "Refusing to overwrite an existing tools\rclone-terabox\rclone.exe with different contents."
        }
        Write-Host "The existing TeraBox executable already matches the verified release; leaving it unchanged."
    } else {
        New-Item -ItemType Directory -Force -Path $installDir | Out-Null
        Copy-Item -LiteralPath $sourceExe -Destination $installExe
    }

    $readme = @"
Community rclone build with TeraBox backend: rclone v1.75.2-extra
Source: https://github.com/gulp79/rclone-extra
Release: $tag
Verified commit: $commit
Asset: $assetName
Release archive SHA-256 (pinned and checked against GitHub API): $archiveSha256
Executable SHA-256: $sourceHash

This is a community fork, not an official rclone release. Its TeraBox backend uses browser session cookies and
undocumented TeraBox web endpoints. Use only the separate TeraBox profile in Aveniq; compatibility is untested.
"@
    Set-Content -LiteralPath (Join-Path $installDir "README.txt") -Value $readme -Encoding UTF8
    Write-Host "Verified TeraBox backend installed separately at $installExe" -ForegroundColor Green
    Write-Host "Executable SHA-256: $sourceHash"
}
finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}
