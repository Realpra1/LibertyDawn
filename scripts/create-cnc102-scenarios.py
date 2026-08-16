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


def add_controlled_trees(text: str, title: str, include_east_tree: bool,
	include_west_tree: bool = True, include_iron_followup: bool = False,
	include_fallback_resonator: bool = False) -> str:
	start = text.index("Title:")
	end = text.index("\n", start)
	text = text[:start] + f"Title: {title}" + text[end:]
	actors = """\
\tCnc102TreeWest: splitblue
\t\tOwner: Neutral
\t\tLocation: 43,162
\tCnc102TreeEast: split3
\t\tOwner: Neutral
\t\tLocation: 60,179
\tCnc102WestRefinery: proc
\t\tOwner: Multi0
\t\tLocation: 35,162
\tCnc102WestRefineryB: proc
\t\tOwner: Multi0
\t\tLocation: 35,154
\tCnc102WestFactory: fact
\t\tOwner: Multi0
\t\tLocation: 35,158
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
\tCnc102WestMcv: mcv
\t\tOwner: Multi0
\t\tLocation: 24,180
\tCnc102WestHarvester1: harv
\t\tOwner: Multi0
\t\tLocation: 28,180
\tCnc102WestHarvester2: harv
\t\tOwner: Multi0
\t\tLocation: 30,180
\tCnc102WestHarvester3: harv
\t\tOwner: Multi0
\t\tLocation: 32,180
\tCnc102WestHarvester4: harv
\t\tOwner: Multi0
\t\tLocation: 34,180
\tCnc102WestHarvester5: harv
\t\tOwner: Multi0
\t\tLocation: 36,180
\tCnc102EastRefinery: proc
\t\tOwner: Multi1
\t\tLocation: 49,162
\tCnc102EastRefineryB: proc
\t\tOwner: Multi1
\t\tLocation: 49,154
\tCnc102EastFactory: fact
\t\tOwner: Multi1
\t\tLocation: 49,158
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
\tCnc102EastMcv: mcv
\t\tOwner: Multi1
\t\tLocation: 84,180
\tCnc102EastHarvester1: harv
\t\tOwner: Multi1
\t\tLocation: 72,180
\tCnc102EastHarvester2: harv
\t\tOwner: Multi1
\t\tLocation: 74,180
\tCnc102EastHarvester3: harv
\t\tOwner: Multi1
\t\tLocation: 76,180
\tCnc102EastHarvester4: harv
\t\tOwner: Multi1
\t\tLocation: 78,180
\tCnc102EastHarvester5: harv
\t\tOwner: Multi1
\t\tLocation: 80,180
"""
	if include_fallback_resonator:
		actors += """\
\tCnc102FallbackSpacingResonatorWest: resonator
\t\tOwner: Multi0
\t\tLocation: 20,150
\tCnc102FallbackSpacingResonator: resonator
\t\tOwner: Multi1
\t\tLocation: 84,150
"""
	if not include_east_tree:
		actors = actors.replace("\tCnc102TreeEast: split3\n\t\tOwner: Neutral\n\t\tLocation: 60,179\n", "")
	if not include_west_tree:
		actors = actors.replace("\tCnc102TreeWest: splitblue\n\t\tOwner: Neutral\n\t\tLocation: 43,162\n", "")
	if include_iron_followup:
		actors += """\
\tCnc102TreeEastFollowup: split3
\t\tOwner: Neutral
\t\tLocation: 60,179
\tCnc102EastContinuationFactory: fact
\t\tOwner: Multi1
\t\tLocation: 60,172
"""
	text = text.rstrip() + "\n" + actors
	if "\nRules:" not in text:
		text = text.rstrip() + "\n\nRules: rules.yaml\n"
	return text


def rules(block_west: str | None, block_east: str | None, renew_trees: bool,
	blocker_owner: str = "Neutral", unit_blockers: bool = False) -> str:
	blockers = []
	if block_west:
		owner = "Multi0" if blocker_owner == "Owned" else blocker_owner
		blockers.append(f'\tActor.Create("brik", true, {{ Owner = Player.GetPlayer("{owner}"), Location = CPos.New({block_west}) }})')
	if block_east:
		owner = "Multi1" if blocker_owner == "Owned" else blocker_owner
		blockers.append(f'\tActor.Create("brik", true, {{ Owner = Player.GetPlayer("{owner}"), Location = CPos.New({block_east}) }})')
	if unit_blockers:
		blockers.extend([
			'\tActor.Create("e1", true, { Owner = Player.GetPlayer("Multi0"), Location = CPos.New(43,161) })',
			'\tActor.Create("e1", true, { Owner = Player.GetPlayer("Multi1"), Location = CPos.New(60,178) })',
		])
	blocker_body = "\n".join(blockers) or "\t-- Discovery variant intentionally leaves both planned sites legal."
	renewal_body = """\
\t-- Make follow-up fields available before the first project finishes. The
\t-- manager keeps its normal single project, but cannot lose continuation to a
\t-- gap in target availability after releasing it.
\tTrigger.AfterDelay(DateTime.Seconds(FOLLOWUP_SECONDS), function()
\t\tActor.Create("splitblue", true, { Owner = Neutral, Location = CPos.New(31, 178) })
\t\tActor.Create("split3", true, { Owner = Neutral, Location = CPos.New(50, 176) })
\t\tActor.Create("cnc102fact", true, { Owner = Player.GetPlayer("Multi0"), Location = CPos.New(24, 180) })
\t\tActor.Create("cnc102fact", true, { Owner = Player.GetPlayer("Multi1"), Location = CPos.New(74, 180) })
\tend)
\t-- Retire only the original targets after both first-project placement windows.
\t-- This prevents a completed fallback from being selected again while leaving
\t-- ordinary manager discovery and production untouched.
\tTrigger.AfterDelay(DateTime.Seconds(RETIRE_SECONDS), function()
\t\tCnc102TreeWest.Destroy()
\t\tif Cnc102TreeEast ~= nil then
\t\t\tCnc102TreeEast.Destroy()
\t\tend
\tend)
\tTrigger.AfterDelay(DateTime.Seconds(180), function()
\t\tMedia.Debug("CNC102 post-fallback-count Brutalis=" .. #Player.GetPlayer("Multi0").GetActorsByType("resonator") ..
\t\t\t" IronReaper=" .. #Player.GetPlayer("Multi1").GetActorsByType("resonator"))
\tend)
\tTrigger.AfterDelay(DateTime.Seconds(COUNT_SECONDS), function()
\t\tMedia.Debug("CNC102 resonator-count Brutalis=" .. #Player.GetPlayer("Multi0").GetActorsByType("resonator") ..
\t\t\t" IronReaper=" .. #Player.GetPlayer("Multi1").GetActorsByType("resonator"))
\tend)
""" if renew_trees else ""
	if renew_trees:
		blocked = bool(block_west or block_east)
		renewal_body = renewal_body.replace("FOLLOWUP_SECONDS", "178" if blocked else "70")
		renewal_body = renewal_body.replace("RETIRE_SECONDS", "178" if blocked else "190")
		renewal_body = renewal_body.replace("COUNT_SECONDS", "235" if blocked else "260")
	return f"""Player:
\tBaseBuilderBotModule@brutalis:
\t\tTiberiumFieldDebugLogging: true
\t\tOpeningDebugLogging: true
\t\tEconomyDefenseSamDebugLogging: true
\t\tTiberiumFieldTreeTypes: splitblue
\t\tOpeningMcvCount: 0
\t\tSmartEconomyHarvestersPerRefinery: 10
\t\tSmartEconomyFreeHarvestersPerRefinery: 10
\t\tSmartEconomyWaitingHarvesterThreshold: 99
\tBaseBuilderBotModule@ironreaper:
\t\tTiberiumFieldDebugLogging: true
\t\tOpeningDebugLogging: true
\t\tEconomyDefenseSamDebugLogging: true
\t\tTiberiumFieldTreeTypes: split3
\t\tOpeningMcvCount: 0
\t\tSmartEconomyHarvestersPerRefinery: 10
\t\tSmartEconomyFreeHarvestersPerRefinery: 10
\t\tSmartEconomyWaitingHarvesterThreshold: 99

FACT:
\tPower:
\t\tAmount: 2000
\tStoresResources:
\t\tCapacity: 100000

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
\tPlayer.GetPlayer("Multi0").Cash = 100000
\tPlayer.GetPlayer("Multi1").Cash = 100000
\tfor i = 1, 6 do
\t\tTrigger.AfterDelay(DateTime.Seconds(i * 10), function()
\t\t\tPlayer.GetPlayer("Multi0").Cash = 100000
\t\t\tPlayer.GetPlayer("Multi1").Cash = 100000
\t\tend)
\tend
\tfor i = 0, 28 do
\t\tTrigger.AfterDelay(DateTime.Seconds(65 + i * 10), function()
\t\t\tPlayer.GetPlayer("Multi0").Cash = 100000
\t\t\tPlayer.GetPlayer("Multi1").Cash = 100000
\t\tend)
\tend
\tTrigger.AfterDelay(DateTime.Seconds(5), function()
{blocker_body}
\tend)
\tTrigger.AfterDelay(DateTime.Seconds(110), function()
\t\tMedia.Debug("CNC102 planner-count Brutalis=" .. #Player.GetPlayer("Multi0").GetActorsByType("resonator") ..
\t\t\t" IronReaper=" .. #Player.GetPlayer("Multi1").GetActorsByType("resonator"))
\tend)
{renewal_body}\tTrigger.AfterDelay(DateTime.Seconds(220), function()
\t\tMedia.Debug("CNC102 live-resonators Brutalis=" .. #Player.GetPlayer("Multi0").GetActorsByType("resonator") ..
\t\t\t" IronReaper=" .. #Player.GetPlayer("Multi1").GetActorsByType("resonator"))
\tend)
end
"""


def write_map(source: Path, output: Path, title: str,
	block_west: str | None, block_east: str | None,
	include_east_tree: bool = True, renew_trees: bool = False,
	include_west_tree: bool = True, include_iron_followup: bool = False,
	blocker_owner: str = "Neutral", unit_blockers: bool = False,
	include_fallback_resonator: bool = False) -> None:
	with tempfile.TemporaryDirectory() as temporary:
		root = Path(temporary)
		with zipfile.ZipFile(source) as archive:
			archive.extractall(root)
		map_yaml = root / "map.yaml"
		text = without_tree_actors(map_yaml.read_text(encoding="utf-8-sig"))
		map_yaml.write_text(add_controlled_trees(text, title, include_east_tree,
			include_west_tree, include_iron_followup, include_fallback_resonator),
			encoding="utf-8")
		rules_yaml, lua = rules(block_west, block_east, renew_trees,
			blocker_owner, unit_blockers)
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
		"CNC-102 Blocked Then Continued Resonators", args.west_blocker,
		args.east_blocker, renew_trees=True)
	write_map(source, args.output / "cnc102-fancy-legal.oramap",
		"CNC-102 Legal Fancy Resonator Continuation", None, None,
		include_east_tree=False, renew_trees=True)
	write_map(source, args.output / "cnc102-iron-legal-continuation.oramap",
		"CNC-102 Iron Reaper Legal Resonator Continuation", None, None,
		include_west_tree=False, include_iron_followup=True)
	write_map(source, args.output / "cnc102-planner-owned-blockers.oramap",
		"CNC-102 Planner Owned Blockers", "43, 161", "60, 178",
		blocker_owner="Owned", unit_blockers=True)
	write_map(source, args.output / "cnc102-planner-fallback-spacing.oramap",
		"CNC-102 Planner Fallback Spacing", "43, 161", "60, 178",
		include_fallback_resonator=True)


if __name__ == "__main__":
	main()
