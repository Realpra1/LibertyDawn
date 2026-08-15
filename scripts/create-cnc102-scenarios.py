#!/usr/bin/env python3
"""Create ignored CNC-102 full-engine scenarios from Empire Earth."""

from __future__ import annotations

import argparse
import tempfile
import zipfile
from pathlib import Path


TREE_TYPES = {"split2", "split3", "splitblue", "splitred"}


def without_tree_actors(text: str) -> str:
	lines = text.splitlines(keepends=True)
	result: list[str] = []
	skip = False
	for line in lines:
		if line.startswith("\t") and not line.startswith("\t\t"):
			skip = line.rstrip().split(": ", 1)[-1].lower() in TREE_TYPES
		if not skip:
			result.append(line)
	return "".join(result)


def add_controlled_trees(text: str, title: str) -> str:
	start = text.index("Title:")
	end = text.index("\n", start)
	text = text[:start] + f"Title: {title}" + text[end:]
	actors = """\
\tCnc102TreeWest: splitblue
\t\tOwner: Neutral
\t\tLocation: 43,162
\tCnc102WestSiteGuard01: brik
\t\tOwner: Neutral
\t\tLocation: 42,159
\tCnc102WestSiteGuard02: brik
\t\tOwner: Neutral
\t\tLocation: 43,159
\tCnc102WestSiteGuard03: brik
\t\tOwner: Neutral
\t\tLocation: 44,159
\tCnc102WestSiteGuard04: brik
\t\tOwner: Neutral
\t\tLocation: 45,159
\tCnc102WestSiteGuard05: brik
\t\tOwner: Neutral
\t\tLocation: 42,160
\tCnc102WestSiteGuard06: brik
\t\tOwner: Neutral
\t\tLocation: 45,160
\tCnc102WestSiteGuard07: brik
\t\tOwner: Neutral
\t\tLocation: 42,161
\tCnc102WestSiteGuard08: brik
\t\tOwner: Neutral
\t\tLocation: 43,161
\tCnc102WestSiteGuard09: brik
\t\tOwner: Neutral
\t\tLocation: 44,161
\tCnc102WestSiteGuard10: brik
\t\tOwner: Neutral
\t\tLocation: 45,161
\tCnc102TreeEast: split3
\t\tOwner: Neutral
\t\tLocation: 44,162
\tCnc102WestRefinery: proc
\t\tOwner: Multi0
\t\tLocation: 35,162
\tCnc102WestRefineryB: proc
\t\tOwner: Multi0
\t\tLocation: 35,154
\tCnc102WestFactory: fact
\t\tOwner: Multi0
\t\tLocation: 35,158
\tCnc102WestFactoryB: fact
\t\tOwner: Multi0
\t\tLocation: 24,155
\tCnc102WestFactoryC: fact
\t\tOwner: Multi0
\t\tLocation: 24,160
\tCnc102WestFactoryD: fact
\t\tOwner: Multi0
\t\tLocation: 24,165
\tCnc102WestPowerA: nuk2
\t\tOwner: Multi0
\t\tLocation: 39,158
\tCnc102WestPowerB: nuk2
\t\tOwner: Multi0
\t\tLocation: 39,162
\tCnc102WestBarracks: pyle
\t\tOwner: Multi0
\t\tLocation: 31,158
\tCnc102WestFactoryVehicle: weap
\t\tOwner: Multi0
\t\tLocation: 30,166
\tCnc102WestFactoryAirA: afld
\t\tOwner: Multi0
\t\tLocation: 30,171
\tCnc102WestFactoryAirB: afld
\t\tOwner: Multi0
\t\tLocation: 30,175
\tCnc102WestRadar: hq
\t\tOwner: Multi0
\t\tLocation: 35,166
\tCnc102WestHelipad: hpad
\t\tOwner: Multi0
\t\tLocation: 35,170
\tCnc102WestSilo: silo
\t\tOwner: Multi0
\t\tLocation: 39,166
\tCnc102WestDefense: gtwr
\t\tOwner: Multi0
\t\tLocation: 39,168
\tCnc102EastRefinery: proc
\t\tOwner: Multi1
\t\tLocation: 49,162
\tCnc102EastRefineryB: proc
\t\tOwner: Multi1
\t\tLocation: 49,154
\tCnc102EastFactory: fact
\t\tOwner: Multi1
\t\tLocation: 49,158
\tCnc102EastFactoryB: fact
\t\tOwner: Multi1
\t\tLocation: 55,165
\tCnc102EastFactoryC: fact
\t\tOwner: Multi1
\t\tLocation: 75,160
\tCnc102EastPowerA: nuk2
\t\tOwner: Multi1
\t\tLocation: 46,158
\tCnc102EastPowerB: nuk2
\t\tOwner: Multi1
\t\tLocation: 46,162
\tCnc102EastPowerC: nuk2
\t\tOwner: Multi1
\t\tLocation: 67,167
\tCnc102EastBarracks: pyle
\t\tOwner: Multi1
\t\tLocation: 53,158
\tCnc102EastFactoryVehicle: weap
\t\tOwner: Multi1
\t\tLocation: 53,166
\tCnc102EastFactoryAirA: afld
\t\tOwner: Multi1
\t\tLocation: 53,171
\tCnc102EastFactoryAirB: afld
\t\tOwner: Multi1
\t\tLocation: 53,175
\tCnc102EastRadar: hq
\t\tOwner: Multi1
\t\tLocation: 49,166
\tCnc102EastHelipad: hpad
\t\tOwner: Multi1
\t\tLocation: 49,170
\tCnc102EastSilo: silo
\t\tOwner: Multi1
\t\tLocation: 46,166
\tCnc102EastDefense: gtwr
\t\tOwner: Multi1
\t\tLocation: 46,168
"""
	text = text.rstrip() + "\n" + actors
	if "\nRules:" not in text:
		text = text.rstrip() + "\n\nRules: rules.yaml\n"
	return text


def rules(block_west: str | None, block_east: str | None) -> str:
	blockers = []
	if block_west:
		blockers.append(f'\tActor.Create("brik", true, {{ Owner = Neutral, Location = CPos.New({block_west}) }})')
	if block_east:
		blockers.append(f'\tActor.Create("brik", true, {{ Owner = Neutral, Location = CPos.New({block_east}) }})')
	blocker_body = "\n".join(blockers) or "\t-- Discovery variant intentionally leaves both planned sites legal."
	return f"""Player:
\tBaseBuilderBotModule@brutalis:
\t\tTiberiumFieldDebugLogging: true
\t\tOpeningDebugLogging: true
\t\tTiberiumFieldTreeTypes: splitblue
\tBaseBuilderBotModule@ironreaper:
\t\tTiberiumFieldDebugLogging: true
\t\tOpeningDebugLogging: true
\t\tTiberiumFieldTreeTypes: split3

CNC102FACT:
\tInherits: FACT
\tRenderSprites:
\t\tImage: fact
\t-Transforms:
\t-TransformsIntoMobile:
\t-TransformsIntoPassenger:
\t-TransformsIntoRepairable:
\t-TransformsIntoTransforms:

World:
\tLuaScript:
\t\tScripts: cnc102.lua
\tStartingUnits@cnc102gdi:
\t\tClass: cnc102
\t\tClassName: CNC-102 ready economy base
\t\tFactions: gdi
\t\tSupportActors: harv, harv, harv, harv, harv, harv, harv, harv, harv, harv
\t\tInnerSupportRadius: 3
\t\tOuterSupportRadius: 8
\t\tUpgrades: upgrade.economy1, upgrade.economy2, upgrade.economy3
\tStartingUnits@cnc102nod:
\t\tClass: cnc102
\t\tClassName: CNC-102 distant control
\t\tFactions: nod
\t\tBaseActor: mcv
\t\tSupportActors: harv, harv
\t\tInnerSupportRadius: 3
\t\tOuterSupportRadius: 8
""", f"""WorldLoaded = function()
\tNeutral = Player.GetPlayer("Neutral")
\tPlayer.GetPlayer("Multi0").Cash = 5000
\tPlayer.GetPlayer("Multi1").Cash = 5000
\tfor i = 1, 6 do
\t\tTrigger.AfterDelay(DateTime.Seconds(i * 10), function()
\t\t\tPlayer.GetPlayer("Multi0").Cash = 5000
\t\t\tPlayer.GetPlayer("Multi1").Cash = 5000
\t\tend)
\tend
\tTrigger.AfterDelay(DateTime.Seconds(65), function()
\t\tPlayer.GetPlayer("Multi0").Cash = 100000
\t\tPlayer.GetPlayer("Multi1").Cash = 100000
\tend)
\tTrigger.AfterDelay(DateTime.Seconds(66), function()
\t\tfor i = 0, 7 do
\t\t\tActor.Create("cnc102fact", true, {{ Owner = Player.GetPlayer("Multi0"), Location = CPos.New(10 + i * 4, 185) }})
\t\t\tActor.Create("cnc102fact", true, {{ Owner = Player.GetPlayer("Multi1"), Location = CPos.New(60 + i * 4, 185) }})
\t\tend
\tend)
\tTrigger.AfterDelay(DateTime.Seconds(88), function()
{blocker_body}
\tend)
end
"""


def write_map(source: Path, output: Path, title: str,
	block_west: str | None, block_east: str | None) -> None:
	with tempfile.TemporaryDirectory() as temporary:
		root = Path(temporary)
		with zipfile.ZipFile(source) as archive:
			archive.extractall(root)
		map_yaml = root / "map.yaml"
		text = without_tree_actors(map_yaml.read_text(encoding="utf-8-sig"))
		map_yaml.write_text(add_controlled_trees(text, title), encoding="utf-8")
		rules_yaml, lua = rules(block_west, block_east)
		(root / "rules.yaml").write_text(rules_yaml, encoding="utf-8")
		(root / "cnc102.lua").write_text(lua, encoding="utf-8")
		output.parent.mkdir(parents=True, exist_ok=True)
		with zipfile.ZipFile(output, "w", zipfile.ZIP_DEFLATED) as archive:
			for path in sorted(root.iterdir()):
				archive.write(path, path.name)


def main() -> None:
	parser = argparse.ArgumentParser()
	parser.add_argument("--output", required=True, type=Path)
	parser.add_argument("--west-blocker", help="planned west cell as x,y")
	parser.add_argument("--east-blocker", help="planned east cell as x,y")
	args = parser.parse_args()
	root = Path(__file__).resolve().parent.parent
	source = root / "mods/cnc/maps/Empire-Earth.oramap"
	write_map(source, args.output / "cnc102-blocked-both.oramap",
		"CNC-102 Blocked Ready Resonators", args.west_blocker, args.east_blocker)
	write_map(source, args.output / "cnc102-fancy-save-control.oramap",
		"CNC-102 Fancy Save and Blocked Control", None, args.east_blocker)


if __name__ == "__main__":
	main()
