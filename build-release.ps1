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
$icon = Join-Path $root "src\ClaudeBuddy\appicon.ico"
& $vpk pack `
    --packId "ClaudeBuddy" `
    --packTitle "Claude Buddy" `
    --packAuthors "Claude Buddy" `
    --packVersion $Version `
    --packDir $publishDir `
    --mainExe "ClaudeBuddy.exe" `
    --icon $icon `
    --outputDir $releasesDir
if ($LASTEXITCODE -ne 0) { throw "vpk pack failed." }

# vpk always emits a portable .zip too; drop it so the release stays uncluttered. The
# user only ever needs ClaudeBuddy-win-Setup.exe — the .nupkg / .json files are the
# auto-updater's machinery (the installed app reads them via the GitHub Releases API).
Remove-Item (Join-Path $releasesDir "ClaudeBuddy-win-Portable.zip") -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "==> Done. Output in: $releasesDir" -ForegroundColor Green
Write-Host ""
Write-Host "Publish the update (one command):" -ForegroundColor Yellow
Write-Host "  gh release create v$Version (Get-ChildItem '$releasesDir' | % FullName) ``"
Write-Host "    --target main --title 'Claude Buddy $Version' ``"
Write-Host "    --notes '## Descarga`n\xF0\x9F\x91\x89 Bajate **ClaudeBuddy-win-Setup.exe** y ejecutalo. El resto de archivos los usa el auto-update.'"
Write-Host ""
Write-Host "Users install ClaudeBuddy-win-Setup.exe (direct link:" -ForegroundColor Gray
Write-Host "  https://github.com/LbillaSupport/Claude-pet/releases/latest/download/ClaudeBuddy-win-Setup.exe )" -ForegroundColor Gray
Write-Host "Existing installs auto-update on their next restart." -ForegroundColor Gray
