@echo off
setlocal
echo Uninstalling Trayify...
taskkill /IM Trayify.exe /F >nul 2>nul
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v Trayify /f >nul 2>nul
set "DEST=%LOCALAPPDATA%\Trayify"
if exist "%DEST%\Trayify.exe" del /F /Q "%DEST%\Trayify.exe"
if exist "%DEST%\Trayify.exe.bak" del /F /Q "%DEST%\Trayify.exe.bak"
if exist "%DEST%" rd "%DEST%" 2>nul
echo.
echo Trayify has been removed.
echo Your settings were kept at:
echo %APPDATA%\Trayify
echo.
pause
