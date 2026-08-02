@echo off
setlocal

set "ai_map=%~1"
if defined ai_map goto launch

for /r "%AppData%\OpenRA\maps\cnc" %%f in (Empire-Earth.oramap) do if not defined ai_map if exist "%%f" set "ai_map=%%f"
for /r "%AppData%\OpenRA\maps\cnc" %%f in (EmpireEarth4.oramap) do if not defined ai_map if exist "%%f" set "ai_map=%%f"

if not defined ai_map (
	echo Could not find Empire-Earth.oramap or EmpireEarth4.oramap under %AppData%\OpenRA\maps\cnc.
	echo Pass the map path explicitly: launch-ai-debug.cmd "C:\path\to\Empire-Earth.oramap"
	exit /b 1
)

:launch
echo Launching autonomous AI test on "%ai_map%".
call "%~dp0launch-game.cmd" Game.Mod=cnc Launch.Map="%ai_map%" Launch.LobbyCommands="spectate;option gamespeed fastest;slot_bot Multi0 1 skynet 1 1;slot_bot Multi1 1 skynet 1 2;slot_bot Multi2 1 brutalis 2 4;slot_bot Multi3 1 brutalis 2 5;slot_bot Multi4 1 brutalis 2 6"

endlocal
