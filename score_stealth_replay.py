#!/usr/bin/env python3
"""Play one CNC replay and emit its permanent stealth-watchdog terminal rows.

Replay owner aggregation preserves actor-lifetime efficiency. Replay files do not
retain live SquadManager generation IDs, so owner-wide cadence is non-comparable
context and live per-squad cadence is reported unavailable.
"""

import argparse
import hashlib
import os
import re
import shutil
import signal
import subprocess
import sys
import time
from pathlib import Path


WATCHDOG_MARKERS = (
	"Stealth efficiency control membership",
	"stealth_efficiency_watchdog|",
	"Stealth kill watchdog",
	"AI stationary watchdog failure",
	"Stealth Obelisk death watchdog",
)


def sha256(path):
	digest = hashlib.sha256()
	with path.open("rb") as stream:
		for chunk in iter(lambda: stream.read(1024 * 1024), b""):
			digest.update(chunk)
	return digest.hexdigest()


def terminal_watchdog_lines(log_text, owner=None):
	lines = [line for line in log_text.splitlines() if any(marker in line for marker in WATCHDOG_MARKERS)]
	if owner is None:
		return lines

	owner_membership = [line for line in lines
		if "Stealth efficiency control membership" in line and f"owner={owner} " in line]
	bot_ids = set()
	for line in owner_membership:
		match = re.search(r"\bbot_id=(\d+)\b", line)
		if match:
			bot_ids.add(match.group(1))

	selected = []
	for line in lines:
		if f"owner={owner} " in line or line in owner_membership:
			selected.append(line)
		elif "stealth_efficiency_watchdog|" in line and any(f"|bot_id={bot_id}|" in line for bot_id in bot_ids):
			selected.append(line)
	return selected


def settings_text(uuid):
	return f"""Player:
Game:
Sound:
\tMute: True
\tMuteBackgroundMusic: True
Graphics:
\tMode: Windowed
\tWindowedSize: 320,240
\tVSync: False
\tCapFramerate: False
Server:
Debug:
\tUUID: {uuid}
\tBotDebug: True
Keys:
"""


def replay_identity(repo, replay):
	result = subprocess.run(
		[str(repo / "utility.sh"), "cnc", "--replay-metadata", str(replay)],
		cwd=repo, check=True, capture_output=True, text=True)
	version = re.search(r"^\s*Version:\s*(\S+)\s*$", result.stdout, re.MULTILINE)
	map_uid = re.search(r"^\s*MapUid:\s*(\S+)\s*$", result.stdout, re.MULTILINE)
	if version is None or map_uid is None:
		raise RuntimeError("replay metadata lacks version or map UID")
	return version.group(1), map_uid.group(1)


def stage_replay_runtime(repo, output, version):
	mod = output / "runtime/mods/cnc"
	shutil.copytree(repo / "mods/cnc", mod, copy_function=os.link)
	manifest = mod / "mod.yaml"
	text = manifest.read_text(encoding="utf-8")
	manifest.unlink()
	old = "\tVersion: {DEV_VERSION}"
	if old not in text:
		raise RuntimeError("CNC manifest does not contain the expected development version marker")
	manifest.write_text(text.replace(old, f"\tVersion: {version}", 1), encoding="utf-8")
	return mod


def build_command(repo, replay, output, lock_dir, mod):
	resource = repo / ".agents/skills/coordinate-cnc-development/scripts/with_resource_slots.py"
	launcher = repo / "launch-game.sh"
	benchmark = output / "benchmark"
	return [
		sys.executable, str(resource), "--lock-dir", str(lock_dir), "--resource", "game",
		"--capacity", "2", "--slots", "1", "--", "xvfb-run", "--auto-servernum", str(launcher),
		f"Game.Mod={mod}",
		f"Engine.SupportDir={output / 'support'}", "Debug.BotDebug=true",
		f"Launch.Benchmark={benchmark}", f"Launch.Replay={replay}",
	]


def supervise(command, repo, output, timeout):
	console = output / "console.log"
	debug = output / "support/Logs/debug.log"
	terminal_seen = False
	with console.open("w", encoding="utf-8") as stream:
		process = subprocess.Popen(command, cwd=repo, stdout=stream, stderr=subprocess.STDOUT,
			start_new_session=True)
		try:
			deadline = time.monotonic() + timeout
			while process.poll() is None and time.monotonic() < deadline:
				if debug.is_file() and "stealth_efficiency_watchdog|summary=terminal" in \
					debug.read_text(encoding="utf-8", errors="replace"):
					terminal_seen = True
					os.killpg(process.pid, signal.SIGINT)
					break
				time.sleep(0.25)
		except KeyboardInterrupt:
			if process.poll() is None:
				os.killpg(process.pid, signal.SIGINT)
				process.wait(timeout=15)
			raise

		if process.poll() is None and not terminal_seen:
			os.killpg(process.pid, signal.SIGINT)
		try:
			returncode = process.wait(timeout=15)
		except subprocess.TimeoutExpired:
			os.killpg(process.pid, signal.SIGKILL)
			returncode = process.wait()
	if debug.is_file() and "stealth_efficiency_watchdog|summary=terminal" in \
		debug.read_text(encoding="utf-8", errors="replace"):
		terminal_seen = True

	return returncode, terminal_seen


def main(argv=None):
	parser = argparse.ArgumentParser(description=__doc__)
	parser.add_argument("--replay", required=True, type=Path, help="input .orarep (never modified)")
	parser.add_argument("--output", required=True, type=Path, help="new isolated artifact directory")
	parser.add_argument("--owner", help="optional exact replay owner name to select")
	parser.add_argument("--timeout", type=int, default=3600, help="bounded playback seconds (default: 3600)")
	parser.add_argument("--repo", type=Path, default=Path(__file__).resolve().parent)
	parser.add_argument("--content", required=True, type=Path, help="installed CNC content directory")
	parser.add_argument("--lock-dir", type=Path, help="resource lock directory (default: REPO/.agents/locks)")
	args = parser.parse_args(argv)

	repo = args.repo.resolve()
	replay = args.replay.resolve()
	output = args.output.resolve()
	content = args.content.resolve()
	lock_dir = (args.lock_dir or repo / ".agents/locks").resolve()
	if not replay.is_file() or replay.suffix.lower() != ".orarep":
		parser.error(f"replay must be an existing .orarep file: {replay}")
	if args.timeout <= 0:
		parser.error("timeout must be positive")
	if output.exists():
		parser.error(f"output must not already exist: {output}")
	if not content.is_dir():
		parser.error(f"installed content directory not found: {content}")
	if not (repo / "launch-game.sh").is_file():
		parser.error(f"repository launcher not found: {repo / 'launch-game.sh'}")

	before = sha256(replay)
	(output / "support").mkdir(parents=True)
	(output / "support/Content").symlink_to(content, target_is_directory=True)
	(output / "support/settings.yaml").write_text(
		settings_text(f"stealth-replay-{before[:16]}"), encoding="utf-8")
	version, map_uid = replay_identity(repo, replay)
	mod = stage_replay_runtime(repo, output, version)
	command = build_command(repo, replay, output, lock_dir, mod)
	print("command=" + subprocess.list2cmdline(command), flush=True)
	returncode, terminal_seen = supervise(command, repo, output, args.timeout)
	after = sha256(replay)
	if before != after:
		raise RuntimeError("raw replay hash changed during playback")
	if not terminal_seen:
		raise RuntimeError(f"replay playback lacked terminal watchdog output (status {returncode})")

	debug_log = output / "support/Logs/debug.log"
	if not debug_log.is_file():
		raise RuntimeError(f"debug log missing after natural exit: {debug_log}")
	lines = terminal_watchdog_lines(debug_log.read_text(encoding="utf-8"), args.owner)
	terminal = [line for line in lines if "summary=terminal" in line]
	if not terminal:
		raise RuntimeError("permanent watchdog terminal summary was not emitted")

	summary = output / "permanent-watchdog-summary.txt"
	summary.write_text("\n".join([
		f"replay={replay}", f"sha256_before={before}", f"sha256_after={after}",
		f"replay_version={version}", f"map_uid={map_uid}",
		f"supervisor_exit_status={returncode}", "terminal_flush=observed-before-bounded-exit",
		f"owner={args.owner or 'all'}", "actor_time_denominator=sum-live-member-ticks",
		"replay_owner_cadence_comparable=false", "live_per_squad_cadence=unavailable",
		*lines, "",
	]), encoding="utf-8")
	print(summary)
	return 0


if __name__ == "__main__":
	raise SystemExit(main())
