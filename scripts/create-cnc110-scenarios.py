#!/usr/bin/env python3
"""Create ignored CNC-110 HeavyDrop lifecycle scenarios from shipped maps."""

from __future__ import annotations

import argparse
import tempfile
import zipfile
from pathlib import Path


COMMON_RULES = """Player:
\tTransportManagerBotModule:
\t\tScanInterval: 1
\t\tHeavyDropMinimumGameTicks: 0
\t\tHeavyDropCooldownTicks: 7500
\t\tDebugLogging: true

World:
\tLuaScript:
\t\tScripts: utils.lua, cnc110.lua
\tStartingUnits@cnc110gdi:
\t\tClass: cnc110
\t\tClassName: CNC-110 HeavyDrop lifecycle
\t\tFactions: gdi
\t\tBaseActor: mcv
\t\tSupportActors: harv, tran, tran, tran, tran, tran, tran, tran, tran, tran, tran, {passengers}
\t\tOuterSupportRadius: 12
\tStartingUnits@cnc110nod:
\t\tClass: cnc110
\t\tClassName: CNC-110 HeavyDrop lifecycle target
\t\tFactions: nod
\t\tBaseActor: mcv
\t\tSupportActors: harv, harv, bggy, bike, ltnk, e1, e1, e3
\t\tOuterSupportRadius: 10
"""


def write_map(source: Path, output: Path, title: str, rules: str, script: str) -> None:
	with tempfile.TemporaryDirectory() as temporary:
		root = Path(temporary)
		with zipfile.ZipFile(source) as archive:
			archive.extractall(root)

		map_yaml = root / "map.yaml"
		text = map_yaml.read_text(encoding="utf-8-sig")
		start = text.index("Title:")
		end = text.index("\n", start)
		text = text[:start] + f"Title: {title}" + text[end:]
		text = text.rstrip() + "\n\nRules: rules.yaml\nScript: cnc110.lua\n"
		map_yaml.write_text(text, encoding="utf-8")
		(root / "rules.yaml").write_text(rules, encoding="utf-8")
		(root / "cnc110.lua").write_text(script, encoding="utf-8")

		output.parent.mkdir(parents=True, exist_ok=True)
		with zipfile.ZipFile(output, "w", zipfile.ZIP_DEFLATED) as archive:
			for path in sorted(root.iterdir()):
				archive.write(path, path.name)


def main() -> None:
	parser = argparse.ArgumentParser()
	parser.add_argument("--output", required=True, type=Path)
	args = parser.parse_args()
	root = Path(__file__).resolve().parent.parent
	source = root / "mods/cnc/maps/island-duel.oramap"
	passengers = ", ".join(["htnk"] * 10)

	carrier_script = """Brutalis = nil

WorldLoaded = function()
\tBrutalis = Player.GetPlayer("Multi0")
\tTrigger.AfterDelay(50, function()
\t\tlocal transports = Brutalis.GetActorsByType("tran")
\t\tif #transports > 0 then transports[1].Destroy() end
\tend)
end
"""

	passenger_script = """Brutalis = nil

WorldLoaded = function()
\tBrutalis = Player.GetPlayer("Multi0")
\tTrigger.AfterDelay(50, function()
\t\tlocal passengers = Brutalis.GetActorsByType("htnk")
\t\tif #passengers > 0 then passengers[#passengers].Destroy() end
\tend)
end
"""

	timeout_rules = COMMON_RULES.format(
		passengers=", ".join(["htnk"] * 8 + ["htnk.blocked"] * 2)) + """
Player:
\tTransportManagerBotModule:
\t\tHeavyDropConcurrentBoarding: 8
\t\tHeavyDropGatherTimeoutTicks: 600
\t\tHeavyDropPassengerTypes: htnk, htnk.blocked

htnk.blocked:
\tInherits: HTNK
\tRenderSprites:
\t\tImage: htnk
\tGrantCondition@cnc110blocked:
\t\tCondition: cnc110-blocked
\tMobile:
\t\tPauseOnCondition: cnc110-blocked
"""

	write_map(source, args.output / "cnc110-carrier-invalid.oramap",
		"CNC-110 Carrier Lifecycle", COMMON_RULES.format(passengers=passengers), carrier_script)
	write_map(source, args.output / "cnc110-passenger-invalid.oramap",
		"CNC-110 Passenger Lifecycle", COMMON_RULES.format(passengers=passengers), passenger_script)
	write_map(source, args.output / "cnc110-eight-loaded-timeout.oramap",
		"CNC-110 Eight Loaded Timeout", timeout_rules, "WorldLoaded = function() end\n")


if __name__ == "__main__":
	main()
