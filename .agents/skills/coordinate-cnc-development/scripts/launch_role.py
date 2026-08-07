#!/usr/bin/env python3
"""Launch a fresh, role-pinned Codex session in an isolated worktree."""

from __future__ import annotations

import argparse
import json
import os
import pathlib
import shutil
import subprocess
import sys
from datetime import datetime, timezone


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


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--role", required=True, choices=sorted(ROLES))
    parser.add_argument("--worktree", required=True, type=pathlib.Path)
    parser.add_argument("--job-file", required=True, type=pathlib.Path)
    parser.add_argument("--output-dir", required=True, type=pathlib.Path)
    parser.add_argument("--background", action="store_true")
    parser.add_argument("--print-command", action="store_true")
    parser.add_argument(
        "--validate-cli",
        action="store_true",
        help="exercise the installed Codex parser without starting an agent",
    )
    parser.add_argument("--supervised", action="store_true", help=argparse.SUPPRESS)
    return parser.parse_args()


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
    temporary = path.with_name(f".{path.name}.{os.getpid()}.tmp")
    temporary.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")
    temporary.replace(path)


def main() -> int:
    args = parse_args()
    if not args.worktree.is_dir():
        raise SystemExit(f"Worktree does not exist: {args.worktree}")
    if not args.job_file.is_file():
        raise SystemExit(f"Job file does not exist: {args.job_file}")

    validate_analysis_job(args)
    args.output_dir.mkdir(parents=True, exist_ok=True)
    policy = runtime_policy(args)
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
        "started_utc": datetime.now(timezone.utc).isoformat(),
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
        supervisor_command = [
            sys.executable,
            str(pathlib.Path(__file__).resolve()),
            "--role",
            args.role,
            "--worktree",
            str(args.worktree.resolve()),
            "--job-file",
            str(args.job_file.resolve()),
            "--output-dir",
            str(args.output_dir.resolve()),
            "--supervised",
        ]
        supervisor_log = (args.output_dir / "supervisor.log").open("wb")
        supervisor = subprocess.Popen(
            supervisor_command,
            stdout=supervisor_log,
            stderr=subprocess.STDOUT,
            start_new_session=True,
        )
        supervisor_log.close()
        write_json(
            args.output_dir / "supervisor.json",
            {
                "pid": supervisor.pid,
                "status": "launched",
                "role": args.role,
                "model": policy["model"],
                "reasoning_effort": policy["reasoning_effort"],
                "sandbox": policy["sandbox"],
                "session_directory": str(policy["session_directory"]),
                "output_dir": str(policy["output_dir"]),
                "command": supervisor_command,
                "started_utc": metadata["started_utc"],
            },
        )
        print(supervisor.pid)
        return 0

    event_log = args.output_dir / "events.jsonl"
    with event_log.open("wb") as output:
        process = subprocess.Popen(command, stdout=output, stderr=subprocess.STDOUT)
        metadata["pid"] = process.pid
        if args.supervised:
            metadata["supervisor_pid"] = os.getpid()
        metadata["status"] = "running"
        write_json(args.output_dir / "process.json", metadata)
        return_code = process.wait()

    metadata["exit_code"] = return_code
    metadata["completed_utc"] = datetime.now(timezone.utc).isoformat()
    metadata["status"] = "complete" if return_code == 0 else "failed"
    write_json(args.output_dir / "process.json", metadata)
    return return_code


if __name__ == "__main__":
    sys.exit(main())
