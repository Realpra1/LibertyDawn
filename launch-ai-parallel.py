#!/usr/bin/env python3
"""Run one to three isolated Linux headless MAX games from a JSON manifest."""

from __future__ import annotations

import argparse
import json
import os
import re
import shutil
import signal
import subprocess
import sys
import time
import uuid
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


MAX_TICK_PATTERNS = (
    re.compile(r"MAX progress: world=(\d+)"),
    re.compile(r"world tick (\d+)", re.IGNORECASE),
    re.compile(r"\btick=(\d+)\b"),
)
HEADLESS_MARKER = "Headless MAX automation enabled"
MAX_MARKER = "MAX game speed enabled"
STARTED_MARKER = "Headless MAX automation started map"
NATURAL_MARKER = "Headless MAX automation reached natural game over"
BOUNDED_MARKER = "Headless MAX automation reached configured exit"
FATAL_PATTERN = re.compile(
    r"unhandled exception|fatal (?:lua )?error|desync detected|exception of type", re.IGNORECASE
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


@dataclass(frozen=True)
class ReadinessSpec:
    actor_log_patterns: list[str]
    build_log_patterns: list[str]
    ready_log_pattern: str
    timeout_seconds: float
    maximum_world_tick: int | None


@dataclass
class RunSpec:
    name: str
    source_path: Path
    source_kind: str
    seed: int | None
    lobby_commands: str | None
    exit_at_tick: int | None
    minimum_world_tick: int
    timeout_seconds: float
    save_at_tick: int | None
    save_name: str
    required_log_patterns: list[str]
    forbidden_log_patterns: list[str]
    expected_artifacts: list[str]
    extra_args: list[str]
    support_maps: list[Path]
    readiness: ReadinessSpec | None


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
    readiness_state: str = "not-configured"
    readiness_reason: str | None = None
    readiness_observed_actor: list[str] = field(default_factory=list)
    readiness_observed_build: list[str] = field(default_factory=list)
    readiness_marker_count: int = 0
    readiness_tick: int | None = None
    readiness_seconds: float | None = None


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
        if source_kind == "map":
            if not isinstance(lobby_commands, str) or not re.search(
                r"(?:^|;)\s*option gamespeed max\s*(?:;|$)", lobby_commands, re.IGNORECASE
            ):
                raise ConfigurationError(f"Run '{name}' map lobby_commands must select gamespeed max.")
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

        support_maps = []
        for support_map in string_list("support_maps"):
            path = resolve_path(support_map, base)
            if not path.is_file():
                raise ConfigurationError(f"Run '{name}' support map does not exist: {path}")
            support_maps.append(path)

        readiness = None
        readiness_config = config.get("readiness")
        if readiness_config is not None:
            if not isinstance(readiness_config, dict):
                raise ConfigurationError(f"Run '{name}' readiness must be an object.")

            def readiness_patterns(key: str) -> list[str]:
                value = readiness_config.get(key)
                if not isinstance(value, list) or not 0 < len(value) <= 16 or not all(
                    isinstance(item, str) and item for item in value
                ):
                    raise ConfigurationError(
                        f"Run '{name}' readiness {key} must contain 1 to 16 non-empty patterns."
                    )
                return value

            actor_patterns = readiness_patterns("actor_log_patterns")
            build_patterns = readiness_patterns("build_log_patterns")
            ready_pattern = readiness_config.get("ready_log_pattern")
            if not isinstance(ready_pattern, str) or not ready_pattern:
                raise ConfigurationError(
                    f"Run '{name}' readiness ready_log_pattern must be a non-empty string."
                )
            readiness_timeout = readiness_config.get("timeout_seconds")
            if not isinstance(readiness_timeout, (int, float)) or not 0 < readiness_timeout < timeout_seconds:
                raise ConfigurationError(
                    f"Run '{name}' readiness timeout_seconds must be positive and below timeout_seconds."
                )
            readiness_tick = readiness_config.get("maximum_world_tick")
            if readiness_tick is not None and (
                not isinstance(readiness_tick, int) or readiness_tick < 1
            ):
                raise ConfigurationError(
                    f"Run '{name}' readiness maximum_world_tick must be a positive integer."
                )
            for pattern in actor_patterns + build_patterns + [ready_pattern]:
                try:
                    re.compile(pattern)
                except re.error as ex:
                    raise ConfigurationError(
                        f"Run '{name}' has an invalid readiness pattern '{pattern}': {ex}"
                    ) from ex
            if len(set(actor_patterns + build_patterns + [ready_pattern])) != (
                len(actor_patterns) + len(build_patterns) + 1
            ):
                raise ConfigurationError(f"Run '{name}' readiness patterns must be distinct.")
            readiness = ReadinessSpec(
                actor_patterns, build_patterns, ready_pattern,
                float(readiness_timeout), readiness_tick,
            )

        specs.append(RunSpec(
            name=name,
            source_path=source_path,
            source_kind=source_kind,
            seed=config.get("seed"),
            lobby_commands=lobby_commands,
            exit_at_tick=exit_at_tick,
            minimum_world_tick=minimum_world_tick,
            timeout_seconds=float(timeout_seconds),
            save_at_tick=save_at_tick,
            save_name=save_name,
            required_log_patterns=required_log_patterns,
            forbidden_log_patterns=forbidden_log_patterns,
            expected_artifacts=expected_artifacts,
            extra_args=extra_args,
            support_maps=support_maps,
            readiness=readiness,
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

    if spec.support_maps:
        if mod_version is None:
            raise ConfigurationError("A CNC mod version is required to stage support maps.")
        maps_dir = support_dir / "maps" / "cnc" / mod_version
        maps_dir.mkdir(parents=True)
        for support_map in spec.support_maps:
            shutil.copy2(support_map, maps_dir / support_map.name)

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


def update_readiness(run: ActiveRun, text: str, now: float) -> None:
    spec = run.spec.readiness
    if spec is None or run.readiness_state in ("ready", "failed"):
        return

    run.readiness_state = "pending"
    actor_matches = {pattern: re.search(pattern, text, re.MULTILINE) for pattern in spec.actor_log_patterns}
    build_matches = {pattern: re.search(pattern, text, re.MULTILINE) for pattern in spec.build_log_patterns}
    ready_matches = list(re.finditer(spec.ready_log_pattern, text, re.MULTILINE))
    run.readiness_observed_actor = [pattern for pattern, match in actor_matches.items() if match]
    run.readiness_observed_build = [pattern for pattern, match in build_matches.items() if match]
    run.readiness_marker_count = len(ready_matches)
    elapsed = now - run.started_monotonic

    if ready_matches:
        first_ready = ready_matches[0].start()
        evidence_matches = list(actor_matches.values()) + list(build_matches.values())
        if any(match is None or match.start() > first_ready for match in evidence_matches):
            run.readiness_state = "failed"
            run.readiness_reason = "ready marker observed before all authoritative evidence"
        elif len(ready_matches) != 1:
            run.readiness_state = "failed"
            run.readiness_reason = "ready marker observed more than once"
        else:
            run.readiness_state = "ready"
            run.readiness_tick = run.last_tick
            run.readiness_seconds = elapsed
            return
    elif FATAL_PATTERN.search(text):
        run.readiness_state = "failed"
        run.readiness_reason = "fatal/crash/desync signal before readiness"
    elif spec.maximum_world_tick is not None and run.last_tick > spec.maximum_world_tick:
        run.readiness_state = "failed"
        run.readiness_reason = f"setup exceeded world tick {spec.maximum_world_tick}"
    elif elapsed > spec.timeout_seconds:
        run.readiness_state = "failed"
        run.readiness_reason = f"setup exceeded {spec.timeout_seconds:g} seconds"

    if run.readiness_state == "failed":
        run.readiness_tick = run.last_tick
        run.readiness_seconds = elapsed


def readiness_summary(run: ActiveRun) -> dict[str, Any]:
    spec = run.spec.readiness
    if spec is None:
        return {"configured": False, "state": "not-configured"}

    return {
        "configured": True,
        "state": run.readiness_state,
        "reason": run.readiness_reason,
        "observed_actor_patterns": run.readiness_observed_actor,
        "missing_actor_patterns": [
            pattern for pattern in spec.actor_log_patterns
            if pattern not in run.readiness_observed_actor
        ],
        "observed_build_patterns": run.readiness_observed_build,
        "missing_build_patterns": [
            pattern for pattern in spec.build_log_patterns
            if pattern not in run.readiness_observed_build
        ],
        "ready_marker_count": run.readiness_marker_count,
        "maximum_world_tick": run.readiness_tick,
        "duration_seconds": round(run.readiness_seconds, 3)
        if run.readiness_seconds is not None else None,
    }


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
    benchmark_files = relative_files(run.support_dir, "Logs/*.csv")
    replay_files = relative_files(run.support_dir, "Replays/**/*")
    save_files = relative_files(run.support_dir, "Saves/**/*")
    crash_files = relative_files(run.support_dir, "Logs/*crash*.log")

    reasons = []
    exit_code = run.process.returncode
    if run.spec.readiness is not None and run.readiness_state != "ready":
        reason = run.readiness_reason or "process exited before readiness"
        reasons.append(f"setup failed: {reason}")
    if run.interrupted:
        reasons.append("batch interrupted")
    if run.timed_out:
        reasons.append("timeout")
    if exit_code != 0:
        reasons.append(f"exit code {exit_code}")
    if HEADLESS_MARKER not in text:
        reasons.append("headless activation marker missing")
    if MAX_MARKER not in text:
        reasons.append("MAX activation marker missing")
    if STARTED_MARKER not in text:
        reasons.append("actual map/bot start marker missing")
    exit_marker = BOUNDED_MARKER if run.spec.exit_at_tick is not None else NATURAL_MARKER
    if exit_marker not in text:
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

    result = {
        "name": run.spec.name,
        "status": "passed" if not reasons else "failed",
        "reasons": reasons,
        "exit_code": exit_code,
        "duration_seconds": round(duration, 3),
        "maximum_world_tick": tick,
        "valid_world_ticks": tick if not reasons else 0,
        "started_utc": run.started_utc,
        "finished_utc": datetime.now(timezone.utc).isoformat(),
        "display_start": run.display_start,
        "network_endpoint": "engine-assigned ephemeral loopback",
        "source_copy": str(run.runtime_input),
        "support_directory": str(run.support_dir),
        "logs": [str(path.relative_to(run.run_dir)) for path in log_paths if path.exists()],
        "benchmarks": benchmark_files,
        "replays": replay_files,
        "saves": save_files,
        "required_log_patterns": required,
        "forbidden_log_patterns": forbidden,
        "expected_artifacts": artifacts,
        "readiness": readiness_summary(run),
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
    valid_ticks = sum(result.get("valid_world_ticks", 0) for result in results)
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
                update_readiness(run, text, now)
                if run.process.poll() is None and run.readiness_state == "failed":
                    terminate_process(run)
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
