<#
.SYNOPSIS
    Builds Claude Buddy and packages it with Velopack into a professional Setup.exe plus
    update packages, ready to publish to GitHub Releases. Installed copies then update
    themselves automatically.

.DESCRIPTION
    Run this whenever you want to cut a new version. It:
      1. Publishes a self-contained win-x64 build (no .NET needed on the user's PC).
      2. Uses the Velopack CLI (vpk) to produce, in .\Releases\ :
           - ClaudeBuddy-win-Setup.exe   <- the friendly installer users double-click
           - a full + delta .nupkg        <- consumed by the auto-updater
           - RELEASES / releases.win.json <- the update manifest
    Then create a GitHub Release tagged "v<version>" and upload EVERYTHING in .\Releases\.
    Existing installs will silently update to it on their next restart.

    First run installs the vpk tool automatically if it's missing.

.EXAMPLE
    .\build-release.ps1 -Version 1.1.0
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Version
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$proj = Join-Path $root "src\ClaudeBuddy\ClaudeBuddy.csproj"
$publishDir = Join-Path $root "src\ClaudeBuddy\bin\Release\net9.0-windows\win-x64\publish"
$releasesDir = Join-Path $root "Releases"

# --- Locate dotnet (not on PATH on this machine) ---------------------------
$dotnet = "C:\Program Files\dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) { $dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source }
if (-not $dotnet) { throw "Could not find dotnet. Install the .NET 9 SDK." }

# --- Ensure the Velopack CLI (vpk) is available ----------------------------
$vpk = (Get-Command vpk -ErrorAction SilentlyContinue).Source
if (-not $vpk) {
    Write-Host "==> Installing Velopack CLI (vpk) as a global tool" -ForegroundColor Cyan
    & $dotnet tool install -g vpk
    $vpk = Join-Path $env:USERPROFILE ".dotnet\tools\vpk.exe"
}
if (-not (Test-Path $vpk)) { $vpk = "vpk" } # fall back to PATH

# --- 1. Publish ------------------------------------------------------------
Write-Host "==> Publishing self-contained build $Version (this can take a minute)" -ForegroundColor Cyan
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
& $dotnet publish $proj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    /p:Version=$Version `
    /p:DebugType=none
if ($LASTEXITCODE -ne 0) { throw "Publish failed." }

# --- 2. Package with Velopack ---------------------------------------------
Write-Host "==> Packaging Setup.exe + update packages with Velopack" -ForegroundColor Cyan
& $vpk pack `
    --packId "ClaudeBuddy" `
    --packTitle "Claude Buddy" `
    --packAuthors "Claude Buddy" `
    --packVersion $Version `
    --packDir $publishDir `
    --mainExe "ClaudeBuddy.exe" `
    --outputDir $releasesDir
if ($LASTEXITCODE -ne 0) { throw "vpk pack failed." }

Write-Host ""
Write-Host "==> Done. Output in: $releasesDir" -ForegroundColor Green
Write-Host ""
Write-Host "Publish the update:" -ForegroundColor Yellow
Write-Host "  1. GitHub -> Releases -> Draft a new release, tag it  v$Version."
Write-Host "  2. Upload ALL files from the Releases\ folder as assets."
Write-Host "  3. Publish. Users share the link to ClaudeBuddy-win-Setup.exe to install;"
Write-Host "     existing installs auto-update on their next restart."
