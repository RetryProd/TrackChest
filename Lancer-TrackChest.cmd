@echo off
if not exist "%~dp0dist\TrackChest.exe" (
    echo TrackChest.exe est absent. Lance Construire.ps1 depuis PowerShell.
    pause
    exit /b 1
)
start "" "%~dp0dist\TrackChest.exe"
