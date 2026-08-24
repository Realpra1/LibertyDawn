#!/usr/bin/env python3
"""Prove that the inactive-ground Stealth AI copy differs from Air only by identity."""

from __future__ import annotations

import hashlib
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent

COPIES = {
	"OpenRA.Mods.Common/Traits/BotModules/StealthAIModule.cs": (
		"OpenRA.Mods.Common/Traits/BotModules/SquadManagerBotModule.cs",
		(
			("StealthAIGeneralGroundMissionStatus", "GeneralGroundMissionStatus"),
			("StealthAIModuleInfo", "SquadManagerBotModuleInfo"),
			("StealthAIModule", "SquadManagerBotModule"),
			("StealthAISquadType", "SquadType"),
			("StealthAISquad", "Squad"),
			("StealthAIThreatGeometry", "AirThreatGeometry"),
		),
	),
	"OpenRA.Mods.Common/Traits/BotModules/Squads/StealthAISquad.cs": (
		"OpenRA.Mods.Common/Traits/BotModules/Squads/Squad.cs",
		(
			("StealthAIModuleInfo", "SquadManagerBotModuleInfo"),
			("StealthAIModule", "SquadManagerBotModule"),
			("StealthAISquadType", "SquadType"),
			("StealthAISquad", "Squad"),
			("StealthAIIdleState", "AirIdleState"),
		),
	),
	"OpenRA.Mods.Common/Traits/BotModules/Squads/States/StealthAIStates.cs": (
		"OpenRA.Mods.Common/Traits/BotModules/Squads/States/AirStates.cs",
		(
			("StealthAIModuleInfo", "SquadManagerBotModuleInfo"),
			("StealthAIModule", "SquadManagerBotModule"),
			("StealthAISquadType", "SquadType"),
			("StealthAISquad", "Squad"),
			("StealthAIStateBase", "AirStateBase"),
			("StealthAIIdleState", "AirIdleState"),
			("StealthAIAttackState", "AirAttackState"),
			("StealthAIFleeState", "AirFleeState"),
			("StealthAIThreatGeometry", "AirThreatGeometry"),
			("StealthAIDefendedAirAction", "DefendedAirAction"),
			("StealthAILocalAaClearResponse", "LocalAaClearResponse"),
		),
	),
	"OpenRA.Mods.Common/Traits/BotModules/BotModuleLogic/StealthAIThreatGeometry.cs": (
		"OpenRA.Mods.Common/Traits/BotModules/BotModuleLogic/AirThreatGeometry.cs",
		(
			("StealthAIThreatGeometry", "AirThreatGeometry"),
			("StealthAIDefendedAirAction", "DefendedAirAction"),
			("StealthAILocalAaClearResponse", "LocalAaClearResponse"),
		),
	),
}


def reverse_identities(text: str, substitutions: tuple[tuple[str, str], ...]) -> str:
	for copied, original in substitutions:
		text = re.sub(rf"\b{re.escape(copied)}\b", original, text)
	return text


def main() -> int:
	failed = False
	for copied_path, (air_path, substitutions) in COPIES.items():
		copied = (ROOT / copied_path).read_text(encoding="utf-8")
		original = (ROOT / air_path).read_text(encoding="utf-8")
		normalized = reverse_identities(copied, substitutions)
		if normalized != original:
			failed = True
			print(f"FAIL {copied_path} != {air_path} after identity reversal")
		else:
			digest = hashlib.sha256(original.encode("utf-8")).hexdigest()
			print(f"PASS {copied_path} == {air_path} after identity reversal; "
				f"lines={len(original.splitlines())} sha256={digest}")

	return 1 if failed else 0


if __name__ == "__main__":
	raise SystemExit(main())
