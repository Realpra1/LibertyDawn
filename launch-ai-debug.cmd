@echo off
setlocal

set "ai_map=%~1"
if defined ai_map goto launch

if exist "%~dp0mods\cnc\maps\Empire-Earth.oramap" set "ai_map=%~dp0mods\cnc\maps\Empire-Earth.oramap"
for /r "%AppData%\OpenRA\maps\cnc" %%f in (Empire-Earth.oramap) do if not defined ai_map if exist "%%f" set "ai_map=%%f"
for /r "%AppData%\OpenRA\maps\cnc" %%f in (EmpireEarth4.oramap) do if not defined ai_map if exist "%%f" set "ai_map=%%f"

if not defined ai_map (
	echo Could not find Empire-Earth.oramap or EmpireEarth4.oramap under %AppData%\OpenRA\maps\cnc.
	echo Pass the map path explicitly: launch-ai-debug.cmd "C:\path\to\Empire-Earth.oramap"
	exit /b 1
)

:launch
echo Launching autonomous AI test on "%ai_map%".
call "%~dp0launch-game.cmd" Game.Mod=cnc Launch.Map="%ai_map%" Launch.LobbyCommands="spectate;option gamespeed fastest;option startingcash 20000;slot_bot Multi0 0 skynet 1 1;slot_bot Multi1 0 skynet 1 2;slot_bot Multi2 0 brutalis 2 4;slot_bot Multi3 0 brutalis 2 5;slot_bot Multi4 0 brutalis 2 6;faction 1 nod;faction 2 gdi"

endlocal
