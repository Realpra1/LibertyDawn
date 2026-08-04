@echo off
setlocal

set "ai_map=%~1"
set "ai_speed=%~2"
set "ai_benchmark=%~3"
set "ai_save_tick=%~4"
set "ai_save_name=%~5"
set "ai_save_args="
set "ai_seed=24680"
set "ai_headless_args="

if /I "%ai_map%"=="max" (
	set "ai_speed=max"
	set "ai_benchmark=%~2"
	set "ai_map="
)

if /I "%ai_map%"=="headless" (
	set "ai_headless_args=Launch.Headless=true"
	set "ai_speed=max"
	set "ai_benchmark=%~2"
	set "ai_save_tick=%~3"
	set "ai_save_name=%~4"
	set "ai_map="
)

if /I "%ai_speed%"=="headless" (
	set "ai_headless_args=Launch.Headless=true"
	set "ai_speed=max"
)

if defined ai_headless_args if not defined ai_benchmark set "ai_benchmark=headless-max"

if not defined ai_speed set "ai_speed=fastest"
if /I not "%ai_speed%"=="fastest" if /I not "%ai_speed%"=="max" (
	echo Invalid game speed "%ai_speed%". Use fastest or max.
	exit /b 1
)

if defined ai_save_tick (
	if not defined ai_save_name set "ai_save_name=automated-test.orasav"
	set "ai_save_args=Launch.SaveGameAtTick=%ai_save_tick% Launch.SaveGameName=%ai_save_name%"
)

if defined ai_map goto launch

if exist "%~dp0mods\cnc\maps\Empire-Earth.oramap" set "ai_map=%~dp0mods\cnc\maps\Empire-Earth.oramap"
for /r "%AppData%\OpenRA\maps\cnc" %%f in (Empire-Earth.oramap) do if not defined ai_map if exist "%%f" set "ai_map=%%f"
for /r "%AppData%\OpenRA\maps\cnc" %%f in (EmpireEarth4.oramap) do if not defined ai_map if exist "%%f" set "ai_map=%%f"

if not defined ai_map (
	echo Could not find Empire-Earth.oramap or EmpireEarth4.oramap under %AppData%\OpenRA\maps\cnc.
	echo Usage: launch-ai-debug.cmd ["C:\path\to\Empire-Earth.oramap"] [fastest^|max^|headless] [benchmark-prefix] [save-tick] [save-name]
	echo Or:    launch-ai-debug.cmd max [benchmark-prefix]
	echo Or:    launch-ai-debug.cmd headless [benchmark-prefix] [save-tick] [save-name]
	exit /b 1
)

:launch
echo Launching autonomous AI test on "%ai_map%" at %ai_speed% speed.
if defined ai_benchmark (
	call "%~dp0launch-game.cmd" Game.Mod=cnc Debug.BotDebug=true %ai_headless_args% Launch.RandomSeed=%ai_seed% Launch.Benchmark="%ai_benchmark%" %ai_save_args% Launch.Map="%ai_map%" Launch.LobbyCommands="spectate;option gamespeed %ai_speed%;option startingcash 20000;slot_bot Multi0 0 skynet 1 1;slot_bot Multi1 0 skynet 1 2;slot_bot Multi2 0 brutalis 2 4;slot_bot Multi3 0 brutalis 2 5;slot_bot Multi4 0 brutalis 2 6;faction 1 nod;faction 2 gdi"
) else (
	call "%~dp0launch-game.cmd" Game.Mod=cnc Debug.BotDebug=true %ai_headless_args% Launch.RandomSeed=%ai_seed% %ai_save_args% Launch.Map="%ai_map%" Launch.LobbyCommands="spectate;option gamespeed %ai_speed%;option startingcash 20000;slot_bot Multi0 0 skynet 1 1;slot_bot Multi1 0 skynet 1 2;slot_bot Multi2 0 brutalis 2 4;slot_bot Multi3 0 brutalis 2 5;slot_bot Multi4 0 brutalis 2 6;faction 1 nod;faction 2 gdi"
)

endlocal
