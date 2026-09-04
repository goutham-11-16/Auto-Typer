# Register Xbox Game Bar Widget in Windows
# Usage: powershell -ExecutionPolicy Bypass -File .\Register-Widget.ps1

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$manifestPath = Join-Path $ScriptDir "GameBarWidget\AppxManifest.xml"

if (-not (Test-Path $manifestPath)) {
    Write-Error "AppxManifest.xml not found at $manifestPath. Please run build.bat first."
}

Write-Host "Registering Auto-Typer Xbox Game Bar Widget with Windows..." -ForegroundColor Cyan

# Unregister any previous version first
$existing = Get-AppxPackage -Name "AutoTyper.GameBarWidget" -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Removing existing registration..." -ForegroundColor Yellow
    Remove-AppxPackage -Package $existing.PackageFullName
}

# Register the manifest as a loose AppX development package
Add-AppxPackage -Register $manifestPath

$pkg = Get-AppxPackage -Name "AutoTyper.GameBarWidget" -ErrorAction SilentlyContinue
if ($pkg -and $pkg.Status -eq "Ok") {
    Write-Host "`n[SUCCESS] Auto-Typer Xbox Game Bar Widget registered successfully!" -ForegroundColor Green
    Write-Host "Package Name: $($pkg.Name)" -ForegroundColor White
    Write-Host "Status:       $($pkg.Status)" -ForegroundColor White
    Write-Host "`nTo open the widget:" -ForegroundColor Cyan
    Write-Host "  1. Press Win + G on your keyboard to open Xbox Game Bar." -ForegroundColor White
    Write-Host "  2. In the Widget Menu (top bar), look for 'Auto-Typer' and click it." -ForegroundColor White
    Write-Host "  3. Click the pin icon to keep the widget visible over fullscreen games or browsers!" -ForegroundColor White
} else {
    Write-Error "Registration could not be confirmed. Check Windows Event Viewer."
}
