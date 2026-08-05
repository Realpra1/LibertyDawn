@echo off
title OpenRA.Utility.exe
set ENGINE_DIR=..

rem Forward scripted invocations directly to the utility.  The interactive
rem prompt below is retained when no command (or only a mod) is supplied.
if /I "%~1"=="ra" goto unsupportedmod
if /I "%~1"=="d2k" goto unsupportedmod
if /I "%~1"=="ts" goto unsupportedmod
if not "%~2"=="" goto runarguments
if not "%~1"=="" (
	set "mod=%~1"
	goto help
)

:choosemod
echo ----------------------------------------
echo.
call bin\OpenRA.Utility.exe
echo Enter --exit to exit
set /P mod="Please enter cnc: OpenRA.Utility.exe "
if /I "%mod%" EQU "--exit" (exit /b)
if /I "%mod%" EQU "cnc" (goto help)
echo.
echo Unknown mod: %mod%
echo.
goto choosemod
:help
echo.
echo ----------------------------------------
echo.
echo OpenRA.Utility.exe %mod%
call bin\OpenRA.Utility.exe %mod%
:start
echo.
echo ----------------------------------------
echo.
echo Script options:
echo   --exit to exit
echo   --help to view the help
echo   --mod to choose a new mod
echo.
set /P command="Please enter a command: OpenRA.Utility.exe %mod% "
if /I "%command%" EQU "--exit" (exit /b)
if /I "%command%" EQU "--help" (goto help)
if /I "%command%" EQU "--mod" (goto choosemod)
echo.
echo ----------------------------------------
echo.
echo OpenRA.Utility.exe %mod% %command%
call bin\OpenRA.Utility.exe %mod% %command%
goto start

:runarguments
call bin\OpenRA.Utility.exe %*
exit /b %errorlevel%

:unsupportedmod
echo LibertyDawn utilities support only the cnc mod.
exit /b 2
