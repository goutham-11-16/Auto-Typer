# Unregister Xbox Game Bar Widget from Windows
# Usage: powershell -ExecutionPolicy Bypass -File .\Unregister-Widget.ps1

Write-Host "Searching for installed Auto-Typer Xbox Game Bar Widget..." -ForegroundColor Cyan

$existing = Get-AppxPackage -Name "AutoTyper.GameBarWidget" -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Removing package: $($existing.PackageFullName)..." -ForegroundColor Yellow
    Remove-AppxPackage -Package $existing.PackageFullName
    Write-Host "[SUCCESS] Auto-Typer Widget unregistered successfully." -ForegroundColor Green
} else {
    Write-Host "Auto-Typer Widget is not currently registered." -ForegroundColor White
}
