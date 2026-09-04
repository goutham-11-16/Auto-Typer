@echo off
echo Registering Auto-Typer Xbox Game Bar Widget...
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Get-AppxPackage AutoTyper.GameBarWidget -ErrorAction SilentlyContinue | Remove-AppxPackage -ErrorAction SilentlyContinue; Add-AppxPackage -Register '%~dp0GameBarWidget\AppxManifest.xml' -ForceApplicationShutdown; Get-Process GameBar, GameBarFTServer, XboxGameBarWidgets -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue"
echo.
echo =======================================================
echo Auto-Typer Widget Registered Successfully!
echo Press Win + G to open Xbox Game Bar and view widget.
echo =======================================================
pause
