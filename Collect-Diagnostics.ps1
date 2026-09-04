# Auto-Typer Xbox Game Bar Widget Diagnostic Script
# Usage: powershell -ExecutionPolicy Bypass -File .\Collect-Diagnostics.ps1

Write-Host ======================================================= -ForegroundColor Cyan
Write-Host  Auto-Typer Xbox Game Bar Widget Diagnostic Report  -ForegroundColor Cyan
Write-Host ======================================================= -ForegroundColor Cyan

# 1. Package Status
Write-Host 
[1/4] Checking UWP Package Registration... -ForegroundColor Yellow
 = Get-AppxPackage AutoTyper.GameBarWidget -ErrorAction SilentlyContinue
if () {
    Write-Host Package Found: -ForegroundColor Green
    Write-Host  Name: 
    Write-Host  Version: 
    Write-Host  Status: 
    Write-Host  InstallLocation: 
} else {
    Write-Host [WARNING] AutoTyper.GameBarWidget is NOT registered on this machine! -ForegroundColor Red
    Write-Host Try running Register-Widget.bat in your installation folder as Administrator. -ForegroundColor Yellow
}

# 2. Developer Mode / Sideloading
Write-Host 
[2/4] Checking AppModelUnlock / Sideloading Registry... -ForegroundColor Yellow
try {
     = Get-ItemProperty -Path HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock -ErrorAction SilentlyContinue
     = Get-ItemProperty -Path HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock -ErrorAction SilentlyContinue
    Write-Host  HKLM AllowDevelopmentWithoutDevLicense: 
    Write-Host  HKCU AllowDevelopmentWithoutDevLicense: 
} catch {
    Write-Host  Could not read AppModelUnlock key. -ForegroundColor Gray
}

# 3. Windows Application Event Log Errors
Write-Host 
[3/4] Checking Windows Event Viewer (Recent Application Errors)... -ForegroundColor Yellow
 = Get-WinEvent -FilterHashtable @{LogName='Application'; Level=1,2,3} -MaxEvents 50 -ErrorAction SilentlyContinue | 
          Where-Object { .Message -like *AutoTyper* -or .Message -like *GameBar* } | 
          Select-Object -First 3

if () {
    foreach ( in ) {
        Write-Host ------------------------------------------------------- -ForegroundColor DarkGray
        Write-Host Time:  -ForegroundColor White
        Write-Host Source:  -ForegroundColor Magenta
        Write-Host Message:  -ForegroundColor White
    }
} else {
    Write-Host No AutoTyper errors found in Application Event Log. -ForegroundColor Green
}

# 4. Widget Internal Debug Log
Write-Host 
[4/4] Checking Internal Widget Debug Log... -ForegroundColor Yellow
 = Get-ChildItem -Path C:\Users\esamb\AppData\Local\Packages\AutoTyper.GameBarWidget*\LocalState\widget_debug.log -ErrorAction SilentlyContinue | 
           Select-Object -First 1 -ExpandProperty FullName

if ( -and (Test-Path )) {
    Write-Host Found log:  -ForegroundColor Green
    Write-Host --- Last 25 lines --- -ForegroundColor Cyan
    Get-Content  -Tail 25
} else {
    Write-Host No widget_debug.log found in LocalState folder. -ForegroundColor Yellow
    Write-Host This usually means the widget executable has not been started yet by Xbox Game Bar. -ForegroundColor Gray
}

Write-Host 
======================================================= -ForegroundColor Cyan
Write-Host Diagnostic check completed. -ForegroundColor Cyan
Write-Host ======================================================= -ForegroundColor Cyan
