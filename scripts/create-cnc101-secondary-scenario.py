#!/usr/bin/env python3
"""Create an ignored CNC-101 secondary-queue observation scenario."""

from __future__ import annotations

import argparse
import tempfile
import zipfile
from pathlib import Path


RULES = """Player:
\tBaseBuilderBotModule@brutalis:
\t\tFirstTowerDebugLogging: true
\t\tSmartEconomyDebugLogging: true

World:
\tLuaScript:
\t\tScripts: utils.lua, cnc101-secondary.lua
"""


SILO_QUEUE_RULES = """
SILO:
	Buildable:
		Queue: {queue}
"""


SCRIPT = """Target = nil
LastWalls = 0
LastSilos = 0
LastDefenses = 0
FirstWallTick = nil
FourthWallTick = nil
FirstSiloTick = nil
FirstDefenseTick = nil
SequencePassed = false
ContinuationPassed = false
SequenceWalls = 0
SequenceSilos = 0
SequenceDefenses = 0
SiloOrderFailed = false
DefenseOrderFailed = false
SequenceOrderFailed = false

Count = function(types)
\tlocal total = 0
\tfor _, actorType in ipairs(types) do
\t\ttotal = total + #Target.GetActorsByType(actorType)
\tend
\treturn total
end

Observe = function()
\tlocal tick = DateTime.GameTime
\tlocal walls = Count({ "brik", "sbag", "cycl" })
\tlocal silos = Count({ "silo" })
\tlocal defenses = Count({ "obli", "gtwr", "gun", "atwr" })

\tif walls > LastWalls then
\t\tif FirstWallTick == nil then FirstWallTick = tick end
\t\tif walls >= 4 and FourthWallTick == nil then FourthWallTick = tick end
\t\tMedia.Debug("CNC101 secondary observation tick=" .. tick .. " event=wall walls=" .. walls ..
\t\t\t" silos=" .. silos .. " configured-defenses=" .. defenses)
\tend

\tif silos > LastSilos then
\t\tif FirstSiloTick == nil then FirstSiloTick = tick end
\t\tMedia.Debug("CNC101 secondary observation tick=" .. tick .. " event=silo walls=" .. walls ..
\t\t\t" silos=" .. silos .. " configured-defenses=" .. defenses)
\tend

\tif defenses > LastDefenses then
\t\tif FirstDefenseTick == nil then FirstDefenseTick = tick end
\t\tMedia.Debug("CNC101 secondary observation tick=" .. tick .. " event=configured-defense walls=" .. walls ..
\t\t\t" silos=" .. silos .. " configured-defenses=" .. defenses)
\tend

\tif silos > 0 and walls < 4 and not SiloOrderFailed then
\t\tSiloOrderFailed = true
\t\tMedia.Debug("CNC101 secondary FAIL silo-before-four-walls tick=" .. tick)
\tend
\tif defenses > 0 and silos < 1 and not DefenseOrderFailed then
\t\tDefenseOrderFailed = true
\t\tMedia.Debug("CNC101 secondary FAIL defense-before-silo tick=" .. tick)
\tend

\tif not SequencePassed and FourthWallTick ~= nil and FirstSiloTick ~= nil and FirstDefenseTick ~= nil then
\t\tif FourthWallTick < FirstSiloTick and FirstSiloTick < FirstDefenseTick then
\t\t\tSequencePassed = true
\t\t\tSequenceWalls = walls
\t\t\tSequenceSilos = silos
\t\t\tSequenceDefenses = defenses
\t\t\tMedia.Debug("CNC101 secondary PASS exact-order fourth-wall=" .. FourthWallTick ..
\t\t\t\t" silo=" .. FirstSiloTick .. " configured-defense=" .. FirstDefenseTick)
\t\telse
\t\t\tif not SequenceOrderFailed then
\t\t\t\tSequenceOrderFailed = true
\t\t\t\tMedia.Debug("CNC101 secondary FAIL exact-order fourth-wall=" .. FourthWallTick ..
\t\t\t\t\t" silo=" .. FirstSiloTick .. " configured-defense=" .. FirstDefenseTick)
\t\t\tend
\t\tend
\tend

\tif SequencePassed and not ContinuationPassed and
\t\t(walls > SequenceWalls or silos > SequenceSilos or defenses > SequenceDefenses) then
\t\tContinuationPassed = true
\t\tMedia.Debug("CNC101 secondary PASS post-sequence-construction tick=" .. tick ..
\t\t\t" walls=" .. walls .. " silos=" .. silos .. " configured-defenses=" .. defenses)
\tend

\tLastWalls = walls
\tLastSilos = silos
\tLastDefenses = defenses
end

WorldLoaded = function()
\tTarget = Player.GetPlayer("Multi0")
\tTarget.Cash = 100000
\tActor.Create("upgrade.recon1", true, { Owner = Target })
\tMedia.Debug("CNC101 secondary observer loaded tick=" .. DateTime.GameTime ..
\t\t" player=" .. Target.Name .. " faction=" .. Target.Faction ..
\t\t" cash=" .. Target.Cash .. " staged-upgrade=upgrade.recon1")
end

Tick = function()
\tif DateTime.GameTime % 5 == 0 then Observe() end
end
"""


def main() -> None:
	parser = argparse.ArgumentParser()
	parser.add_argument("--output", required=True, type=Path)
	parser.add_argument("--silo-queue", choices=("Building.GDI", "Building.Nod"))
	args = parser.parse_args()
	root = Path(__file__).resolve().parent.parent
	source = root / "mods/cnc/maps/Empire-Earth.oramap"

	with tempfile.TemporaryDirectory() as temporary:
		temporary_root = Path(temporary)
		with zipfile.ZipFile(source) as archive:
			archive.extractall(temporary_root)

		map_yaml = temporary_root / "map.yaml"
		text = map_yaml.read_text(encoding="utf-8-sig")
		start = text.index("Title:")
		end = text.index("\n", start)
		text = text[:start] + "Title: Empire Earth4 CNC-101 Secondary Queue" + text[end:]
		text = text.rstrip() + "\n\nRules: rules.yaml\nScript: cnc101-secondary.lua\n"
		map_yaml.write_text(text, encoding="utf-8")
		rules = RULES
		if args.silo_queue:
			rules += SILO_QUEUE_RULES.format(queue=args.silo_queue)
		(temporary_root / "rules.yaml").write_text(rules, encoding="utf-8")
		(temporary_root / "cnc101-secondary.lua").write_text(SCRIPT, encoding="utf-8")

		args.output.parent.mkdir(parents=True, exist_ok=True)
		with zipfile.ZipFile(args.output, "w", zipfile.ZIP_DEFLATED) as archive:
			for path in sorted(temporary_root.iterdir()):
				archive.write(path, path.name)


if __name__ == "__main__":
	main()
