@echo off
setlocal
echo ====================================================
echo    Auto-Typer ^& Xbox Game Bar Widget Build Script
echo ====================================================

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %*
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Build failed with exit code %ERRORLEVEL%
    exit /b %ERRORLEVEL%
)

echo [SUCCESS] Build completed successfully.
exit /b 0
