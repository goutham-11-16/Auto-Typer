# Auto-Typer and Xbox Game Bar All-In-One Release Build Script
# Usage: powershell -ExecutionPolicy Bypass -File .\Build-ReleaseInstaller.ps1

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ScriptDir

Write-Host "====================================================" -ForegroundColor Cyan
Write-Host "   Auto-Typer + Game Bar Widget Release Packager    " -ForegroundColor Cyan
Write-Host "====================================================" -ForegroundColor Cyan

# 1. Build and Publish Auto-Typer Desktop App
Write-Host "`n[1/3] Publishing Auto-Typer Desktop Application..." -ForegroundColor Yellow
$autoTyperProj = Join-Path $ScriptDir "AutoTyper\AutoTyper.csproj"
& dotnet publish $autoTyperProj -c Release -r win-x64 --self-contained true
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to publish AutoTyper."
    exit 1
}

# 2. Build Xbox Game Bar Widget
Write-Host "`n[2/3] Building Xbox Game Bar Widget (C++/WinRT)..." -ForegroundColor Yellow
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $ScriptDir "build.ps1") -Configuration Release
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to build Xbox Game Bar Widget."
    exit 1
}

# 3. Locate Inno Setup Compiler (ISCC.exe)
Write-Host "`n[3/3] Compiling Setup Installer..." -ForegroundColor Yellow
$isccCandidates = @(
    "C:\Users\esamb\AppData\Local\Programs\Inno Setup 7\ISCC.exe",
    "C:\Users\esamb\AppData\Local\Programs\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 7\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
)

$isccPath = $null
foreach ($path in $isccCandidates) {
    if (Test-Path $path) {
        $isccPath = $path
        break
    }
}

if (-not $isccPath) {
    $found = Get-Command "iscc.exe" -ErrorAction SilentlyContinue
    if ($found) { $isccPath = $found.Source }
}

if (-not $isccPath) {
    Write-Error "Inno Setup Compiler (ISCC.exe) not found. Please install Inno Setup."
    exit 1
}

$setupIss = Join-Path $ScriptDir "setup.iss"
& "$isccPath" "$setupIss"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Inno Setup compilation failed."
    exit 1
}

$distDir = Join-Path $ScriptDir "dist"
$installerExe = Join-Path $distDir "AutoTyper-byGo-Setup.exe"

if (Test-Path $installerExe) {
    $item = Get-Item $installerExe
    $sizeMb = [math]::Round($item.Length / 1MB, 2)
    Write-Host "`n====================================================" -ForegroundColor Green
    Write-Host "   BUILD AND PACKAGING COMPLETE!" -ForegroundColor Green
    Write-Host "   Installer: $installerExe ($sizeMb MB)" -ForegroundColor Green
    Write-Host "   Ready to upload to GitHub Releases!" -ForegroundColor Green
    Write-Host "====================================================" -ForegroundColor Cyan
}
