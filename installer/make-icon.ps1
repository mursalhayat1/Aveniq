# The approved Aveniq ribbon mark is already a multi-resolution ICO (16-256 px).
# Copy it to the filename used by the desktop project and Inno Setup instead of
# regenerating a different mark.
param(
    [string]$Source = "$PSScriptRoot\..\src\Aveniq.Desktop\Assets\aveniq-logo.ico",
    [string]$Out = "$PSScriptRoot\..\src\Aveniq.Desktop\Assets\aveniq.ico"
)
if (-not (Test-Path -LiteralPath $Source)) { throw "Icon source not found: $Source" }
Copy-Item -LiteralPath $Source -Destination $Out -Force
Write-Host "Copied $Source -> $Out"
