@echo off
title OpenRA
echo %* | %SystemRoot%\System32\findstr.exe /I /C:"Game.Mod=ra" /C:"Game.Mod=d2k" /C:"Game.Mod=ts" >nul
if not errorlevel 1 goto unsupportedmod
cd %~dp0%
bin\OpenRA.exe Engine.EngineDir=".." Engine.LaunchPath="%~dpf0" Game.Mod=cnc %*

:end
if %errorlevel% neq 0 goto crashdialog
exit /b

:unsupportedmod
echo LibertyDawn supports only Game.Mod=cnc.
exit /b 2

:crashdialog
set logs=%AppData%\OpenRA\Logs
if exist %USERPROFILE%\Documents\OpenRA\Logs (set logs=%USERPROFILE%\Documents\OpenRA\Logs)
if exist Support\Logs (set logs=%cd%\Support\Logs)

echo ----------------------------------------
echo OpenRA has encountered a fatal error.
echo   * Log Files are available in %logs%
echo   * FAQ is available at https://github.com/OpenRA/OpenRA/wiki/FAQ
echo ----------------------------------------
pause
