#!/usr/bin/env python3
"""Create ignored CNC-100 adversarial maps from canonical shipped maps."""

from __future__ import annotations

import argparse
import tempfile
import zipfile
from pathlib import Path


def write_map(source: Path, output: Path, title: str, rules: str) -> None:
	with tempfile.TemporaryDirectory() as temporary:
		root = Path(temporary)
		with zipfile.ZipFile(source) as archive:
			archive.extractall(root)
		map_yaml = root / "map.yaml"
		text = map_yaml.read_text(encoding="utf-8-sig")
		start = text.index("Title:")
		end = text.index("\n", start)
		text = text[:start] + f"Title: {title}" + text[end:]
		if "\nRules:" not in text:
			text = text.rstrip() + "\n\nRules: rules.yaml\n"
		map_yaml.write_text(text, encoding="utf-8")
		(root / "rules.yaml").write_text(rules, encoding="utf-8")
		output.parent.mkdir(parents=True, exist_ok=True)
		with zipfile.ZipFile(output, "w", zipfile.ZIP_DEFLATED) as archive:
			for path in sorted(root.iterdir()):
				archive.write(path, path.name)


def main() -> None:
	parser = argparse.ArgumentParser()
	parser.add_argument("--output", required=True, type=Path)
	args = parser.parse_args()
	root = Path(__file__).resolve().parent.parent
	# Keep the same scenario bytes for enabled and disabled feature legs. The
	# performance contract requires at least 300 representative units per AI.
	high_unit_support = ", ".join(
		["e1"] * 80 + ["e2"] * 40 + ["e3"] * 30 + ["e4"] * 20 + ["e5"] * 10 +
		["bggy"] * 20 + ["bike"] * 20 + ["jeep"] * 20 + ["ltnk"] * 25 +
		["mtnk"] * 25 + ["ftnk"] * 10 + ["htnk"] * 10 + ["stnk"] * 10 + ["ctnk"] * 10 +
		["arty"] * 5 + ["msam"] * 5
	)

	high_unit_rules = f"""Player:
\tModularBot@IronReaperObserver:
\t\tName: Iron Reaper Observer
\t\tType: ironreaper-observer
\tModularBot@IronReaper:
\t\tAdvancedSquadSampleInterval: 40
\t\tAdvancedSquadBreachSamples: 2
\t\tAdvancedSquadRecoverySamples: 2
\t\tAdvancedSquadOffenderPenaltySamples: 2
\tGrantConditionOnBotOwner@ironreaper:
\t\tBots: ironreaper, ironreaper-observer
\tGrantConditionOnBotOwner@ironreaper-tech-counter:
\t\tBots: ironreaper, ironreaper-observer
\tSquadManagerBotModule@ironreaper:
\t\tGroundTargetDebugLogging: true
\tStealthTankSquadBotModule:
\t\tDebugLogging: true
\t\tFailsafeTestAdvancedWorkMilliseconds: 15
\t\tFailsafeTestAdvancedWorkFromTick: 80
\t\tFailsafeTestAdvancedWorkUntilTick: 280
\tStealthTankSquadBotModule@chemical:
\t\tDebugLogging: true

World:
\tStartingUnits@cnc100stressnod:
\t\tClass: cnc100-stress
\t\tClassName: CNC-100 high-unit failsafe stress
\t\tFactions: nod
\t\tBaseActor: mcv
\t\tSupportActors: harv, harv, harv, harv, {high_unit_support}
\t\tOuterSupportRadius: 18
\t\tUpgrades: upgrade.covert1, upgrade.covert2, upgrade.covert3, upgrade.recon1, upgrade.recon2, upgrade.recon3, upgrade.economy1, upgrade.economy2, upgrade.economy3
\tStartingUnits@cnc100stressgdi:
\t\tClass: cnc100-stress
\t\tClassName: CNC-100 high-unit failsafe stress
\t\tFactions: gdi
\t\tBaseActor: mcv
\t\tSupportActors: harv, harv, harv, harv, {high_unit_support}
\t\tOuterSupportRadius: 18
\t\tUpgrades: upgrade.covert1, upgrade.covert2, upgrade.covert3, upgrade.recon1, upgrade.recon2, upgrade.recon3, upgrade.economy1, upgrade.economy2, upgrade.economy3
"""

	mixed_rules = """Player:
\tModularBot@IronReaperObserver:
\t\tName: Iron Reaper Observer
\t\tType: ironreaper-observer
\tModularBot@IronReaper:
\t\tAdvancedSquadSampleInterval: 30
\t\tAdvancedSquadBreachSamples: 2
\t\tAdvancedSquadRecoverySamples: 2
\t\tAdvancedSquadOffenderPenaltySamples: 1
\tGrantConditionOnBotOwner@ironreaper:
\t\tBots: ironreaper, ironreaper-observer
\tGrantConditionOnBotOwner@ironreaper-tech-counter:
\t\tBots: ironreaper, ironreaper-observer
\tSquadManagerBotModule@ironreaper:
\t\tGroundTargetDebugLogging: true
\t\tFailsafeReconsiderInterval: 75
\tStealthTankSquadBotModule:
\t\tDebugLogging: true
\tStealthTankSquadBotModule@chemical:
\t\tDebugLogging: true
\t\tFailsafeTestAdvancedWorkMilliseconds: 15
\t\tFailsafeTestAdvancedWorkFromTick: 60
\t\tFailsafeTestAdvancedWorkUntilTick: 300

World:
\tStartingUnits@cnc100mixednod:
\t\tClass: cnc100-mixed
\t\tClassName: CNC-100 mixed-owner blocked topology
\t\tFactions: nod
\t\tBaseActor: mcv
\t\tSupportActors: harv, sharv, harv, e6, rmbo, truck, tran, heli, stnk, stnk, stnk, stnk, ctnk, ctnk, ctnk, ctnk, arty, msam, e1, e1, e3, e4, bggy, bike, ltnk, ftnk
\t\tOuterSupportRadius: 16
\t\tUpgrades: upgrade.covert1, upgrade.covert2, upgrade.covert3, upgrade.recon1, upgrade.recon2, upgrade.recon3, upgrade.economy1, upgrade.economy2, upgrade.economy3
\tStartingUnits@cnc100mixedgdi:
\t\tClass: cnc100-mixed
\t\tClassName: CNC-100 mixed-owner blocked topology
\t\tFactions: gdi
\t\tBaseActor: mcv
\t\tSupportActors: harv, sharv, harv, e6, rmbo, truck, tran, orca, stnk, stnk, stnk, stnk, ctnk, ctnk, ctnk, ctnk, mlrs, msam, e1, e1, e3, e4, jeep, mtnk, htnk, apc
\t\tOuterSupportRadius: 16
\t\tUpgrades: upgrade.covert1, upgrade.covert2, upgrade.covert3, upgrade.recon1, upgrade.recon2, upgrade.recon3, upgrade.economy1, upgrade.economy2, upgrade.economy3
"""

	covert_rules = """Player:
	ModularBot@IronReaper:
		AdvancedSquadSampleInterval: 30
		AdvancedSquadBreachSamples: 2
		AdvancedSquadRecoverySamples: 2
		AdvancedSquadOffenderPenaltySamples: 1
	SquadManagerBotModule@ironreaper:
		GroundTargetDebugLogging: true
	CovertHarassmentBotModule:
		DebugLogging: true

World:
	StartingUnits@cnc100covertnod:
		Class: cnc100-covert
		ClassName: CNC-100 covert harassment participation
		Factions: nod
		BaseActor: mcv
		SupportActors: harv, harv, sharv, bike, bike, bike, bike, bike, bike, bggy, bggy, bggy, bggy, bggy, bggy, arty, arty, arty, arty, msam, msam, mtnk, mtnk, e1, e1, e3, e4
		OuterSupportRadius: 14
		Upgrades: upgrade.covert1, upgrade.covert2, upgrade.covert3, upgrade.recon1, upgrade.recon2, upgrade.recon3, upgrade.economy1, upgrade.economy2, upgrade.economy3
	StartingUnits@cnc100covertgdi:
		Class: cnc100-covert
		ClassName: CNC-100 covert harassment participation
		Factions: gdi
		BaseActor: mcv
		SupportActors: harv, harv, sharv, bike, bike, bike, bike, bike, bike, bggy, bggy, bggy, bggy, bggy, bggy, arty, arty, arty, arty, msam, msam, mtnk, mtnk, e1, e1, e3, e4
		OuterSupportRadius: 14
		Upgrades: upgrade.covert1, upgrade.covert2, upgrade.covert3, upgrade.recon1, upgrade.recon2, upgrade.recon3, upgrade.economy1, upgrade.economy2, upgrade.economy3
"""

	write_map(root / "mods/cnc/maps/Empire-Earth.oramap", args.output / "cnc100-high-unit.oramap",
		"CNC-100 High Unit Failsafe", high_unit_rules)
	write_map(root / "mods/cnc/maps/Empire-Earth.oramap", args.output / "cnc100-high-unit-failsafe-disabled.oramap",
		"CNC-100 High Unit Failsafe Disabled", high_unit_rules.replace(
			"\t\tAdvancedSquadSampleInterval: 40", "\t\tAdvancedSquadCpuFailsafe: false\n\t\tAdvancedSquadSampleInterval: 40"))
	write_map(root / "mods/cnc/maps/island-duel.oramap", args.output / "cnc100-mixed-archipelago.oramap",
		"CNC-100 Mixed Ownership Archipelago", mixed_rules)
	write_map(root / "mods/cnc/maps/island-duel.oramap", args.output / "cnc100-covert-harassment.oramap",
		"CNC-100 Covert Harassment Participation", covert_rules)


if __name__ == "__main__":
	main()
