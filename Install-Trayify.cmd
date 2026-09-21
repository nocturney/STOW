@echo off
setlocal
set "DEST=%LOCALAPPDATA%\Trayify"
if not exist "%DEST%" mkdir "%DEST%"
copy /Y "%~dp0Trayify.exe" "%DEST%\Trayify.exe" >nul
if errorlevel 1 (
  echo Failed to install Trayify.
  pause
  exit /b 1
)
start "" "%DEST%\Trayify.exe"
echo Trayify installed to:
echo %DEST%\Trayify.exe
timeout /t 2 >nul
