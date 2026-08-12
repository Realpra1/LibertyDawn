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

	high_unit_rules = """Player:
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

World:
\tStartingUnits@cnc100stressnod:
\t\tClass: cnc100-stress
\t\tClassName: CNC-100 high-unit failsafe stress
\t\tFactions: nod
\t\tBaseActor: mcv
\t\tSupportActors: harv, harv, harv, harv, e1, e1, e1, e1, e1, e2, e2, e3, e3, e4, e4, e5, bggy, bike, ltnk, ltnk, ftnk, htnk, arty, stnk, msam, tran, heli
\t\tOuterSupportRadius: 18
\t\tUpgrades: upgrade.covert1, upgrade.covert2, upgrade.covert3, upgrade.recon1, upgrade.recon2, upgrade.recon3, upgrade.economy1, upgrade.economy2, upgrade.economy3
\tStartingUnits@cnc100stressgdi:
\t\tClass: cnc100-stress
\t\tClassName: CNC-100 high-unit failsafe stress
\t\tFactions: gdi
\t\tBaseActor: mcv
\t\tSupportActors: harv, harv, harv, harv, e1, e1, e1, e1, e1, e2, e2, e3, e3, e4, e4, e5, jeep, mtnk, mtnk, mtnk, htnk, mlrs, msam, apc, tran, orca
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

World:
\tStartingUnits@cnc100mixednod:
\t\tClass: cnc100-mixed
\t\tClassName: CNC-100 mixed-owner blocked topology
\t\tFactions: nod
\t\tBaseActor: mcv
\t\tSupportActors: harv, sharv, harv, e6, rmbo, truck, tran, heli, stnk, arty, msam, e1, e1, e3, e4, bggy, bike, ltnk, ftnk
\t\tOuterSupportRadius: 16
\t\tUpgrades: upgrade.covert1, upgrade.covert2, upgrade.covert3, upgrade.recon1, upgrade.recon2, upgrade.recon3, upgrade.economy1, upgrade.economy2, upgrade.economy3
\tStartingUnits@cnc100mixedgdi:
\t\tClass: cnc100-mixed
\t\tClassName: CNC-100 mixed-owner blocked topology
\t\tFactions: gdi
\t\tBaseActor: mcv
\t\tSupportActors: harv, sharv, harv, e6, rmbo, truck, tran, orca, mlrs, msam, e1, e1, e3, e4, jeep, mtnk, htnk, apc
\t\tOuterSupportRadius: 16
\t\tUpgrades: upgrade.covert1, upgrade.covert2, upgrade.covert3, upgrade.recon1, upgrade.recon2, upgrade.recon3, upgrade.economy1, upgrade.economy2, upgrade.economy3
"""

	write_map(root / "mods/cnc/maps/Empire-Earth.oramap", args.output / "cnc100-high-unit.oramap",
		"CNC-100 High Unit Failsafe", high_unit_rules)
	write_map(root / "mods/cnc/maps/island-duel.oramap", args.output / "cnc100-mixed-archipelago.oramap",
		"CNC-100 Mixed Ownership Archipelago", mixed_rules)


if __name__ == "__main__":
	main()
