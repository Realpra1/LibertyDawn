#!/usr/bin/env python3
"""Prove that the inactive-ground Stealth AI copy differs from Air only by identity."""

from __future__ import annotations

import hashlib
import re
import subprocess
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent
BASE_COMMIT = "0f807a81cf8e9be1b8f6b4c3abd7ad4314223fea"

ARCHIVED_OWNERS = {
	".agents/inspiration/stealth-ai-pre-air-copy/air-derived-nonowning-reference/StealthAIModule.cs.inspiration":
		"OpenRA.Mods.Common/Traits/BotModules/StealthAIModule.cs",
	".agents/inspiration/stealth-ai-pre-air-copy/air-derived-nonowning-reference/StealthAISquad.cs.inspiration":
		"OpenRA.Mods.Common/Traits/BotModules/Squads/StealthAISquad.cs",
}

COPIES = {
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
	text = re.sub(
		r"^[ \t]{3,}// BEGIN CNC96A GROUND EXTENSION\r?\n.*?"
		r"^[ \t]{3,}// END CNC96A GROUND EXTENSION\r?\n^[ \t]*\r?\n",
		"",
		text,
		flags=re.MULTILINE | re.DOTALL,
	)
	text = re.sub(
		r"(?:^[ \t]*\r?\n)?^[ \t]*// BEGIN CNC96A GROUND EXTENSION\r?\n.*?"
		r"^[ \t]*// END CNC96A GROUND EXTENSION\r?\n",
		"",
		text,
		flags=re.MULTILINE | re.DOTALL,
	)
	for copied, original in substitutions:
		text = re.sub(rf"\b{re.escape(copied)}\b", original, text)
	return text


def main() -> int:
	failed = False
	for archived_path, source_path in ARCHIVED_OWNERS.items():
		archived = (ROOT / archived_path).read_text(encoding="utf-8")
		original = subprocess.run(
			("git", "show", f"{BASE_COMMIT}:{source_path}"),
			cwd=ROOT, check=True, capture_output=True, text=True).stdout
		if archived != original:
			failed = True
			print(f"FAIL {archived_path} != {source_path} at {BASE_COMMIT}")
		else:
			digest = hashlib.sha256(original.encode("utf-8")).hexdigest()
			print(f"PASS archived non-owner {archived_path} == {source_path} at {BASE_COMMIT}; "
				f"lines={len(original.splitlines())} sha256={digest}")

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
