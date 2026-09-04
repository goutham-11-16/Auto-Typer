@echo off
echo Registering Auto-Typer Xbox Game Bar Widget...
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Add-AppxPackage -Register '%~dp0GameBarWidget\AppxManifest.xml'; Get-Process GameBar, GameBarFTServer, XboxGameBarWidgets -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue"
echo.
echo =======================================================
echo Auto-Typer Widget Registered Successfully!
echo Press Win + G to open Xbox Game Bar and view widget.
echo =======================================================
pause
