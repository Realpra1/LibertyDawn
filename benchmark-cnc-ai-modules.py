#!/usr/bin/env python3
"""Compare simple AttackMove fallback against the full CNC squad modules."""

from __future__ import annotations

import argparse
import json
import re
import statistics
import subprocess
import sys
import zipfile
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


TICK_SECONDS = 0.04
BOT_NAMES = ("SkyNet", "VIKI", "Brutalis", "IronReaper")
LOBBY_COMMANDS = (
    "spectate;option gamespeed max;option startingcash 20000;"
    "slot_bot Multi0 0 skynet 1 1;"
    "slot_bot Multi1 0 viki 2 6;"
    "slot_bot Multi2 0 brutalis 3 34;"
    "slot_bot Multi3 0 ironreaper 4 35"
)
BASELINE_RULES = """Player:
\t-TransportManagerBotModule:
\t-CrateCollectorBotModule:
\t-CrateCollectorBotModule@VIKI:
\t-RedTiberiumBombBotModule:
\t-OpeningGarrisonBotModule:
\t-CaptureManagerBotModule:
\t-SpecialOrderBotModule:
\tModularBot@Brutalis:
\t\tAdvancedSquadModulesInitiallyDisabled: true
\tModularBot@VIKI:
\t\tAdvancedSquadModulesInitiallyDisabled: true
\tModularBot@SkyNet:
\t\tAdvancedSquadModulesInitiallyDisabled: true
\tModularBot@IronReaper:
\t\tAdvancedSquadModulesInitiallyDisabled: true
\tSquadManagerBotModule@skynet:
\t\tSimpleAttackMoveFallbackWhenDisabled: true
\t\tFailsafeReconsiderInterval: 750
\t\tFailsafeDirectCombatTypes: e1, e2, e3, e4, e5, bggy, bike, jeep, ltnk, mtnk, ftnk, htnk, arty, mlrs, msam, ctnk, stnk, apc, heli, orca
\tSquadManagerBotModule@viki:
\t\tSimpleAttackMoveFallbackWhenDisabled: true
\t\tFailsafeReconsiderInterval: 750
\t\tFailsafeDirectCombatTypes: e1, e2, e3, e4, e5, bggy, bike, jeep, ltnk, mtnk, ftnk, htnk, arty, mlrs, msam, ctnk, stnk, apc, heli, orca
\tSquadManagerBotModule@brutalis:
\t\tSimpleAttackMoveFallbackWhenDisabled: true
\t\tFailsafeReconsiderInterval: 750
\t\tFailsafeDirectCombatTypes: e1, e2, e3, e4, e5, bggy, bike, jeep, ltnk, mtnk, ftnk, htnk, arty, mlrs, msam, ctnk, stnk, apc, heli, orca
\tSquadManagerBotModule@ironreaper:
\t\tSimpleAttackMoveFallbackWhenDisabled: true
\t\tFailsafeReconsiderInterval: 750
\t\tFailsafeDirectCombatTypes: e1, e2, e3, e4, e5, bggy, bike, jeep, ltnk, mtnk, ftnk, htnk, arty, mlrs, msam, ctnk, stnk, apc, heli, orca
"""


def parse_args() -> argparse.Namespace:
    root = Path(__file__).resolve().parent
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--content", required=True, type=Path)
    parser.add_argument("--map", type=Path, default=root / "mods/cnc/maps/Empire-Earth.oramap")
    parser.add_argument("--launcher", type=Path, default=root / "launch-game.sh")
    parser.add_argument("--output", type=Path)
    parser.add_argument("--runs", type=int, default=5)
    parser.add_argument("--seed", type=int, default=9609200)
    parser.add_argument("--timeout", type=float, default=1800)
    return parser.parse_args()


def create_baseline_map(source: Path, destination: Path) -> None:
    with zipfile.ZipFile(source, "r") as archive:
        map_yaml = archive.read("map.yaml").decode("utf-8")
        if re.search(r"^Rules:", map_yaml, re.MULTILINE):
            raise ValueError("The source map already defines custom rules.")

        with zipfile.ZipFile(destination, "w", zipfile.ZIP_DEFLATED) as output:
            for item in archive.infolist():
                data = archive.read(item.filename)
                if item.filename == "map.yaml":
                    data = (map_yaml.rstrip() + "\n\nRules: benchmark-baseline.yaml\n").encode("utf-8")
                output.writestr(item, data)
            output.writestr("benchmark-baseline.yaml", BASELINE_RULES)


def phase_manifest(
    phase: str,
    game_map: Path,
    runs: int,
    seed: int,
    timeout: float,
    initially_disabled: bool,
) -> dict[str, Any]:
    disabled = str(initially_disabled)
    required = [
        r"Headless MAX automation started map 'Empire Earth4'",
        r"bot=skynet",
        r"bot=viki",
        r"bot=brutalis",
        r"bot=ironreaper",
    ]
    required.extend(
        rf"Advanced squad failsafe \[{re.escape(name)}\].*initially-disabled={disabled}"
        for name in BOT_NAMES
    )
    forbidden = [
        r"Desync detected",
        r"unhandled exception",
        r"Failed to load rules",
    ]
    if not initially_disabled:
        forbidden.append(r"Advanced squad failsafe .*transition=disabled")

    return {
        "defaults": {
            "map": str(game_map),
            "mode": "headless",
            "bot_debug": False,
            "lobby_commands": LOBBY_COMMANDS,
            "timeout_seconds": timeout,
            "required_log_patterns": required,
            "forbidden_log_patterns": forbidden,
        },
        "runs": [
            {"name": f"{phase}-{index + 1}", "seed": seed + index}
            for index in range(runs)
        ],
    }


def periodic_metrics(path: Path) -> dict[str, float]:
    runtime: list[tuple[int, float]] = []
    tick_mean = 0.0
    module_milliseconds = 0.0
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        fields = line.split("\t")
        if len(fields) >= 4 and fields[0] == "runtime" and fields[1].isdigit():
            runtime.append((int(fields[1]), float(fields[2])))
        elif len(fields) >= 3 and fields[0] == "distribution" and fields[1] == "tick":
            match = re.search(r"\bmean_ms=([0-9.]+)", line)
            tick_mean = float(match.group(1)) if match else 0.0
        elif (len(fields) >= 5 and fields[0] == "module" and fields[2].isdigit()
              and fields[1].count("/") == 1):
            module_milliseconds += float(fields[3])

    if len(runtime) < 2:
        raise ValueError(f"Missing bounded runtime CPU samples in {path}")
    return {
        "cpu_seconds": max(0.0, runtime[-1][1] - runtime[0][1]) / 1000,
        "tick_mean_milliseconds": tick_mean,
        "module_cpu_seconds": module_milliseconds / 1000,
    }


def phase_results(batch: Path) -> dict[str, Any]:
    summary = json.loads((batch / "batch-summary.json").read_text(encoding="utf-8"))
    if summary["status"] != "passed":
        raise RuntimeError(f"Benchmark phase failed: {batch}")

    samples = []
    for run in summary["runs"]:
        reports = sorted((batch / run["name"] / "support/Logs").glob("*periodic-stall.tsv"))
        if len(reports) != 1:
            raise ValueError(f"Expected one periodic report for {run['name']}, found {len(reports)}")
        metrics = periodic_metrics(reports[0])
        world_ticks = run["maximum_engine_world_tick"]
        if world_ticks <= 0:
            raise ValueError(f"Natural game {run['name']} reported no world ticks")
        game_seconds = world_ticks * TICK_SECONDS
        metrics["module_cpu_seconds_per_1000_ticks"] = (
            metrics["module_cpu_seconds"] * 1000 / world_ticks
        )
        metrics["cpu_seconds_per_1000_ticks"] = (
            metrics["cpu_seconds"] * 1000 / world_ticks
        )
        wall_seconds = run["duration_seconds"]
        samples.append({
            "name": run["name"],
            "seed": json.loads((batch / "manifest.json").read_text(encoding="utf-8"))["runs"][
                len(samples)
            ]["seed"],
            "world_ticks": world_ticks,
            "game_seconds": game_seconds,
            "wall_seconds": wall_seconds,
            "game_seconds_per_wall_second": game_seconds / wall_seconds,
            **metrics,
        })

    def average(key: str) -> float:
        return statistics.fmean(sample[key] for sample in samples)

    return {
        "run_count": len(samples),
        "samples": samples,
        "averages": {
            key: average(key)
            for key in (
                "world_ticks",
                "game_seconds",
                "wall_seconds",
                "game_seconds_per_wall_second",
                "cpu_seconds",
                "cpu_seconds_per_1000_ticks",
                "tick_mean_milliseconds",
                "module_cpu_seconds",
                "module_cpu_seconds_per_1000_ticks",
            )
        },
    }


def comparison(baseline: dict[str, Any], full: dict[str, Any]) -> dict[str, Any]:
    baseline_average = baseline["averages"]
    full_average = full["averages"]

    def change(key: str) -> float:
        before = baseline_average[key]
        return 100 * (full_average[key] / before - 1) if before else 0.0

    return {
        "wall_time_change_percent": change("wall_seconds"),
        "game_time_change_percent": change("game_seconds"),
        "simulation_throughput_change_percent": change("game_seconds_per_wall_second"),
        "cpu_time_change_percent": change("cpu_seconds"),
        "normalized_cpu_time_change_percent": change("cpu_seconds_per_1000_ticks"),
        "mean_tick_time_change_percent": change("tick_mean_milliseconds"),
        "module_cpu_time_change_percent": change("module_cpu_seconds"),
        "normalized_module_cpu_change_percent": change("module_cpu_seconds_per_1000_ticks"),
        "full_faster_than_wall_clock": (
            full_average["game_seconds_per_wall_second"] > 1
        ),
    }


def write_markdown(path: Path, result: dict[str, Any]) -> None:
    baseline = result["baseline"]["averages"]
    full = result["full"]["averages"]
    delta = result["comparison"]
    lines = [
        "# CNC AI module performance benchmark",
        "",
        "- Map: Empire Earth4; every game ran to natural game-over.",
        "- Players: SkyNet spawn 1, VIKI spawn 6, Brutalis spawn 34, Iron Reaper spawn 35.",
        "- Execution: serial headless MAX games; identical seeds; bot debug logging disabled.",
        "",
        "| Mode | Runs | Avg ticks | Avg game seconds | Avg wall seconds | Game/wall | CPU sec/1k ticks | Avg tick ms | Module CPU sec/1k ticks |",
        "| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |",
        f"| Simple AttackMove baseline | {result['runs']} | {baseline['world_ticks']:.1f} | "
        f"{baseline['game_seconds']:.3f} | "
        f"{baseline['wall_seconds']:.3f} | {baseline['game_seconds_per_wall_second']:.3f}x | "
        f"{baseline['cpu_seconds_per_1000_ticks']:.3f} | {baseline['tick_mean_milliseconds']:.3f} | "
        f"{baseline['module_cpu_seconds_per_1000_ticks']:.3f} |",
        f"| Full modules | {result['runs']} | {full['world_ticks']:.1f} | "
        f"{full['game_seconds']:.3f} | "
        f"{full['wall_seconds']:.3f} | {full['game_seconds_per_wall_second']:.3f}x | "
        f"{full['cpu_seconds_per_1000_ticks']:.3f} | {full['tick_mean_milliseconds']:.3f} | "
        f"{full['module_cpu_seconds_per_1000_ticks']:.3f} |",
        "",
        f"Full-module game-duration change: {delta['game_time_change_percent']:+.2f}%. "
        f"Wall-time change: {delta['wall_time_change_percent']:+.2f}%. "
        f"Simulation-throughput change: {delta['simulation_throughput_change_percent']:+.2f}%. "
        f"Normalized CPU-time change: {delta['normalized_cpu_time_change_percent']:+.2f}%. "
        f"Mean-tick-time change: {delta['mean_tick_time_change_percent']:+.2f}%. "
        f"Normalized bot-module CPU-time change: "
        f"{delta['normalized_module_cpu_change_percent']:+.2f}%.",
        "",
        "Full modules are faster than wall clock."
        if delta["full_faster_than_wall_clock"]
        else "Full modules are not faster than wall clock.",
        "",
    ]
    path.write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    args = parse_args()
    if not 1 <= args.runs <= 20 or args.timeout <= 0:
        raise SystemExit("runs must be 1..20 and timeout must be positive")

    root = Path(__file__).resolve().parent
    output = args.output or (
        root / "AUTONOMOUS-CNC-LOGS" /
        f"ai-module-benchmark-{datetime.now(timezone.utc).strftime('%Y%m%d-%H%M%S')}"
    )
    output = output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    baseline_map = output / "empire-earth-baseline.oramap"
    create_baseline_map(args.map.resolve(), baseline_map)

    manifests = {
        "baseline": phase_manifest(
            "baseline", baseline_map, args.runs, args.seed, args.timeout, True,
        ),
        "full": phase_manifest(
            "full", args.map.resolve(), args.runs, args.seed, args.timeout, False,
        ),
    }
    for phase, manifest in manifests.items():
        manifest_path = output / f"{phase}-manifest.json"
        manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
        subprocess.run([
            sys.executable,
            str(root / "launch-ai-parallel.py"),
            "--manifest", str(manifest_path),
            "--output", str(output / phase),
            "--jobs", "1",
            "--poll-interval", "0.1",
            "--launcher", str(args.launcher.resolve()),
            "--content", str(args.content.resolve()),
        ], check=True, cwd=root)

    result = {
        "format": "cnc-ai-module-benchmark-v1",
        "created_utc": datetime.now(timezone.utc).isoformat(),
        "runs": args.runs,
        "completion": "natural-game-over",
        "seeds": [args.seed + index for index in range(args.runs)],
        "baseline": phase_results(output / "baseline"),
        "full": phase_results(output / "full"),
    }
    result["comparison"] = comparison(result["baseline"], result["full"])
    (output / "benchmark-summary.json").write_text(
        json.dumps(result, indent=2) + "\n", encoding="utf-8",
    )
    write_markdown(output / "benchmark-summary.md", result)
    print(output / "benchmark-summary.md")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
