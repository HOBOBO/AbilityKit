@echo off
setlocal

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0clone_unity_project_for_multiplayer.ps1" %*
if errorlevel 1 (
    echo.
    pause
)

endlocal
