#!/usr/bin/env python3
"""Run interleaved serial CNC Normal/Fastest performance baseline repetitions."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import platform
import re
import shutil
import statistics
import subprocess
import sys
import tempfile
from datetime import datetime, timezone
from pathlib import Path

from performance_baseline_loader import load_builder


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def host_identity() -> dict[str, object]:
    memory_kib = None
    try:
        for line in Path("/proc/meminfo").read_text(encoding="utf-8").splitlines():
            if line.startswith("MemTotal:"):
                memory_kib = int(line.split()[1])
                break
    except OSError:
        pass
    cpu_model = None
    try:
        for line in Path("/proc/cpuinfo").read_text(encoding="utf-8", errors="replace").splitlines():
            if line.startswith("model name"):
                cpu_model = line.split(":", 1)[1].strip()
                break
    except OSError:
        pass
    return {
        "os": platform.system(),
        "kernel": platform.release(),
        "architecture": platform.machine(),
        "cpu_model": cpu_model,
        "logical_cpu_count": os.cpu_count(),
        "memory_total_kib": memory_kib,
        "python_runtime": platform.python_version(),
        "process_affinity": sorted(os.sched_getaffinity(0)) if hasattr(os, "sched_getaffinity") else None,
        "load_average": list(os.getloadavg()) if hasattr(os, "getloadavg") else None,
    }


def command_output(command: list[str], cwd: Path) -> str | None:
    try:
        result = subprocess.run(
            command, cwd=cwd, check=False, text=True, stdout=subprocess.PIPE,
            stderr=subprocess.DEVNULL,
        )
    except OSError:
        return None
    return result.stdout.strip() if result.returncode == 0 else None


def revision_identity(root: Path) -> dict[str, object]:
    status = command_output(["git", "status", "--porcelain=v1", "--untracked-files=normal"], root)
    return {
        "commit": command_output(["git", "rev-parse", "HEAD"], root),
        "branch": command_output(["git", "branch", "--show-current"], root),
        "dirty": bool(status) if status is not None else None,
        "dirty_paths": status.splitlines() if status else [],
    }


def cnc_mod_version(root: Path) -> str | None:
    try:
        text = (root / "mods" / "cnc" / "mod.yaml").read_text(encoding="utf-8")
    except OSError:
        return None
    match = re.search(r"^\s*Version:\s*(\S.*?)\s*$", text, re.MULTILINE)
    return match.group(1) if match else None


def checkout_identity(root: Path) -> dict[str, object]:
    revision = revision_identity(root)
    if not revision["commit"] or revision["dirty"] is None:
        raise ValueError(f"cannot establish Git revision identity for checkout: {root}")
    mod_version = cnc_mod_version(root)
    if mod_version is None:
        raise ValueError(f"cannot establish CNC mod version for checkout: {root}")
    return {
        "root": str(root),
        "revision": revision,
        "cnc_mod_version": mod_version,
    }


def measured_checkout_identity(launcher: Path) -> dict[str, object]:
    checkout_text = command_output(
        ["git", "rev-parse", "--show-toplevel"], launcher.parent
    )
    if not checkout_text:
        raise ValueError(f"launcher is not owned by a Git checkout: {launcher}")
    checkout = Path(checkout_text).resolve()
    expected_launcher = checkout / "launch-game.sh"
    if launcher != expected_launcher:
        raise ValueError(
            "alternate launcher must be the owning checkout's launch-game.sh: "
            f"{launcher}"
        )
    engine_assembly = checkout / "bin" / "OpenRA.dll"
    if not launcher.is_file() or not engine_assembly.is_file():
        raise ValueError(
            f"measured checkout is missing launch-game.sh or bin/OpenRA.dll: {checkout}"
        )
    identity = checkout_identity(checkout)
    identity.update({
        "launcher_relative_path": str(launcher.relative_to(checkout)),
        "launcher_sha256": sha256(launcher),
        "engine_assembly_relative_path": str(engine_assembly.relative_to(checkout)),
        "engine_assembly_sha256": sha256(engine_assembly),
    })
    return identity


def provenance_identity(workload_root: Path, launcher: Path) -> dict[str, object]:
    return {
        "workload_source": checkout_identity(workload_root),
        "measured_checkout": measured_checkout_identity(launcher),
    }


def artifact_inventory(output: Path, run_names: list[str]) -> list[dict[str, object]]:
    inventory = []
    for run_name in run_names:
        run_dir = output / run_name
        for path in sorted(run_dir.rglob("*")):
            if not path.is_file() or path.is_symlink():
                continue
            relative = path.relative_to(output)
            inventory.append({
                "path": str(relative),
                "size_bytes": path.stat().st_size,
                "sha256": sha256(path),
            })
    return inventory


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", required=True, type=Path, help="new artifact root")
    parser.add_argument("--launcher", type=Path)
    parser.add_argument("--content", required=True, type=Path)
    parser.add_argument("--repetitions", type=int, default=3)
    parser.add_argument("--timeout", type=float, default=1800)
    parser.add_argument("--no-xvfb", action="store_true")
    parser.add_argument("--build-configuration", choices=("Debug", "Release"), default="Debug")
    parser.add_argument("--task-identity", default="CNC-47")
    parser.add_argument("--profile-only", action="store_true")
    return parser.parse_args()


def distribution(values: list[float]) -> dict[str, float]:
    median = statistics.median(values)
    return {
        "minimum": min(values),
        "median": median,
        "maximum": max(values),
        "spread": max(values) - min(values),
        "cv": statistics.pstdev(values) / statistics.fmean(values) if len(values) > 1 else 0,
    }


def expected_players(config: dict[str, object]) -> list[dict[str, object]]:
    return [
        {
            "player": player["slot"],
            "bot_type": player["bot_type"],
            "faction": player["faction"],
            "team": player["team"],
            "spawn": player["spawn"],
        }
        for player in config["players"]
    ]


def lobby_commands(config: dict[str, object], speed: str) -> str:
    commands = [
        f"option gamespeed {speed}",
        "spectate",
        f"option shortgame {config['short_game']}",
        f"option startingcash {config['starting_cash']}",
    ]
    for index, player in enumerate(config["players"], 1):
        commands.append(
            f"slot_bot {player['slot']} 0 {player['bot_type']} {player['team']} {player['spawn']}"
        )
        commands.append(f"faction {index} {player['faction']}")
    return ";".join(commands)


def main() -> int:
    args = parse_args()
    if args.repetitions < 1:
        print("repetitions must be positive", file=sys.stderr)
        return 2
    output = args.output.resolve()
    if output.exists():
        print(f"output directory already exists: {output}", file=sys.stderr)
        return 2

    root = Path(__file__).resolve().parent
    config_path = root / "performance-baseline" / "workload.json"
    config = json.loads(config_path.read_text(encoding="utf-8"))
    base = config_path.parent
    source_map = (base / config["base_map"]).resolve()
    rules = (base / config["rules"]).resolve()
    launcher = (args.launcher or (root / "launch-game.sh")).resolve()
    try:
        provenance = provenance_identity(root, launcher)
    except ValueError as error:
        print(str(error), file=sys.stderr)
        return 2
    started_utc = datetime.now(timezone.utc).isoformat()
    dotnet_runtime = command_output(["dotnet", "--version"], root)
    host = host_identity()

    with tempfile.TemporaryDirectory(prefix="cnc47-baseline-") as temporary:
        temporary_path = Path(temporary)
        generated_map = temporary_path / "cnc47-performance.oramap"
        load_builder().build_map(source_map, rules, generated_map)

        measurement = {
            "warmup_tick": config["warmup_tick"],
            "measurement_ticks": config["measurement_ticks"],
            "sample_interval": config["sample_interval"],
            "minimum_bots": config["minimum_bots"],
            "minimum_live_mobile": config["minimum_live_mobile"],
            "expected_short_game": config["short_game"],
            "expected_starting_cash": config["starting_cash"],
            "expected_players": expected_players(config),
        }
        runs = []
        order = ["default", "fastest"] if args.repetitions % 2 else ["fastest", "default"]
        for repetition in range(1, args.repetitions + 1):
            for speed in (order if repetition % 2 else reversed(order)):
                speed_name = "normal" if speed == "default" else speed
                runs.append({
                    "name": f"{speed_name}-{repetition}",
                    "map": str(generated_map),
                    "seed": config["seed"],
                    "lobby_commands": lobby_commands(config, speed),
                    "exit_at_tick": config["warmup_tick"] + config["measurement_ticks"],
                    "minimum_world_tick": config["warmup_tick"] + config["measurement_ticks"],
                    "timeout_seconds": args.timeout,
                    "measurement": measurement,
                    "forbidden_log_patterns": ["Desync detected"],
                })
        if args.profile_only:
            runs = [{
                "name": "profile-fastest",
                "map": str(generated_map),
                "seed": config["seed"],
                "lobby_commands": lobby_commands(config, "fastest"),
                "exit_at_tick": config["warmup_tick"] + config["measurement_ticks"],
                "minimum_world_tick": config["warmup_tick"] + config["measurement_ticks"],
                "timeout_seconds": args.timeout,
                "measurement": measurement,
                "profile": config["profile"],
                "forbidden_log_patterns": ["Desync detected"],
            }]
        manifest_path = temporary_path / "manifest.json"
        manifest_path.write_text(json.dumps({
            "workload_identity": config["identity"],
            "runs": runs,
        }, indent=2) + "\n", encoding="utf-8")

        command = [
            sys.executable, str(root / "launch-ai-parallel.py"),
            "--manifest", str(manifest_path), "--output", str(output), "--jobs", "1",
            "--launcher", str(launcher), "--content", str(args.content.resolve()),
        ]
        if args.no_xvfb:
            command.append("--no-xvfb")
        try:
            result = subprocess.run(command, cwd=root, check=False)
            returncode = result.returncode
        except KeyboardInterrupt:
            returncode = 130

        if not output.is_dir():
            return returncode
        shutil.copy2(generated_map, output / "workload.oramap")
        shutil.copy2(manifest_path, output / "input-manifest.json")
        shutil.copy2(config_path, output / "workload.json")
        shutil.copy2(rules, output / "workload-rules.yaml")

    summary_path = output / "batch-summary.json"
    if not summary_path.is_file():
        return returncode
    summary = json.loads(summary_path.read_text(encoding="utf-8"))
    ratios: dict[str, list[float]] = {"default": [], "fastest": []}
    for run in summary["runs"]:
        measurement_summary = run.get("measurement_summary")
        if run["status"] == "passed" and measurement_summary and not args.profile_only:
            ratios[run["requested_speed_key"]].append(measurement_summary["real_game_time_ratio"])
    summary["baseline"] = {
        "schema": 1,
        "task_identity": args.task_identity,
        "result_kind": "profiled diagnostic" if args.profile_only else "golden paced timing",
        "workload_identity": config["identity"],
        "revision": provenance["measured_checkout"]["revision"],
        "cnc_mod_version": provenance["measured_checkout"]["cnc_mod_version"],
        "measured_checkout": provenance["measured_checkout"],
        "workload_source": provenance["workload_source"],
        "requested_build_configuration": args.build_configuration,
        "automation_mode": "local headless paced; rendering suppressed",
        "dotnet_runtime": dotnet_runtime,
        "workload_config_sha256": sha256(config_path),
        "input_manifest_sha256": sha256(output / "input-manifest.json"),
        "base_map_sha256": sha256(source_map),
        "rules_sha256": sha256(rules),
        "generated_map_sha256": sha256(output / "workload.oramap"),
        "launcher_sha256": provenance["measured_checkout"]["launcher_sha256"],
        "requested_jobs": 1,
        "requested_game_slots": 2,
        "requested_affinity": host["process_affinity"],
        "started_utc": started_utc,
        "finished_utc": datetime.now(timezone.utc).isoformat(),
        "host": host,
        "workload": {
            "seed": config["seed"],
            "warmup_tick": config["warmup_tick"],
            "measurement_ticks": config["measurement_ticks"],
            "sample_interval": config["sample_interval"],
            "minimum_live_mobile": config["minimum_live_mobile"],
            "players": config["players"],
        },
        "ratio_distributions": {
            speed: distribution(values) if values else None for speed, values in ratios.items()
        },
        "artifact_inventory": artifact_inventory(
            output, [run["name"] for run in summary["runs"]]
        ),
    }
    summary_path.write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    return returncode


if __name__ == "__main__":
    raise SystemExit(main())
