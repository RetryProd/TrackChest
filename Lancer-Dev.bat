@echo off
rem Compile la version en cours de developpement (code de src\) puis la lance avec ton historique.
setlocal
set "SDK=%LOCALAPPDATA%\TrackChest\sdk\dotnet.exe"
if not exist "%SDK%" set "SDK=dotnet"
set "OUT=%~dp0src\ChestCounter\bin\Dev"

rem Une seule instance par historique : ferme celle qui tourne (et libere l'exe).
taskkill /im TrackChest.exe /f >nul 2>&1

echo Compilation de TrackChest (dev)...
"%SDK%" build "%~dp0src\ChestCounter\TrackChest.csproj" -c Debug -o "%OUT%" -v q -nologo
if errorlevel 1 (
    echo.
    echo La compilation a echoue.
    pause
    exit /b 1
)

start "" "%OUT%\TrackChest.exe" %*
