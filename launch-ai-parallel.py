#!/usr/bin/env python3
"""Run isolated Linux headless CNC games from a validated JSON manifest."""

from __future__ import annotations

import argparse
import csv
import json
import math
import os
import re
import shutil
import signal
import statistics
import subprocess
import sys
import time
import uuid
from collections import defaultdict
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


MAX_TICK_PATTERNS = (
    re.compile(r"MAX progress: world=(\d+)"),
    re.compile(r"world tick (\d+)", re.IGNORECASE),
)
HEADLESS_MARKER = "Headless automation enabled"
STARTED_PATTERN = re.compile(r"Headless (?:MAX|paced) automation started map")
NATURAL_PATTERN = re.compile(r"Headless (?:MAX|paced) automation reached natural game over")
BOUNDED_PATTERN = re.compile(r"Headless (?:MAX|paced) automation reached configured exit")
ACCEPTED_SPEED_PATTERN = re.compile(
    r"Headless automation accepted gamespeed key=(\w+), name=([^,]+), timestep=(\d+), maximum=(True|False)\."
)
ACCEPTED_LOBBY_PATTERN = re.compile(
    r"Performance baseline accepted lobby identity: shortgame=(True|False), "
    r"startingcash=([^,]+), bots=(.*)\."
)
SPEEDS = {
    "default": {"name": "Normal", "timestep": 40, "maximum": False},
    "fastest": {"name": "Fastest", "timestep": 20, "maximum": False},
    "max": {"name": "MAX", "timestep": 20, "maximum": True},
}
FATAL_PATTERN = re.compile(
    r"unhandled exception|fatal error|desync detected|exception of type", re.IGNORECASE
)
SAFE_NAME = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$")
ISOLATED_ARGUMENTS = (
    "Engine.SupportDir=",
    "Launch.Benchmark=",
    "Launch.ExitAtTick=",
    "Launch.GameSave=",
    "Launch.Headless=",
    "Launch.Map=",
)


class ConfigurationError(ValueError):
    pass


@dataclass
class RunSpec:
    name: str
    source_path: Path
    source_kind: str
    seed: int | None
    lobby_commands: str | None
    speed_key: str | None
    exit_at_tick: int | None
    minimum_world_tick: int
    timeout_seconds: float
    save_at_tick: int | None
    save_name: str
    required_log_patterns: list[str]
    forbidden_log_patterns: list[str]
    expected_artifacts: list[str]
    extra_args: list[str]
    measurement: dict[str, Any] | None
    profile: dict[str, Any] | None


@dataclass
class ActiveRun:
    spec: RunSpec
    run_dir: Path
    support_dir: Path
    runtime_input: Path
    display_start: int
    command: list[str]
    console_file: Any
    process: subprocess.Popen[Any]
    started_monotonic: float
    started_utc: str
    last_tick: int = 0
    timed_out: bool = False
    interrupted: bool = False


@dataclass
class BatchState:
    active: list[ActiveRun] = field(default_factory=list)
    interrupted: bool = False


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.ArgumentDefaultsHelpFormatter
    )
    default_jobs = max(1, min(3, (os.cpu_count() or 1) - 1))
    parser.add_argument("--manifest", required=True, type=Path, help="JSON run manifest")
    parser.add_argument("--output", type=Path, help="new batch artifact directory")
    parser.add_argument(
        "--jobs", type=int, choices=(1, 2, 3), default=default_jobs,
        help="maximum simultaneous games (leaves one detected CPU free)",
    )
    parser.add_argument("--launcher", type=Path, help="launch-game.sh override")
    parser.add_argument("--content", type=Path, help="installed CNC content directory")
    parser.add_argument("--settings-template", type=Path)
    parser.add_argument("--timeout", type=float, default=1800, help="default per-run seconds")
    parser.add_argument("--poll-interval", type=float, default=1)
    parser.add_argument("--progress-interval", type=float, default=30)
    parser.add_argument("--no-xvfb", action="store_true", help="testing/debugging only")
    return parser.parse_args(argv)


def resolve_path(value: str | Path, base: Path) -> Path:
    path = Path(value).expanduser()
    return (base / path).resolve() if not path.is_absolute() else path.resolve()


def resolve_mod_version(launcher: Path, configured_version: Any) -> str:
    if configured_version is not None:
        version = configured_version
    else:
        manifest = launcher.parent / "mods" / "cnc" / "mod.yaml"
        try:
            match = re.search(
                r"^\s*Version:\s*(\S.*?)\s*$", manifest.read_text(encoding="utf-8"), re.MULTILINE
            )
        except OSError as ex:
            raise ConfigurationError(f"Cannot read CNC mod version from {manifest}: {ex}") from ex
        if not match:
            raise ConfigurationError(f"Cannot find CNC mod version in {manifest}.")
        version = match.group(1)

    if not isinstance(version, str) or not version or Path(version).name != version:
        raise ConfigurationError("mod_version must be a single non-empty path segment.")
    return version


def load_manifest(path: Path, default_timeout: float) -> tuple[dict[str, Any], list[RunSpec]]:
    try:
        document = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as ex:
        raise ConfigurationError(f"Cannot read manifest {path}: {ex}") from ex

    if not isinstance(document, dict) or not isinstance(document.get("runs"), list):
        raise ConfigurationError("Manifest must be an object containing a 'runs' array.")
    if not document["runs"]:
        raise ConfigurationError("Manifest must define at least one run.")

    defaults = document.get("defaults", {})
    if not isinstance(defaults, dict):
        raise ConfigurationError("Manifest 'defaults' must be an object.")

    base = path.parent.resolve()
    specs: list[RunSpec] = []
    names: set[str] = set()
    for index, raw in enumerate(document["runs"]):
        if not isinstance(raw, dict):
            raise ConfigurationError(f"Run {index + 1} must be an object.")
        config = {**defaults, **raw}
        name = config.get("name")
        if not isinstance(name, str) or not SAFE_NAME.fullmatch(name):
            raise ConfigurationError(f"Run {index + 1} has an unsafe or missing name.")
        if name in names:
            raise ConfigurationError(f"Run name '{name}' is duplicated.")
        names.add(name)

        sources = [(kind, config.get(kind)) for kind in ("map", "game_save") if config.get(kind)]
        if len(sources) != 1:
            raise ConfigurationError(f"Run '{name}' must define exactly one of map or game_save.")
        source_kind, source_value = sources[0]
        if not isinstance(source_value, str):
            raise ConfigurationError(f"Run '{name}' {source_kind} must be a path string.")
        source_path = resolve_path(source_value, base)
        if not source_path.is_file():
            raise ConfigurationError(f"Run '{name}' input does not exist: {source_path}")

        lobby_commands = config.get("lobby_commands")
        speed_key = None
        if source_kind == "map":
            speed_matches = re.findall(
                r"(?:^|;)\s*option gamespeed (default|fastest|max)\s*(?=;|$)",
                lobby_commands if isinstance(lobby_commands, str) else "", re.IGNORECASE,
            )
            if len(speed_matches) != 1:
                raise ConfigurationError(
                    f"Run '{name}' map lobby_commands must select exactly one of gamespeed default, fastest, or max."
                )
            speed_key = speed_matches[0].lower()
            if "\n" in lobby_commands or "\r" in lobby_commands:
                raise ConfigurationError(f"Run '{name}' lobby_commands must not contain newlines.")
        elif lobby_commands is not None:
            raise ConfigurationError(f"Run '{name}' cannot use lobby_commands with game_save.")

        exit_at_tick = config.get("exit_at_tick")
        if exit_at_tick is not None and (not isinstance(exit_at_tick, int) or exit_at_tick < 1):
            raise ConfigurationError(f"Run '{name}' exit_at_tick must be a positive integer.")
        minimum_world_tick = config.get("minimum_world_tick", exit_at_tick or 0)
        if not isinstance(minimum_world_tick, int) or minimum_world_tick < 0:
            raise ConfigurationError(f"Run '{name}' minimum_world_tick must be a non-negative integer.")

        timeout_seconds = config.get("timeout_seconds", default_timeout)
        if not isinstance(timeout_seconds, (int, float)) or timeout_seconds <= 0:
            raise ConfigurationError(f"Run '{name}' timeout_seconds must be positive.")
        save_at_tick = config.get("save_at_tick")
        if save_at_tick is not None and (not isinstance(save_at_tick, int) or save_at_tick < 0):
            raise ConfigurationError(f"Run '{name}' save_at_tick must be a non-negative integer.")
        save_name = config.get("save_name", f"{name}.orasav")
        if not isinstance(save_name, str) or Path(save_name).name != save_name:
            raise ConfigurationError(f"Run '{name}' save_name must be a filename.")

        def string_list(key: str) -> list[str]:
            value = config.get(key, [])
            if not isinstance(value, list) or not all(isinstance(item, str) and item for item in value):
                raise ConfigurationError(f"Run '{name}' {key} must be an array of non-empty strings.")
            return value

        required_log_patterns = string_list("required_log_patterns")
        forbidden_log_patterns = string_list("forbidden_log_patterns")
        for pattern in required_log_patterns + forbidden_log_patterns:
            try:
                re.compile(pattern)
            except re.error as ex:
                raise ConfigurationError(f"Run '{name}' has an invalid log pattern '{pattern}': {ex}") from ex

        expected_artifacts = string_list("expected_artifacts")
        if any(Path(pattern).is_absolute() or ".." in Path(pattern).parts for pattern in expected_artifacts):
            raise ConfigurationError(f"Run '{name}' expected_artifacts must stay inside its support directory.")

        extra_args = string_list("extra_args")
        for argument in extra_args:
            if argument.startswith(ISOLATED_ARGUMENTS):
                raise ConfigurationError(f"Run '{name}' cannot override isolated argument '{argument}'.")

        measurement = config.get("measurement")
        if measurement is not None:
            if source_kind != "map" or speed_key not in ("default", "fastest"):
                raise ConfigurationError(f"Run '{name}' measurement requires a Normal or Fastest map run.")
            if not isinstance(measurement, dict):
                raise ConfigurationError(f"Run '{name}' measurement must be an object.")
            required_measurement = {
                "warmup_tick": 0,
                "measurement_ticks": 2500,
                "sample_interval": 1,
                "minimum_bots": 5,
                "minimum_live_mobile": 300,
            }
            for key, minimum in required_measurement.items():
                value = measurement.get(key)
                if not isinstance(value, int) or value < minimum:
                    raise ConfigurationError(
                        f"Run '{name}' measurement.{key} must be an integer of at least {minimum}."
                    )
            expected_short_game = measurement.get("expected_short_game")
            if not isinstance(expected_short_game, bool):
                raise ConfigurationError(
                    f"Run '{name}' measurement.expected_short_game must be a boolean."
                )
            expected_starting_cash = measurement.get("expected_starting_cash")
            if not isinstance(expected_starting_cash, int) or expected_starting_cash < 0:
                raise ConfigurationError(
                    f"Run '{name}' measurement.expected_starting_cash must be a non-negative integer."
                )
            expected_players = measurement.get("expected_players")
            if not isinstance(expected_players, list) or len(expected_players) < measurement["minimum_bots"]:
                raise ConfigurationError(
                    f"Run '{name}' measurement.expected_players must contain every required bot."
                )
            player_names = set()
            for player in expected_players:
                if not isinstance(player, dict) or set(player) != {
                    "player", "bot_type", "faction", "team", "spawn"
                }:
                    raise ConfigurationError(
                        f"Run '{name}' measurement.expected_players entries have an invalid schema."
                    )
                if not all(isinstance(player[key], str) and player[key] for key in (
                    "player", "bot_type", "faction"
                )) or not all(isinstance(player[key], int) and player[key] > 0 for key in ("team", "spawn")):
                    raise ConfigurationError(
                        f"Run '{name}' measurement.expected_players entries contain invalid values."
                    )
                if player["player"] in player_names:
                    raise ConfigurationError(
                        f"Run '{name}' measurement.expected_players contains duplicate players."
                    )
                player_names.add(player["player"])
            if measurement["measurement_ticks"] % measurement["sample_interval"]:
                raise ConfigurationError(
                    f"Run '{name}' measurement_ticks must be divisible by sample_interval."
                )
            expected_exit = measurement["warmup_tick"] + measurement["measurement_ticks"]
            if exit_at_tick is None or exit_at_tick < expected_exit:
                raise ConfigurationError(
                    f"Run '{name}' exit_at_tick must reach the measurement end tick {expected_exit}."
                )

        profile = config.get("profile")
        if profile is not None:
            if not isinstance(profile, dict) or set(profile) != {
                "kind", "long_tick_threshold_ms", "max_bytes", "top"
            }:
                raise ConfigurationError(f"Run '{name}' profile has an invalid schema.")
            if profile["kind"] != "simulation_perf_log":
                raise ConfigurationError(f"Run '{name}' profile.kind is unsupported.")
            if not isinstance(profile["long_tick_threshold_ms"], (int, float)) or \
                    profile["long_tick_threshold_ms"] <= 0:
                raise ConfigurationError(
                    f"Run '{name}' profile.long_tick_threshold_ms must be positive."
                )
            if not isinstance(profile["max_bytes"], int) or profile["max_bytes"] < 1024:
                raise ConfigurationError(f"Run '{name}' profile.max_bytes must be at least 1024.")
            if not isinstance(profile["top"], int) or not 1 <= profile["top"] <= 100:
                raise ConfigurationError(f"Run '{name}' profile.top must be between 1 and 100.")

        specs.append(RunSpec(
            name=name,
            source_path=source_path,
            source_kind=source_kind,
            seed=config.get("seed"),
            lobby_commands=lobby_commands,
            speed_key=speed_key,
            exit_at_tick=exit_at_tick,
            minimum_world_tick=minimum_world_tick,
            timeout_seconds=float(timeout_seconds),
            save_at_tick=save_at_tick,
            save_name=save_name,
            required_log_patterns=required_log_patterns,
            forbidden_log_patterns=forbidden_log_patterns,
            expected_artifacts=expected_artifacts,
            extra_args=extra_args,
            measurement=measurement,
            profile=profile,
        ))

        if specs[-1].seed is not None and not isinstance(specs[-1].seed, int):
            raise ConfigurationError(f"Run '{name}' seed must be an integer.")

    return document, specs


def create_settings(path: Path, template: Path | None) -> None:
    if template:
        shutil.copy2(template, path)
        return
    path.write_text(
        "Player:\nGame:\nSound:\nGraphics:\nServer:\nDebug:\n"
        f"\tUUID: {uuid.uuid4()}\n\tBotDebug: True\nKeys:\n",
        encoding="utf-8",
    )


def prepare_run(
    spec: RunSpec,
    output: Path,
    content: Path,
    settings_template: Path | None,
    launcher: Path,
    mod_version: str | None,
    display_start: int,
    no_xvfb: bool,
) -> tuple[Path, Path, Path, list[str]]:
    run_dir = output / spec.name
    support_dir = run_dir / "support"
    runtime_dir = run_dir / "runtime"
    support_dir.mkdir(parents=True)
    runtime_dir.mkdir()
    (support_dir / "Content").symlink_to(content, target_is_directory=True)
    create_settings(support_dir / "settings.yaml", settings_template)

    if spec.source_kind == "map":
        runtime_input = runtime_dir / "input.oramap"
        shutil.copy2(spec.source_path, runtime_input)
    else:
        if mod_version is None:
            raise ConfigurationError("A CNC mod version is required to stage a game save.")
        runtime_input = support_dir / "Saves" / "cnc" / mod_version / "input.orasav"
        runtime_input.parent.mkdir(parents=True)
        shutil.copy2(spec.source_path, runtime_input)

    command = []
    if not no_xvfb:
        command.extend(("xvfb-run", "--auto-servernum", f"--server-num={display_start}"))
    command.extend((
        str(launcher),
        f"Engine.SupportDir={support_dir}",
        "Debug.BotDebug=true",
        "Launch.Headless=true",
        f"Launch.Benchmark={spec.name}-",
    ))
    if spec.profile:
        command.extend((
            "Debug.EnableSimulationPerfLogging=true",
            f"Debug.LongTickThresholdMs={spec.profile['long_tick_threshold_ms']}",
        ))
    if spec.source_kind == "map":
        command.append(f"Launch.Map={runtime_input}")
        command.append(f"Launch.LobbyCommands={spec.lobby_commands}")
        if spec.seed is not None:
            command.append(f"Launch.RandomSeed={spec.seed}")
    else:
        command.append(f"Launch.GameSave={runtime_input}")
    if spec.exit_at_tick is not None:
        command.append(f"Launch.ExitAtTick={spec.exit_at_tick}")
    if spec.save_at_tick is not None:
        command.extend((
            f"Launch.SaveGameAtTick={spec.save_at_tick}",
            f"Launch.SaveGameName={spec.save_name}",
        ))
    command.extend(spec.extra_args)
    (run_dir / "command.json").write_text(json.dumps(command, indent=2) + "\n", encoding="utf-8")
    return run_dir, support_dir, runtime_input, command


def read_evidence(run: ActiveRun) -> tuple[str, list[Path]]:
    paths = [run.run_dir / "console.log"]
    logs_dir = run.support_dir / "Logs"
    if logs_dir.is_dir():
        paths.extend(sorted(logs_dir.glob("*.log")))
    chunks = []
    for path in paths:
        try:
            chunks.append(path.read_text(encoding="utf-8", errors="replace"))
        except OSError:
            pass
    return "\n".join(chunks), paths


def maximum_world_tick(text: str) -> int:
    ticks = [int(match.group(1)) for pattern in MAX_TICK_PATTERNS for match in pattern.finditer(text)]
    return max(ticks, default=0)


def terminate_process(run: ActiveRun, grace_seconds: float = 10) -> None:
    if run.process.poll() is not None:
        return
    try:
        os.killpg(run.process.pid, signal.SIGTERM)
        run.process.wait(timeout=grace_seconds)
    except ProcessLookupError:
        return
    except subprocess.TimeoutExpired:
        try:
            os.killpg(run.process.pid, signal.SIGKILL)
        except ProcessLookupError:
            pass
        run.process.wait()


def relative_files(root: Path, pattern: str) -> list[str]:
    return [str(path.relative_to(root)) for path in sorted(root.glob(pattern)) if path.is_file()]


def engine_benchmark_files(run: ActiveRun) -> list[str]:
    prefix = run.spec.name + "-"
    return [
        relative for relative in relative_files(run.support_dir, "Logs/*.csv")
        if Path(relative).name.startswith(prefix)
    ]


def summarize_profile(run: ActiveRun) -> tuple[dict[str, Any] | None, list[str]]:
    if run.spec.profile is None:
        return None, []

    path = run.support_dir / "Logs" / "perf.log"
    if not path.is_file():
        return None, ["profile output missing"]

    size = path.stat().st_size
    reasons = []
    if size > run.spec.profile["max_bytes"]:
        reasons.append(
            f"profile output {size} bytes exceeds {run.spec.profile['max_bytes']} byte bound"
        )

    pattern = re.compile(r"^\s*([0-9]+(?:\.[0-9]+)?) ms \[(\d+)\] ([^:]+): (.+)$")
    aggregate: dict[str, dict[str, float | int]] = {}
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        match = pattern.match(line)
        if not match:
            continue
        elapsed = float(match.group(1))
        label = f"{match.group(3)}: {match.group(4)}"
        item = aggregate.setdefault(label, {"count": 0, "total_ms": 0.0, "max_ms": 0.0})
        item["count"] += 1
        item["total_ms"] += elapsed
        item["max_ms"] = max(item["max_ms"], elapsed)

    if not aggregate:
        reasons.append("profile contains no simulation threshold events")
    ranked = sorted(
        ({"label": label, **values} for label, values in aggregate.items()),
        key=lambda item: (-item["total_ms"], -item["max_ms"], item["label"]),
    )[:run.spec.profile["top"]]
    return {
        "kind": run.spec.profile["kind"],
        "path": str(path.relative_to(run.support_dir)),
        "size_bytes": size,
        "max_bytes": run.spec.profile["max_bytes"],
        "long_tick_threshold_ms": run.spec.profile["long_tick_threshold_ms"],
        "event_count": sum(item["count"] for item in aggregate.values()),
        "hotspots": ranked,
    }, reasons


def percentile(values: list[float], fraction: float) -> float:
    if not values:
        raise ValueError("percentile requires at least one value")
    ordered = sorted(values)
    return ordered[max(0, math.ceil(len(ordered) * fraction) - 1)]


def summarize_benchmarks(
    run: ActiveRun, benchmark_files: list[str], local_start: int | None, local_end: int | None
) -> tuple[dict[str, Any], list[str]]:
    summaries: dict[str, Any] = {}
    reasons: list[str] = []
    prefix = run.spec.name + "-"
    for relative in benchmark_files:
        path = run.support_dir / relative
        if path.name == "performance-baseline.csv" or not path.name.startswith(prefix):
            continue
        stream = path.stem[len(prefix):]
        values = []
        try:
            with path.open(newline="", encoding="utf-8") as source:
                reader = csv.DictReader(source)
                if reader.fieldnames != ["tick", "time [ms]"]:
                    raise ValueError(f"unexpected header {reader.fieldnames}")
                for row in reader:
                    tick = int(row["tick"])
                    value = float(row["time [ms]"])
                    if not math.isfinite(value) or value < 0:
                        raise ValueError("non-finite or negative timing sample")
                    if local_start is None or local_start <= tick <= local_end:
                        values.append(value)
        except (OSError, ValueError, TypeError) as ex:
            reasons.append(f"benchmark stream {stream} is corrupt: {ex}")
            continue

        if not values:
            reasons.append(f"benchmark stream {stream} has no measured-window samples")
            continue
        summaries[stream] = {
            "samples": len(values),
            "median_ms": round(statistics.median(values), 6),
            "p95_ms": round(percentile(values, 0.95), 6),
            "p99_ms": round(percentile(values, 0.99), 6),
            "max_ms": round(max(values), 6),
        }

    if run.spec.measurement:
        for required in ("tick_time", "tick_actors", "bot_tick"):
            if required not in summaries:
                reasons.append(f"required benchmark stream {required} missing")
    return summaries, reasons


def analyze_effective_lobby(run: ActiveRun, text: str) -> tuple[dict[str, Any] | None, list[str]]:
    if run.spec.measurement is None:
        return None, []

    matches = list(ACCEPTED_LOBBY_PATTERN.finditer(text))
    if len(matches) != 1:
        return None, [
            "accepted performance-lobby identity marker missing"
            if not matches else "accepted performance-lobby identity marker is ambiguous"
        ]

    match = matches[0]
    reasons = []
    try:
        starting_cash = int(match.group(2))
        players = []
        for encoded in match.group(3).split(";"):
            player, bot_type, faction, team, spawn = encoded.split("|")
            players.append({
                "player": player,
                "bot_type": bot_type,
                "faction": faction,
                "team": int(team),
                "spawn": int(spawn),
            })
    except (TypeError, ValueError) as ex:
        return None, [f"accepted performance-lobby identity marker is corrupt: {ex}"]

    effective = {
        "short_game": match.group(1) == "True",
        "starting_cash": starting_cash,
        "players": sorted(players, key=lambda item: item["player"]),
    }
    expected = {
        "short_game": run.spec.measurement["expected_short_game"],
        "starting_cash": run.spec.measurement["expected_starting_cash"],
        "players": sorted(run.spec.measurement["expected_players"], key=lambda item: item["player"]),
    }
    if effective != expected:
        reasons.append(f"effective lobby identity {effective} does not match requested {expected}")
    return effective, reasons


def analyze_measurement(run: ActiveRun) -> tuple[dict[str, Any] | None, list[str]]:
    if run.spec.measurement is None:
        return None, []

    config = run.spec.measurement
    path = run.support_dir / "Logs" / "performance-baseline.csv"
    reasons = []
    try:
        with path.open(newline="", encoding="utf-8") as source:
            rows = list(csv.DictReader(source))
    except OSError as ex:
        return None, [f"performance baseline evidence missing: {ex}"]

    required_columns = {
        "world_tick", "local_tick", "elapsed_ms", "warmup_elapsed_ms",
        "total_live_actors", "total_effects",
        "player", "bot_type", "faction", "team", "spawn", "live_mobile", "queued",
        "moving", "busy", "orders", "cash", "resources", "earned", "spent",
        "units_killed", "units_dead",
    }
    if not rows or not required_columns.issubset(rows[0]):
        return None, ["performance baseline evidence is empty or has an invalid schema"]

    numeric_columns = required_columns - {"player", "bot_type", "faction"}
    float_columns = {"elapsed_ms", "warmup_elapsed_ms"}
    try:
        parsed = [
            {
                **row,
                **{key: float(row[key]) if key in float_columns else int(row[key]) for key in numeric_columns},
            }
            for row in rows
        ]
    except (ValueError, TypeError, KeyError) as ex:
        return None, [f"performance baseline evidence is corrupt: {ex}"]

    expected_ticks = list(range(
        config["warmup_tick"],
        config["warmup_tick"] + config["measurement_ticks"] + 1,
        config["sample_interval"],
    ))
    by_tick: dict[int, list[dict[str, Any]]] = defaultdict(list)
    for row in parsed:
        by_tick[row["world_tick"]].append(row)
    if sorted(by_tick) != expected_ticks:
        reasons.append("measurement samples do not cover the exact configured tick window")

    players = sorted({row["player"] for row in parsed})
    if len(players) < config["minimum_bots"]:
        reasons.append(f"measurement contains {len(players)} bots; {config['minimum_bots']} required")
    for tick, samples in by_tick.items():
        if sorted(row["player"] for row in samples) != players:
            reasons.append(f"measurement tick {tick} has an incomplete or duplicate bot roster")
        for row in samples:
            if row["live_mobile"] < config["minimum_live_mobile"]:
                reasons.append(
                    f"{row['player']} live-mobile count {row['live_mobile']} below "
                    f"{config['minimum_live_mobile']} at tick {tick}"
                )

    observed_ticks = sorted(by_tick)
    first_tick = observed_ticks[0]
    last_tick = observed_ticks[-1]
    first_rows = by_tick.get(first_tick, [])
    last_rows = by_tick.get(last_tick, [])
    first_elapsed = min((row["elapsed_ms"] for row in first_rows), default=0)
    measured_wall_ms = max((row["elapsed_ms"] for row in last_rows), default=0) - first_elapsed
    observed_ticks_count = last_tick - first_tick
    simulated_ms = observed_ticks_count * SPEEDS[run.spec.speed_key]["timestep"]
    complete_window = observed_ticks == expected_ticks
    if complete_window and measured_wall_ms <= 0:
        reasons.append("measured wall interval is not positive")

    per_player = {}
    for player in players:
        samples = sorted((row for row in parsed if row["player"] == player), key=lambda row: row["world_tick"])
        live = [row["live_mobile"] for row in samples]
        per_player[player] = {
            "bot_type": samples[0]["bot_type"],
            "faction": samples[0]["faction"],
            "team": samples[0]["team"],
            "spawn": samples[0]["spawn"],
            "live_mobile_min": min(live),
            "live_mobile_median": statistics.median(live),
            "live_mobile_max": max(live),
            "queued_max": max(row["queued"] for row in samples),
            "moving_samples_total": sum(row["moving"] for row in samples[1:]),
            "busy_samples_total": sum(row["busy"] for row in samples),
            "order_delta": samples[-1]["orders"] - samples[0]["orders"],
            "earned_delta": samples[-1]["earned"] - samples[0]["earned"],
            "spent_delta": samples[-1]["spent"] - samples[0]["spent"],
            "kills_delta": samples[-1]["units_killed"] - samples[0]["units_killed"],
            "deaths_delta": samples[-1]["units_dead"] - samples[0]["units_dead"],
        }
        if per_player[player]["moving_samples_total"] <= 0:
            reasons.append(f"{player} has no observable mobile movement during the measured window")

    if per_player and not any(
        item["earned_delta"] > 0 or item["spent_delta"] > 0 or item["queued_max"] > 0
        for item in per_player.values()
    ):
        reasons.append("no observable production/economy activity during the measured window")
    if per_player and not any(
        item["kills_delta"] > 0 or item["deaths_delta"] > 0 for item in per_player.values()
    ) and max((row["total_effects"] for row in parsed), default=0) == 0:
        reasons.append("no observable combat/effect activity during the measured window")

    local_start = min((row["local_tick"] for row in first_rows), default=0)
    local_end = max((row["local_tick"] for row in last_rows), default=0)
    summary = {
        "start_world_tick": first_tick,
        "end_world_tick": last_tick,
        "configured_start_world_tick": expected_ticks[0],
        "configured_end_world_tick": expected_ticks[-1],
        "complete_window": complete_window,
        "start_local_tick": local_start,
        "end_local_tick": local_end,
        "sample_interval": config["sample_interval"],
        "sample_count": len(observed_ticks),
        "configured_sample_count": len(expected_ticks),
        "warmup_wall_milliseconds": round(max((row["warmup_elapsed_ms"] for row in first_rows), default=0), 3),
        "measured_wall_milliseconds": round(measured_wall_ms, 3),
        "simulated_milliseconds": simulated_ms,
        "real_game_time_ratio": round(measured_wall_ms / simulated_ms, 6)
        if complete_window and measured_wall_ms > 0 and simulated_ms > 0 else None,
        "measured_ticks_per_second": round(observed_ticks_count * 1000 / measured_wall_ms, 6)
        if complete_window and measured_wall_ms > 0 else None,
        "total_live_actors_min": min((row["total_live_actors"] for row in parsed), default=0),
        "total_live_actors_max": max((row["total_live_actors"] for row in parsed), default=0),
        "total_effects_max": max((row["total_effects"] for row in parsed), default=0),
        "players": per_player,
    }
    return summary, reasons


def finalize_run(run: ActiveRun) -> dict[str, Any]:
    run.console_file.close()
    text, log_paths = read_evidence(run)
    tick = maximum_world_tick(text)
    duration = time.monotonic() - run.started_monotonic
    required = {
        pattern: re.search(pattern, text, re.MULTILINE) is not None
        for pattern in run.spec.required_log_patterns
    }
    forbidden = {
        pattern: re.search(pattern, text, re.MULTILINE) is not None
        for pattern in run.spec.forbidden_log_patterns
    }
    artifacts = {pattern: relative_files(run.support_dir, pattern) for pattern in run.spec.expected_artifacts}
    benchmark_files = engine_benchmark_files(run)
    replay_files = relative_files(run.support_dir, "Replays/**/*")
    save_files = relative_files(run.support_dir, "Saves/**/*")
    crash_files = relative_files(run.support_dir, "Logs/*crash*.log")

    reasons = []
    exit_code = run.process.returncode
    if run.interrupted:
        reasons.append("batch interrupted")
    if run.timed_out:
        reasons.append("timeout")
    if exit_code != 0:
        reasons.append(f"exit code {exit_code}")
    if HEADLESS_MARKER not in text:
        reasons.append("headless activation marker missing")
    if not STARTED_PATTERN.search(text):
        reasons.append("actual map/bot start marker missing")
    exit_pattern = BOUNDED_PATTERN if run.spec.exit_at_tick is not None else NATURAL_PATTERN
    if not exit_pattern.search(text):
        reasons.append(
            "configured exit marker missing"
            if run.spec.exit_at_tick is not None else "natural game-over marker missing"
        )
    if tick < run.spec.minimum_world_tick:
        reasons.append(f"world tick {tick} below required {run.spec.minimum_world_tick}")
    if not benchmark_files:
        reasons.append("benchmark output missing")
    if any(not matched for matched in required.values()):
        reasons.append("required log pattern missing")
    if any(forbidden.values()):
        reasons.append("forbidden log pattern present")
    if any(not files for files in artifacts.values()):
        reasons.append("expected artifact missing")
    if crash_files or FATAL_PATTERN.search(text):
        reasons.append("fatal/crash/desync signal present")

    accepted_match = ACCEPTED_SPEED_PATTERN.search(text)
    accepted_speed = None
    if not accepted_match:
        reasons.append("accepted speed/timestep marker missing")
    else:
        accepted_speed = {
            "key": accepted_match.group(1),
            "name": accepted_match.group(2),
            "timestep": int(accepted_match.group(3)),
            "maximum": accepted_match.group(4) == "True",
        }
        if run.spec.speed_key is not None:
            expected = SPEEDS[run.spec.speed_key]
            if accepted_speed != {"key": run.spec.speed_key, **expected}:
                reasons.append(f"accepted speed {accepted_speed} does not match requested {run.spec.speed_key}")

    effective_lobby, lobby_reasons = analyze_effective_lobby(run, text)
    reasons.extend(lobby_reasons)
    measurement_summary, measurement_reasons = analyze_measurement(run)
    reasons.extend(measurement_reasons)
    if measurement_summary:
        tick = max(tick, measurement_summary["end_world_tick"])
    benchmark_summary, benchmark_reasons = summarize_benchmarks(
        run, benchmark_files,
        measurement_summary["start_local_tick"] if measurement_summary else None,
        measurement_summary["end_local_tick"] if measurement_summary else None,
    )
    reasons.extend(benchmark_reasons)
    profile_summary, profile_reasons = summarize_profile(run)
    reasons.extend(profile_reasons)
    if run.spec.measurement and not replay_files:
        reasons.append("replay output missing")

    result = {
        "name": run.spec.name,
        "status": "passed" if not reasons else "failed",
        "reasons": reasons,
        "exit_code": exit_code,
        "duration_seconds": round(duration, 3),
        "maximum_world_tick": tick,
        "requested_speed_key": run.spec.speed_key,
        "accepted_speed": accepted_speed,
        "effective_lobby": effective_lobby,
        "started_utc": run.started_utc,
        "finished_utc": datetime.now(timezone.utc).isoformat(),
        "display_start": run.display_start,
        "network_endpoint": "engine-assigned ephemeral loopback",
        "source_copy": str(run.runtime_input),
        "support_directory": str(run.support_dir),
        "logs": [str(path.relative_to(run.run_dir)) for path in log_paths if path.exists()],
        "benchmarks": benchmark_files,
        "benchmark_summary": benchmark_summary,
        "profile_summary": profile_summary,
        "measurement_summary": measurement_summary,
        "replays": replay_files,
        "saves": save_files,
        "required_log_patterns": required,
        "forbidden_log_patterns": forbidden,
        "expected_artifacts": artifacts,
    }
    (run.run_dir / "summary.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    return result


def launch_run(
    spec: RunSpec,
    output: Path,
    content: Path,
    settings_template: Path | None,
    launcher: Path,
    mod_version: str | None,
    display_start: int,
    no_xvfb: bool,
    environment: dict[str, str],
) -> ActiveRun:
    run_dir, support_dir, runtime_input, command = prepare_run(
        spec, output, content, settings_template, launcher, mod_version, display_start, no_xvfb
    )
    console_file = (run_dir / "console.log").open("w", encoding="utf-8")
    started_utc = datetime.now(timezone.utc).isoformat()
    try:
        process = subprocess.Popen(
            command,
            cwd=launcher.parent,
            env=environment,
            stdout=console_file,
            stderr=subprocess.STDOUT,
            start_new_session=True,
        )
    except Exception:
        console_file.close()
        raise
    print(f"START {spec.name} pid={process.pid} display>={display_start}", flush=True)
    return ActiveRun(
        spec, run_dir, support_dir, runtime_input, display_start, command,
        console_file, process, time.monotonic(), started_utc
    )


def write_batch_summary(
    output: Path,
    jobs: int,
    started: float,
    results: list[dict[str, Any]],
    interrupted: bool,
) -> None:
    duration = time.monotonic() - started
    valid_ticks = sum(result["maximum_world_tick"] for result in results if result["status"] == "passed")
    summary = {
        "status": "passed"
        if results and all(result["status"] == "passed" for result in results) and not interrupted
        else "failed",
        "jobs": jobs,
        "run_count": len(results),
        "passed": sum(result["status"] == "passed" for result in results),
        "failed": sum(result["status"] != "passed" for result in results),
        "interrupted": interrupted,
        "duration_seconds": round(duration, 3),
        "valid_world_ticks": valid_ticks,
        "valid_world_ticks_per_second": round(valid_ticks / duration, 3) if duration else 0,
        "runs": results,
    }
    (output / "batch-summary.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    lines = ["name\tstatus\texit\tseconds\tworld_tick\treasons"]
    lines.extend(
        f"{item['name']}\t{item['status']}\t{item['exit_code']}\t{item['duration_seconds']}\t"
        f"{item['maximum_world_tick']}\t{' | '.join(item['reasons'])}"
        for item in results
    )
    (output / "batch-summary.tsv").write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(
        f"BATCH {summary['status']} passed={summary['passed']} failed={summary['failed']} "
        f"seconds={summary['duration_seconds']} valid_ticks_per_second={summary['valid_world_ticks_per_second']}",
        flush=True,
    )


def run_batch(
    specs: list[RunSpec],
    output: Path,
    jobs: int,
    content: Path,
    settings_template: Path | None,
    launcher: Path,
    mod_version: str | None,
    no_xvfb: bool,
    poll_interval: float,
    progress_interval: float,
) -> tuple[list[dict[str, Any]], bool]:
    state = BatchState()
    results: list[dict[str, Any]] = []
    pending = list(specs)
    environment = os.environ.copy()
    environment.update({"LIBGL_ALWAYS_SOFTWARE": "1", "ALSOFT_DRIVERS": "null"})
    next_display = 90
    next_progress = time.monotonic() + progress_interval

    def handle_signal(_number: int, _frame: Any) -> None:
        state.interrupted = True

    previous_handlers = {sig: signal.getsignal(sig) for sig in (signal.SIGINT, signal.SIGTERM)}
    for sig in previous_handlers:
        signal.signal(sig, handle_signal)

    try:
        while pending or state.active:
            while pending and len(state.active) < jobs and not state.interrupted:
                spec = pending.pop(0)
                try:
                    state.active.append(launch_run(
                        spec, output, content, settings_template, launcher,
                        mod_version, next_display, no_xvfb, environment
                    ))
                except Exception as ex:
                    run_dir = output / spec.name
                    run_dir.mkdir(parents=True, exist_ok=True)
                    result = {
                        "name": spec.name, "status": "failed", "reasons": [f"launcher error: {ex}"],
                        "exit_code": None, "duration_seconds": 0, "maximum_world_tick": 0,
                    }
                    (run_dir / "summary.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
                    results.append(result)
                next_display += 1

            if state.interrupted:
                for run in state.active:
                    run.interrupted = True
                    terminate_process(run)
                pending.clear()

            now = time.monotonic()
            for run in list(state.active):
                text, _ = read_evidence(run)
                run.last_tick = maximum_world_tick(text)
                if run.process.poll() is None and now - run.started_monotonic > run.spec.timeout_seconds:
                    run.timed_out = True
                    terminate_process(run)
                if run.process.poll() is not None:
                    result = finalize_run(run)
                    results.append(result)
                    state.active.remove(run)
                    print(
                        f"END {result['name']} {result['status']} exit={result['exit_code']} "
                        f"tick={result['maximum_world_tick']} seconds={result['duration_seconds']}",
                        flush=True,
                    )

            if now >= next_progress and state.active:
                status = ", ".join(
                    f"{run.spec.name}:tick={run.last_tick}:seconds={int(now - run.started_monotonic)}"
                    for run in state.active
                )
                print(f"PROGRESS {status}", flush=True)
                next_progress = now + progress_interval
            if pending or state.active:
                time.sleep(poll_interval)
    finally:
        for run in state.active:
            run.interrupted = True
            terminate_process(run)
            results.append(finalize_run(run))
        for sig, handler in previous_handlers.items():
            signal.signal(sig, handler)

    result_order = {spec.name: index for index, spec in enumerate(specs)}
    results.sort(key=lambda item: result_order[item["name"]])
    return results, state.interrupted


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv if argv is not None else sys.argv[1:])
    if not 0 < args.poll_interval <= 60 or not 0 < args.progress_interval <= 60:
        print("poll/progress intervals must be greater than zero and at most 60 seconds", file=sys.stderr)
        return 2

    repo_root = Path(__file__).resolve().parent
    manifest = args.manifest.resolve()
    try:
        document, specs = load_manifest(manifest, args.timeout)
        if args.launcher:
            launcher = args.launcher.resolve()
        else:
            launcher = resolve_path(document.get("launcher") or repo_root / "launch-game.sh", manifest.parent)
        if args.content:
            content = args.content.resolve()
        elif document.get("content"):
            content = resolve_path(document["content"], manifest.parent)
        else:
            candidate = repo_root / "Support" / "Content"
            if not candidate.is_dir():
                raise ConfigurationError("CNC content not found; pass --content or set manifest content.")
            content = candidate.resolve()
        if args.settings_template:
            settings_template = args.settings_template.resolve()
        elif document.get("settings_template"):
            settings_template = resolve_path(document["settings_template"], manifest.parent)
        else:
            settings_template = None
        if not launcher.is_file():
            raise ConfigurationError(f"Launcher does not exist: {launcher}")
        if not content.is_dir():
            raise ConfigurationError(f"Content directory does not exist: {content}")
        if settings_template and not settings_template.is_file():
            raise ConfigurationError(f"Settings template does not exist: {settings_template}")
        mod_version = (
            resolve_mod_version(launcher, document.get("mod_version"))
            if any(spec.source_kind == "game_save" for spec in specs) else None
        )
    except ConfigurationError as ex:
        print(f"configuration error: {ex}", file=sys.stderr)
        return 2

    output = args.output.resolve() if args.output else (
        repo_root / "AUTONOMOUS-CNC-LOGS" /
        f"parallel-{datetime.now(timezone.utc).strftime('%Y%m%d-%H%M%S')}"
    )
    try:
        output.mkdir(parents=True, exist_ok=False)
    except FileExistsError:
        print(f"output directory already exists: {output}", file=sys.stderr)
        return 2

    shutil.copy2(manifest, output / "manifest.json")
    started = time.monotonic()
    results, interrupted = run_batch(
        specs, output, args.jobs, content, settings_template, launcher, mod_version, args.no_xvfb,
        args.poll_interval, args.progress_interval
    )
    write_batch_summary(output, args.jobs, started, results, interrupted)
    return 0 if results and all(result["status"] == "passed" for result in results) and not interrupted else 1


if __name__ == "__main__":
    raise SystemExit(main())
