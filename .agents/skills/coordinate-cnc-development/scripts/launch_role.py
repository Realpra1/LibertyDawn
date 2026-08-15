#!/usr/bin/env python3
"""Launch a fresh, role-pinned Codex session in an isolated worktree."""

from __future__ import annotations

import argparse
import json
import os
import pathlib
import shutil
import stat
import subprocess
import sys
import tempfile
import time
from datetime import datetime, timezone

from external_worker_runtime import (
    ProcessIdentityError,
    RECORD_SCHEMA,
    atomic_write_json,
    current_git_branch,
    initialize_assignment_record,
    new_assignment_id,
    new_attempt_id,
    read_process_identity,
    register_watchdog_assignment,
    update_assignment_record,
    utc_now,
)


ROLES = {
    "task-reader": ("gpt-5.6-terra", "medium", "read-cnc-task/SKILL.md"),
    "task-maker": ("gpt-5.6-terra", "medium", "make-cnc-task/SKILL.md"),
    "speccer": ("gpt-5.6-sol", "xhigh", "spec-cnc-task/SKILL.md"),
    "worker": ("gpt-5.6-sol", "high", None),
    "commenter": ("gpt-5.6-terra", "medium", "comment-cnc-match/SKILL.md"),
    "policy-reviewer": ("gpt-5.6-terra", "medium", "review-cnc-policy/SKILL.md"),
    "policy-speccer": ("gpt-5.6-sol", "high", "review-cnc-policy/SKILL.md"),
    "policy-escalation": ("gpt-5.6-sol", "xhigh", "review-cnc-policy/SKILL.md"),
    "cycle-reviewer": ("gpt-5.6-terra", "medium", "review-cnc-pr/SKILL.md"),
    "reviewer": ("gpt-5.6-sol", "high", "review-cnc-pr/SKILL.md"),
    "integrator": ("gpt-5.6-sol", "high", "integrate-cnc-release/SKILL.md"),
}

POLICY_ROLES = {"policy-reviewer", "policy-speccer", "policy-escalation"}
POLICY_OUTPUTS = ("POLICY-REVIEW.md", "POLICY-SCRATCHPAD.md")
POLICY_SCRATCHPAD_LIMIT = 3000
_DETACHED_SUPERVISORS: list[subprocess.Popen] = []
HOST_SUPERVISION_PROBE_TIMEOUT_SECONDS = 3.0
HOST_SUPERVISION_LAUNCH_TIMEOUT_SECONDS = 5.0


def host_supervision_capability() -> dict[str, object]:
    """Boundedly determine whether a usable systemd user manager is available."""
    checked_utc = utc_now()
    systemctl = shutil.which("systemctl")
    systemd_run = shutil.which("systemd-run")
    diagnostic: dict[str, object] = {
        "checked_utc": checked_utc,
        "probe_timeout_seconds": HOST_SUPERVISION_PROBE_TIMEOUT_SECONDS,
        "mechanism": "systemd-user-service",
        "systemctl": systemctl,
        "systemd_run": systemd_run,
    }
    if not systemctl or not systemd_run:
        diagnostic.update(
            {
                "supported": False,
                "fallback_reason": "systemd user-service tools are unavailable",
            }
        )
        return diagnostic
    try:
        probe = subprocess.run(
            [systemctl, "--user", "show-environment"],
            stdin=subprocess.DEVNULL,
            capture_output=True,
            text=True,
            timeout=HOST_SUPERVISION_PROBE_TIMEOUT_SECONDS,
            check=False,
        )
    except subprocess.TimeoutExpired:
        diagnostic.update(
            {
                "supported": False,
                "fallback_reason": (
                    "systemd user-manager capability probe exceeded "
                    f"{HOST_SUPERVISION_PROBE_TIMEOUT_SECONDS:.1f}s"
                ),
            }
        )
        return diagnostic
    except OSError as error:
        diagnostic.update(
            {
                "supported": False,
                "fallback_reason": f"systemd user-manager probe failed: {error}",
            }
        )
        return diagnostic
    diagnostic["probe_exit_code"] = probe.returncode
    if probe.returncode != 0:
        detail = (probe.stderr or probe.stdout).strip().replace("\n", " ")[:300]
        diagnostic.update(
            {
                "supported": False,
                "fallback_reason": (
                    "systemd user manager is unavailable"
                    + (f": {detail}" if detail else "")
                ),
            }
        )
        return diagnostic
    diagnostic.update({"supported": True, "fallback_reason": None})
    return diagnostic


def _service_unit_name(assignment_id: str, attempt_id: str, generation: int) -> str:
    return (
        f"libertydawn-worker-{assignment_id[:12]}-g{generation}-"
        f"{attempt_id[:8]}.service"
    )


def _stop_systemd_service(systemctl: str, service_unit: str) -> None:
    """Best-effort bounded cleanup after an incompletely published service launch."""
    try:
        subprocess.run(
            [systemctl, "--user", "stop", service_unit],
            stdin=subprocess.DEVNULL,
            capture_output=True,
            timeout=HOST_SUPERVISION_PROBE_TIMEOUT_SECONDS,
            check=False,
        )
    except (OSError, subprocess.TimeoutExpired):
        pass


def _resolved_path(value: object, field: str) -> pathlib.Path:
    if not isinstance(value, str) or not value:
        raise SystemExit(f"Analysis job field {field!r} must be a non-empty path")
    path = pathlib.Path(value)
    if not path.is_absolute():
        raise SystemExit(f"Analysis job field {field!r} must be an absolute path")
    return path.resolve()


def validate_analysis_job(args: argparse.Namespace) -> None:
    """Reject analysis-role jobs that are not strict path-only envelopes."""
    if args.role != "commenter" and args.role not in POLICY_ROLES:
        return

    try:
        job = json.loads(args.job_file.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise SystemExit(f"Analysis role job must be valid JSON: {error}") from error
    if not isinstance(job, dict):
        raise SystemExit("Analysis role job must be a JSON object")

    output_dir = args.output_dir.resolve()
    if args.job_file.resolve().parent != output_dir:
        raise SystemExit("Analysis role job file must be directly inside --output-dir")

    expected_output = "NARRATIVE.md" if args.role == "commenter" else "POLICY-REVIEW.md"
    output = _resolved_path(job.get("output"), "output")
    if output.parent != output_dir or output.name != expected_output:
        raise SystemExit(
            f"Analysis role output must be {output_dir / expected_output}"
        )

    if "design_reference" in job:
        design_path = _resolved_path(job["design_reference"], "design_reference")
        expected_design = (
            args.worktree.resolve()
            / ".agents"
            / "references"
            / "LIBERTY-DAWN-DESIGN.md"
        )
        if design_path != expected_design or not design_path.is_file():
            raise SystemExit(
                "design_reference must name the worktree Liberty Dawn design document"
            )

    if args.role == "commenter":
        allowed = {"artifacts", "design_reference", "output"}
        if set(job) - allowed or not {"artifacts", "output"} <= set(job):
            raise SystemExit(
                "Commenter job permits only artifacts, optional design_reference, and output"
            )
        artifacts = job["artifacts"]
        if not isinstance(artifacts, list) or not artifacts:
            raise SystemExit("Commenter job artifacts must be a non-empty path list")
        input_dir = output_dir / "inputs"
        for index, value in enumerate(artifacts):
            artifact = _resolved_path(value, f"artifacts[{index}]")
            if not artifact.is_file() or not artifact.is_relative_to(input_dir):
                raise SystemExit(
                    f"Commenter artifact must be a staged regular file under {input_dir}: "
                    f"{artifact}"
                )
        return

    if set(job) != {"design_reference", "task_context", "narrative", "output"}:
        raise SystemExit(
            "Policy Reviewer job must contain only design_reference, task_context, "
            "narrative, and output"
        )
    task_context = _resolved_path(job["task_context"], "task_context")
    expected_task_context = output_dir / "inputs" / "TASK-CONTEXT.md"
    if not task_context.is_file() or task_context != expected_task_context:
        raise SystemExit(
            "Policy Reviewer task context must be the staged input "
            f"{expected_task_context}"
        )
    narrative = _resolved_path(job["narrative"], "narrative")
    expected_narrative = output_dir / "inputs" / "NARRATIVE.md"
    if not narrative.is_file() or narrative != expected_narrative:
        raise SystemExit(
            "Policy Reviewer narrative must be the staged input "
            f"{expected_narrative}"
        )
    scratchpad = output_dir / "inputs" / "POLICY-SCRATCHPAD.md"
    try:
        _, scratchpad_text = _read_regular_utf8(
            scratchpad, "Policy Reviewer staged scratchpad"
        )
    except PolicyOutputError as error:
        raise SystemExit(str(error)) from error
    if len(scratchpad_text) > POLICY_SCRATCHPAD_LIMIT:
        raise SystemExit(
            f"Policy Reviewer staged scratchpad exceeds {POLICY_SCRATCHPAD_LIMIT} "
            "Unicode characters"
        )


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--role", required=True, choices=sorted(ROLES))
    parser.add_argument("--worktree", required=True, type=pathlib.Path)
    parser.add_argument("--job-file", required=True, type=pathlib.Path)
    parser.add_argument("--output-dir", required=True, type=pathlib.Path)
    parser.add_argument("--background", action="store_true")
    parser.add_argument(
        "--watchdog-registry",
        type=pathlib.Path,
        help="atomically register this background assignment for bounded recovery",
    )
    parser.add_argument("--print-command", action="store_true")
    parser.add_argument(
        "--validate-cli",
        action="store_true",
        help="exercise the installed Codex parser without starting an agent",
    )
    parser.add_argument("--supervised", action="store_true", help=argparse.SUPPRESS)
    return parser.parse_args()


class PolicyOutputError(RuntimeError):
    """A successful Policy Reviewer child did not satisfy its output contract."""


def _read_regular_utf8(path: pathlib.Path, label: str) -> tuple[bytes, str]:
    try:
        mode = path.lstat().st_mode
    except OSError as error:
        raise PolicyOutputError(f"{label} is missing or unreadable: {error}") from error
    if not stat.S_ISREG(mode):
        raise PolicyOutputError(f"{label} must be a non-symlink regular file: {path}")
    try:
        raw = path.read_bytes()
    except OSError as error:
        raise PolicyOutputError(f"{label} is unreadable: {error}") from error
    try:
        return raw, raw.decode("utf-8")
    except UnicodeDecodeError as error:
        raise PolicyOutputError(f"{label} must be valid UTF-8: {error}") from error


def prepare_policy_outputs(output_dir: pathlib.Path) -> list[str]:
    """Archive prior exact outputs so they cannot satisfy a new attempt."""
    archived = []
    attempt = f"{datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')}-{os.getpid()}"
    for name in POLICY_OUTPUTS:
        path = output_dir / name
        try:
            path.lstat()
        except FileNotFoundError:
            continue
        except OSError as error:
            raise PolicyOutputError(f"cannot inspect stale {name}: {error}") from error
        archive = output_dir / f".{name}.previous-{attempt}"
        try:
            path.replace(archive)
        except OSError as error:
            raise PolicyOutputError(f"cannot archive stale {name}: {error}") from error
        archived.append(str(archive))
    return archived


def validate_and_promote_policy_outputs(
    output_dir: pathlib.Path, canonical: pathlib.Path
) -> dict:
    """Validate both generated outputs, then atomically replace the canonical."""
    review_path = output_dir / "POLICY-REVIEW.md"
    scratchpad_path = output_dir / "POLICY-SCRATCHPAD.md"
    review_raw, _ = _read_regular_utf8(review_path, "Policy Reviewer review")
    scratchpad_raw, scratchpad_text = _read_regular_utf8(
        scratchpad_path, "Policy Reviewer scratchpad replacement"
    )
    if len(scratchpad_text) > POLICY_SCRATCHPAD_LIMIT:
        raise PolicyOutputError(
            "Policy Reviewer scratchpad replacement exceeds "
            f"{POLICY_SCRATCHPAD_LIMIT} Unicode characters"
        )

    try:
        canonical_parent = canonical.resolve(strict=False).parent
        canonical_mode = canonical.lstat().st_mode
    except OSError as error:
        raise PolicyOutputError(f"canonical policy scratchpad is unavailable: {error}") from error
    if not stat.S_ISREG(canonical_mode):
        raise PolicyOutputError(
            f"canonical policy scratchpad must be a non-symlink regular file: {canonical}"
        )

    temporary: pathlib.Path | None = None
    try:
        descriptor, temporary_name = tempfile.mkstemp(
            prefix=f".{canonical.name}.", suffix=".tmp", dir=canonical_parent
        )
        temporary = pathlib.Path(temporary_name)
        os.fchmod(descriptor, stat.S_IMODE(canonical_mode))
        with os.fdopen(descriptor, "wb") as stream:
            stream.write(scratchpad_raw)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, canonical)
        temporary = None
    except OSError as error:
        raise PolicyOutputError(
            f"failed to atomically promote Policy Reviewer scratchpad: {error}"
        ) from error
    finally:
        if temporary is not None:
            try:
                temporary.unlink()
            except FileNotFoundError:
                pass
            except OSError:
                pass

    return {
        "review_bytes": len(review_raw),
        "scratchpad_bytes": len(scratchpad_raw),
        "scratchpad_characters": len(scratchpad_text),
        "canonical_scratchpad": str(canonical),
    }


def build_command(args: argparse.Namespace) -> tuple[list[str], str]:
    policy = runtime_policy(args)
    model = policy["model"]
    effort = policy["reasoning_effort"]
    instruction_name = policy["instruction_name"]
    worktree = args.worktree.resolve()
    job_file = args.job_file.resolve()
    last_message = policy["last_message"]
    session_directory = policy["session_directory"]
    sandbox = policy["sandbox"]

    if instruction_name:
        instruction_file = worktree / ".agents" / "skills" / instruction_name
        if not instruction_file.is_file():
            raise SystemExit(f"Role instruction file does not exist: {instruction_file}")
        prompt = (
            f"Read and work the role instructions at {instruction_file}, then "
            f"read and execute the job file at {job_file}. "
            "Write all requested durable artifacts before returning a concise result."
        )
    else:
        prompt = (
            f"Read and work the file at {job_file}. It is your complete assigned "
            "task contract. Continue its implementation and evidence loop until its "
            "handoff condition is met."
        )

    command = [
        "codex",
        "exec",
        "--ephemeral",
        "--json",
        "-C",
        str(session_directory),
        "-s",
        sandbox,
        "-c",
        'approval_policy="never"',
        "-m",
        model,
        "-c",
        f'model_reasoning_effort="{effort}"',
        "-o",
        str(last_message),
        prompt,
    ]
    return command, prompt


def runtime_policy(args: argparse.Namespace) -> dict:
    model, effort, instruction_name = ROLES[args.role]
    worktree = args.worktree.resolve()
    output_dir = args.output_dir.resolve()
    analysis_role = args.role == "commenter" or args.role in POLICY_ROLES
    return {
        "model": model,
        "reasoning_effort": effort,
        "instruction_name": instruction_name,
        "analysis_role": analysis_role,
        "session_directory": output_dir if analysis_role else worktree,
        "sandbox": "workspace-write" if analysis_role else "danger-full-access",
        "output_dir": output_dir,
        "last_message": output_dir / "last-message.md",
    }


def _parser_command(command: list[str], executable: str) -> list[str]:
    """Replace only the free-form prompt with help after retaining every option."""
    return [executable, *command[1:-1], "--help"]


def _legacy_parser_command(command: list[str]) -> list[str]:
    legacy = list(command)
    for index in range(len(legacy) - 1):
        if legacy[index : index + 2] == ["-c", 'approval_policy="never"']:
            legacy[index : index + 2] = ["-a", "never"]
            return legacy
    raise ValueError("constructed command has no pinned approval policy")


def _agent_event_count(output: str) -> int:
    count = 0
    for line in output.splitlines():
        try:
            value = json.loads(line)
        except json.JSONDecodeError:
            continue
        if isinstance(value, dict) and value.get("type") not in {None, "error"}:
            count += 1
    return count


def validate_cli_parser(
    command: list[str], codex_executable: str | None = None
) -> dict:
    """Exercise the real CLI parser in help mode, plus a known-invalid control."""
    executable = codex_executable or shutil.which(command[0])
    if not executable:
        raise RuntimeError(
            f"Codex executable {command[0]!r} was not found; cannot validate CLI parser"
        )
    try:
        version = subprocess.run(
            [executable, "--version"],
            capture_output=True,
            text=True,
            timeout=10,
            check=False,
        )
        production_argv = _parser_command(command, executable)
        production = subprocess.run(
            production_argv,
            capture_output=True,
            text=True,
            timeout=10,
            check=False,
        )
        legacy_argv = _legacy_parser_command(production_argv)
        legacy = subprocess.run(
            legacy_argv,
            capture_output=True,
            text=True,
            timeout=10,
            check=False,
        )
    except (OSError, subprocess.TimeoutExpired) as error:
        raise RuntimeError(f"Codex CLI parser validation could not run: {error}") from error

    return {
        "executable": str(pathlib.Path(executable).resolve()),
        "version": (version.stdout or version.stderr).strip(),
        "version_exit": version.returncode,
        "sanitized_argv": [*production_argv[:-1], "<NO-AGENT-HELP>"],
        "production_exit": production.returncode,
        "production_stderr": production.stderr.strip(),
        "legacy_negative_exit": legacy.returncode,
        "legacy_stderr": legacy.stderr.strip(),
        "agent_events": _agent_event_count(production.stdout),
        "valid": (
            version.returncode == 0
            and production.returncode == 0
            and legacy.returncode != 0
            and _agent_event_count(production.stdout) == 0
        ),
    }


def write_json(path: pathlib.Path, value: dict) -> None:
    atomic_write_json(path, value)


def _start_supervisor(
    args: argparse.Namespace,
    *,
    assignment_root: pathlib.Path,
    assignment_id: str,
    attempt_id: str,
    generation: int,
    policy: dict,
    started_utc: str,
) -> dict:
    supervisor_command = [
        sys.executable,
        str(pathlib.Path(__file__).resolve()),
        "--role", args.role,
        "--worktree", str(args.worktree.resolve()),
        "--job-file", str(args.job_file.resolve()),
        "--output-dir", str(args.output_dir.resolve()),
        "--supervised",
    ]
    supervisor_log_path = (args.output_dir / "supervisor.log").resolve()
    supervisor_log = supervisor_log_path.open("xb")
    supervisor_log.close()
    environment = dict(os.environ)
    environment.update(
        {
            "LIBERTY_DAWN_ASSIGNMENT_ID": assignment_id,
            "LIBERTY_DAWN_ATTEMPT_ID": attempt_id,
            "LIBERTY_DAWN_ATTEMPT_GENERATION": str(generation),
            "LIBERTY_DAWN_ASSIGNMENT_ROOT": str(assignment_root.resolve()),
            "LIBERTY_DAWN_SUPERVISOR_GATE": str(
                (args.output_dir / ".supervisor-start-gate").resolve()
            ),
        }
    )
    capability = host_supervision_capability()
    supervision: dict[str, object] = {
        "capability": capability,
        "launch_timeout_seconds": HOST_SUPERVISION_LAUNCH_TIMEOUT_SECONDS,
        "startup_gate_timeout_seconds": HOST_SUPERVISION_LAUNCH_TIMEOUT_SECONDS,
        "stdin": "/dev/null",
        "stdout": str(supervisor_log_path),
        "stderr": str(supervisor_log_path),
    }
    supervisor: subprocess.Popen | None = None
    service_unit: str | None = None
    supervisor_pid: int | None = None
    if capability.get("supported"):
        systemd_run = str(capability["systemd_run"])
        systemctl = str(capability["systemctl"])
        service_unit = _service_unit_name(assignment_id, attempt_id, generation)
        service_command = [
            systemd_run,
            "--user",
            "--quiet",
            "--collect",
            "--service-type=exec",
            f"--unit={service_unit}",
            "--property=StandardInput=null",
            f"--property=StandardOutput=append:{supervisor_log_path}",
            f"--property=StandardError=append:{supervisor_log_path}",
            f"--setenv=PATH={environment.get('PATH', '')}",
        ]
        for name in (
            "LIBERTY_DAWN_ASSIGNMENT_ID",
            "LIBERTY_DAWN_ATTEMPT_ID",
            "LIBERTY_DAWN_ATTEMPT_GENERATION",
            "LIBERTY_DAWN_ASSIGNMENT_ROOT",
            "LIBERTY_DAWN_SUPERVISOR_GATE",
        ):
            service_command.append(f"--setenv={name}={environment[name]}")
        service_command.extend(["--", *supervisor_command])
        try:
            launched = subprocess.run(
                service_command,
                stdin=subprocess.DEVNULL,
                capture_output=True,
                text=True,
                timeout=HOST_SUPERVISION_LAUNCH_TIMEOUT_SECONDS,
                check=False,
            )
        except subprocess.TimeoutExpired as error:
            _stop_systemd_service(systemctl, service_unit)
            raise RuntimeError(
                "systemd supervisor launch timed out; refusing an ambiguous fallback"
            ) from error
        except OSError as error:
            launched = subprocess.CompletedProcess(service_command, 127, "", str(error))
        if launched.returncode == 0:
            try:
                shown = subprocess.run(
                    [systemctl, "--user", "show", "--property=MainPID", "--value", service_unit],
                    stdin=subprocess.DEVNULL,
                    capture_output=True,
                    text=True,
                    timeout=HOST_SUPERVISION_LAUNCH_TIMEOUT_SECONDS,
                    check=False,
                )
            except (OSError, subprocess.TimeoutExpired) as error:
                _stop_systemd_service(systemctl, service_unit)
                raise RuntimeError(
                    f"systemd service identity query failed within its bound: {error}"
                ) from error
            try:
                supervisor_pid = int(shown.stdout.strip()) if shown.returncode == 0 else 0
            except ValueError:
                supervisor_pid = 0
            if supervisor_pid <= 0:
                _stop_systemd_service(systemctl, service_unit)
                raise RuntimeError(
                    "systemd launched a service but did not publish a stable MainPID"
                )
            supervision.update(
                {
                    "mechanism": "systemd-user-service",
                    "strong_host_supervision": True,
                    "service_unit": service_unit,
                    "fallback_reason": None,
                }
            )
        else:
            detail = (launched.stderr or launched.stdout).strip().replace("\n", " ")[:300]
            supervision["fallback_reason"] = (
                "systemd transient service launch rejected"
                + (f": {detail}" if detail else "")
            )
    if supervisor_pid is None:
        with supervisor_log_path.open("ab") as supervisor_log:
            supervisor = subprocess.Popen(
                supervisor_command,
                stdin=subprocess.DEVNULL,
                stdout=supervisor_log,
                stderr=subprocess.STDOUT,
                start_new_session=True,
                close_fds=True,
                env=environment,
            )
        supervisor_pid = supervisor.pid
        supervision.update(
            {
                "mechanism": "detached-session-watchdog-fallback",
                "strong_host_supervision": False,
                "service_unit": None,
                "fallback_reason": supervision.get("fallback_reason")
                or capability.get("fallback_reason"),
            }
        )
    try:
        identity = read_process_identity(supervisor_pid)
    except Exception:
        if supervisor is not None:
            supervisor.terminate()
            try:
                supervisor.wait(timeout=2)
            except subprocess.TimeoutExpired:
                supervisor.kill()
                supervisor.wait(timeout=2)
        elif service_unit is not None:
            _stop_systemd_service(str(capability["systemctl"]), service_unit)
        raise
    if supervisor is not None:
        _DETACHED_SUPERVISORS[:] = [item for item in _DETACHED_SUPERVISORS if item.poll() is None]
        _DETACHED_SUPERVISORS.append(supervisor)
    record = {
        "schema": RECORD_SCHEMA,
        "assignment_id": assignment_id,
        "attempt_id": attempt_id,
        "generation": generation,
        "pid": supervisor_pid,
        "identity": identity,
        "status": "launched",
        "role": args.role,
        "model": policy["model"],
        "reasoning_effort": policy["reasoning_effort"],
        "sandbox": policy["sandbox"],
        "session_directory": str(policy["session_directory"]),
        "output_dir": str(policy["output_dir"]),
        "command": supervisor_command,
        "supervision": supervision,
        "started_utc": started_utc,
    }
    try:
        write_json(args.output_dir / "supervisor.json", record)
        pathlib.Path(environment["LIBERTY_DAWN_SUPERVISOR_GATE"]).open("xb").close()
    except Exception:
        if supervisor is not None:
            supervisor.terminate()
            try:
                supervisor.wait(timeout=2)
            except subprocess.TimeoutExpired:
                supervisor.kill()
                supervisor.wait(timeout=2)
        elif service_unit is not None:
            _stop_systemd_service(str(capability["systemctl"]), service_unit)
        raise
    return record


def relaunch_assignment(
    assignment_root: pathlib.Path,
    assignment: dict[str, object],
    interruption: dict[str, object],
) -> dict[str, object]:
    """Relaunch one interrupted worker through current protected launcher policy."""
    role = assignment.get("role")
    if role != "worker":
        raise ProcessIdentityError(f"automatic recovery is not authorized for role {role!r}")
    try:
        worktree_path = pathlib.Path(str(assignment["worktree"]))
        job_path = pathlib.Path(str(assignment["job_file"]))
        if job_path.is_symlink():
            raise ProcessIdentityError("relaunch job must not be a symlink")
        worktree = worktree_path.resolve(strict=True)
        job_file = job_path.resolve(strict=True)
    except (KeyError, OSError) as error:
        raise ProcessIdentityError(f"relaunch envelope is unavailable: {error}") from error
    if not worktree.is_dir() or not job_file.is_file() or not job_file.is_relative_to(worktree):
        raise ProcessIdentityError("relaunch job/worktree escapes the authorized envelope")
    branch = assignment.get("branch")
    if not isinstance(branch, str) or current_git_branch(worktree) != branch:
        raise ProcessIdentityError("relaunch worktree branch does not match the authorized assignment")
    assignment_id = assignment.get("assignment_id")
    if assignment_id != new_assignment_id(role, worktree, job_file):
        raise ProcessIdentityError("relaunch assignment identity does not match role/worktree/job")
    generation = assignment.get("next_generation")
    if not isinstance(generation, int) or generation != assignment.get("generation", 0) + 1:
        raise ProcessIdentityError("relaunch generation reservation is invalid")
    predecessor = assignment.get("current_attempt_id")
    predecessor_lineage = {
        "attempt_id": predecessor,
        "generation": assignment.get("generation"),
        "attempt_dir": assignment.get("current_attempt_dir"),
        "status": assignment.get("status"),
    }
    for key in ("blocked_reason", "blocked_record", "blocked_diagnostics"):
        if key in assignment:
            predecessor_lineage[key] = assignment[key]
    attempt_id = new_attempt_id()
    attempt_dir = assignment_root / "attempts" / f"generation-{generation:06d}-{attempt_id}"
    attempt_dir.mkdir(parents=True, exist_ok=False)
    args = argparse.Namespace(
        role=role,
        worktree=worktree,
        job_file=job_file,
        output_dir=attempt_dir,
        supervised=False,
    )
    policy = runtime_policy(args)
    build_command(args)  # Recompute and validate protected model/sandbox/session policy.
    next_assignment = dict(assignment)
    for key in ("blocked_reason", "blocked_record", "blocked_diagnostics"):
        next_assignment.pop(key, None)
    next_assignment.update(
        {
            "current_attempt_id": attempt_id,
            "generation": generation,
            "next_generation": generation + 1,
            "status": "recovering",
            "current_attempt_dir": str(attempt_dir.resolve()),
            "predecessor_attempt_id": predecessor,
            "predecessor_lineage": predecessor_lineage,
            "registrations": {"attempt_id": attempt_id, "descendants": [], "resources": []},
            "updated_utc": utc_now(),
        }
    )
    assignment_path = assignment_root / "assignment.json"
    try:
        atomic_write_json(assignment_path, next_assignment)
    except Exception:
        # No process can own an attempt whose current-view reservation failed.
        # The directory is new and still empty at this boundary.
        attempt_dir.rmdir()
        raise
    try:
        record = _start_supervisor(
            args,
            assignment_root=assignment_root,
            assignment_id=str(assignment_id),
            attempt_id=attempt_id,
            generation=generation,
            policy=policy,
            started_utc=utc_now(),
        )
    except Exception as error:
        next_assignment.update({"status": "blocked", "blocked_reason": f"relaunch failed: {error}", "updated_utc": utc_now()})
        atomic_write_json(assignment_path, next_assignment)
        raise
    if interruption.get("start_authorization"):
        # Explicit-start staging intentionally retains durable stop intent until
        # the replacement supervisor is published. The assignment lease remains
        # held by the caller, so the child cannot publish running first.
        next_assignment.update({"stop_intent": None, "updated_utc": utc_now()})
        atomic_write_json(assignment_path, next_assignment)
    return {
        "event": "relaunch-started",
        "assignment_id": assignment_id,
        "attempt_id": attempt_id,
        "generation": generation,
        "predecessor_attempt_id": predecessor,
        "attempt_dir": str(attempt_dir),
        "supervisor": record["identity"],
        "interruption_record": interruption.get("interruption_record"),
    }


def main() -> int:
    args = parse_args()
    if not args.worktree.is_dir():
        raise SystemExit(f"Worktree does not exist: {args.worktree}")
    if not args.job_file.is_file():
        raise SystemExit(f"Job file does not exist: {args.job_file}")

    validate_analysis_job(args)
    args.output_dir.mkdir(parents=True, exist_ok=True)
    policy = runtime_policy(args)
    assignment_id = os.environ.get("LIBERTY_DAWN_ASSIGNMENT_ID")
    attempt_id = os.environ.get("LIBERTY_DAWN_ATTEMPT_ID")
    generation_text = os.environ.get("LIBERTY_DAWN_ATTEMPT_GENERATION")
    if args.supervised:
        if not assignment_id or not attempt_id or generation_text is None:
            raise SystemExit("Supervised launch is missing protected attempt lineage")
        try:
            generation = int(generation_text)
        except ValueError as error:
            raise SystemExit("Supervised attempt generation must be an integer") from error
        if generation < 1:
            raise SystemExit("Supervised attempt generation must be positive")
        assignment_root_text = os.environ.get("LIBERTY_DAWN_ASSIGNMENT_ROOT")
        if not assignment_root_text:
            raise SystemExit("Supervised launch is missing protected assignment root")
        assignment_root = pathlib.Path(assignment_root_text).resolve(strict=True)
        gate_text = os.environ.get("LIBERTY_DAWN_SUPERVISOR_GATE")
        if not gate_text:
            raise SystemExit("Supervised launch is missing its protected startup gate")
        gate = pathlib.Path(gate_text)
        if gate.resolve(strict=False).parent != args.output_dir.resolve():
            raise SystemExit("Supervised startup gate escapes the attempt output directory")
        gate_deadline = time.monotonic() + HOST_SUPERVISION_LAUNCH_TIMEOUT_SECONDS
        while not gate.is_file():
            if time.monotonic() >= gate_deadline:
                raise SystemExit("Supervised startup gate was not published within its bound")
            time.sleep(0.01)
        try:
            gate.unlink()
        except OSError as error:
            raise SystemExit(f"Supervised startup gate could not be consumed: {error}") from error
    else:
        assignment_id = new_assignment_id(args.role, args.worktree, args.job_file)
        attempt_id = new_attempt_id()
        generation = 1
        assignment_root = args.output_dir.resolve()
    canonical_scratchpad = (
        args.worktree.resolve()
        / ".agents"
        / "references"
        / "LIBERTY-DAWN-POLICY-SCRATCHPAD.md"
    )
    command, prompt = build_command(args)
    metadata = {
        "role": args.role,
        "model": policy["model"],
        "reasoning_effort": policy["reasoning_effort"],
        "sandbox": policy["sandbox"],
        "session_directory": str(policy["session_directory"]),
        "worktree": str(args.worktree.resolve()),
        "job_file": str(args.job_file.resolve()),
        "output_dir": str(policy["output_dir"]),
        "last_message": str(policy["last_message"]),
        "supervised": args.supervised,
        "prompt": prompt,
        "command": command,
        "schema": RECORD_SCHEMA,
        "assignment_id": assignment_id,
        "attempt_id": attempt_id,
        "generation": generation,
        "started_utc": utc_now(),
    }

    if args.print_command:
        print(json.dumps(metadata, indent=2))
        return 0

    if args.validate_cli:
        try:
            validation = validate_cli_parser(command)
        except RuntimeError as error:
            print(f"Codex CLI validation rejected: {error}", file=sys.stderr)
            return 69
        metadata["cli_validation"] = validation
        print(json.dumps(metadata, indent=2))
        if not validation["valid"]:
            print(
                "Codex CLI validation rejected the protected invocation; inspect "
                "production_stderr and legacy_stderr above",
                file=sys.stderr,
            )
            return 2
        return 0

    if args.background:
        if args.role in POLICY_ROLES:
            print(
                "Policy Reviewer roles require foreground launch so the caller's "
                "policy-scratchpad lock covers validation and promotion",
                file=sys.stderr,
            )
            return 64
        try:
            initialize_assignment_record(
                args.output_dir,
                assignment_id=assignment_id,
                attempt_id=attempt_id,
                generation=generation,
                role=args.role,
                worktree=args.worktree,
                job_file=args.job_file,
                branch=current_git_branch(args.worktree),
            )
            if args.watchdog_registry is not None:
                register_watchdog_assignment(args.watchdog_registry, args.output_dir)
        except (OSError, ProcessIdentityError, TimeoutError, ValueError) as error:
            print(f"External assignment launch rejected: {error}", file=sys.stderr)
            return 75
        try:
            record = _start_supervisor(
                args,
                assignment_root=assignment_root,
                assignment_id=assignment_id,
                attempt_id=attempt_id,
                generation=generation,
                policy=policy,
                started_utc=metadata["started_utc"],
            )
        except Exception as error:
            reason = f"supervisor launch blocked: {error}"
            update_assignment_record(
                assignment_root,
                assignment_id=assignment_id,
                attempt_id=attempt_id,
                generation=generation,
                status="blocked",
            )
            write_json(
                args.output_dir / "launch-blocked.json",
                {
                    "schema": RECORD_SCHEMA,
                    "assignment_id": assignment_id,
                    "attempt_id": attempt_id,
                    "generation": generation,
                    "status": "blocked",
                    "reason": reason,
                    "observed_utc": utc_now(),
                },
            )
            print(reason, file=sys.stderr)
            return 75
        print(record["pid"])
        return 0

    if args.role in POLICY_ROLES:
        try:
            metadata["archived_policy_outputs"] = prepare_policy_outputs(
                args.output_dir
            )
        except PolicyOutputError as error:
            metadata["status"] = "failed"
            metadata["contract_error"] = str(error)
            metadata["completed_utc"] = datetime.now(timezone.utc).isoformat()
            write_json(args.output_dir / "process.json", metadata)
            print(f"Policy Reviewer output preparation failed: {error}", file=sys.stderr)
            return 65

    event_log = args.output_dir / "events.jsonl"
    with event_log.open("wb") as output:
        process = subprocess.Popen(
            command,
            stdin=subprocess.DEVNULL if args.supervised else None,
            stdout=output,
            stderr=subprocess.STDOUT,
            close_fds=True,
        )
        metadata["pid"] = process.pid
        metadata["identity"] = read_process_identity(process.pid)
        if args.supervised:
            metadata["supervisor_pid"] = os.getpid()
            metadata["supervisor_identity"] = read_process_identity(os.getpid())
        metadata["status"] = "running"
        write_json(args.output_dir / "process.json", metadata)
        if args.supervised:
            update_assignment_record(
                assignment_root,
                assignment_id=assignment_id,
                attempt_id=attempt_id,
                generation=generation,
                status="running",
            )
        return_code = process.wait()

    metadata["child_exit_code"] = return_code
    metadata["completed_utc"] = utc_now()
    if return_code == 0 and args.role in POLICY_ROLES:
        try:
            metadata["policy_output_contract"] = validate_and_promote_policy_outputs(
                args.output_dir, canonical_scratchpad
            )
        except PolicyOutputError as error:
            metadata["exit_code"] = 65
            metadata["status"] = "failed"
            metadata["contract_error"] = str(error)
            write_json(args.output_dir / "process.json", metadata)
            print(f"Policy Reviewer output contract failed: {error}", file=sys.stderr)
            return 65
    metadata["exit_code"] = return_code
    metadata["status"] = "complete" if return_code == 0 else "failed"
    write_json(args.output_dir / "process.json", metadata)
    if args.supervised:
        update_assignment_record(
            assignment_root,
            assignment_id=assignment_id,
            attempt_id=attempt_id,
            generation=generation,
            status=metadata["status"],
        )
    return return_code


if __name__ == "__main__":
    sys.exit(main())
