# Auto-Typer and Xbox Game Bar Widget Build Script
# Usage: powershell -ExecutionPolicy Bypass -File .\build.ps1 [-Configuration Release|Debug]

param (
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ScriptDir

Write-Host "====================================================" -ForegroundColor Cyan
Write-Host "   Auto-Typer & Xbox Game Bar Widget Build Script   " -ForegroundColor Cyan
Write-Host "   Configuration: $Configuration                    " -ForegroundColor Cyan
Write-Host "====================================================" -ForegroundColor Cyan

# 1. Build Auto-Typer Desktop Application
Write-Host "`n[1/3] Building Auto-Typer WPF Application..." -ForegroundColor Yellow
$autoTyperProj = Join-Path $ScriptDir "AutoTyper\AutoTyper.csproj"
& dotnet build $autoTyperProj -c $Configuration
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to build AutoTyper desktop application."
}
Write-Host "Auto-Typer desktop application build succeeded." -ForegroundColor Green

# 2. Locate Visual Studio C++ Compiler Tools
Write-Host "`n[2/3] Building Xbox Game Bar Widget (C++/WinRT)..." -ForegroundColor Yellow

$vswhere = "C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vswhere)) {
    Write-Error "vswhere.exe not found at $vswhere."
}

$vsPath = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $vsPath) {
    $vsPath = & $vswhere -latest -products * -property installationPath
}

$vcvars = Join-Path $vsPath "VC\Auxiliary\Build\vcvars64.bat"
if (-not (Test-Path $vcvars)) {
    Write-Error "vcvars64.bat not found at $vcvars."
}

$widgetDir = Join-Path $ScriptDir "GameBarWidget"
$mainCpp = Join-Path $widgetDir "main.cpp"
$viewCpp = Join-Path $widgetDir "WidgetView.cpp"
$ipcCpp = Join-Path $widgetDir "IpcClient.cpp"
# Ensure any running widget process is stopped before linking
Get-Process -Name "AutoTyperWidget" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

$compileCmd = "call `"$vcvars`" && cd /d `"$widgetDir`" && cl.exe /EHsc /std:c++17 /O2 /Fo:.\\ /I.\\generated main.cpp WidgetView.cpp IpcClient.cpp /Fe:AutoTyperWidget.exe WindowsApp.lib Shell32.lib User32.lib Advapi32.lib"
cmd.exe /c $compileCmd
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to compile Xbox Game Bar Widget."
    exit 1
}

# Ensure runtime DLL and Manifest are in place
Copy-Item (Join-Path $widgetDir "lib\Microsoft.Gaming.XboxGameBar.dll") $widgetDir -Force
Copy-Item (Join-Path $widgetDir "lib\Microsoft.Gaming.XboxGameBar.winmd") $widgetDir -Force
if (-not (Test-Path (Join-Path $widgetDir "AppxManifest.xml"))) {
    Copy-Item (Join-Path $widgetDir "Package.appxmanifest") (Join-Path $widgetDir "AppxManifest.xml") -Force
}

Write-Host "Xbox Game Bar Widget build succeeded." -ForegroundColor Green

# 3. Summary
Write-Host "`n[3/3] Build Summary" -ForegroundColor Cyan
Write-Host "  Auto-Typer Desktop App: Built" -ForegroundColor Green
Write-Host "  Game Bar Widget Binary: $outputExe" -ForegroundColor Green
Write-Host "  AppX Manifest: $(Join-Path $widgetDir 'AppxManifest.xml')" -ForegroundColor Green
Write-Host "`nTo register the widget with Xbox Game Bar, run:" -ForegroundColor Yellow
Write-Host "  .\Register-Widget.ps1" -ForegroundColor White
Write-Host "====================================================" -ForegroundColor Cyan
