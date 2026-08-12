#!/usr/bin/env python3
"""Launch a fresh, role-pinned Codex session in an isolated worktree."""

from __future__ import annotations

import argparse
import json
import os
import pathlib
import subprocess
import sys
from datetime import datetime, timezone


ROLES = {
    "task-reader": ("gpt-5.6-luna", "medium", "read-cnc-task/SKILL.md"),
    "task-maker": ("gpt-5.6-luna", "medium", "make-cnc-task/SKILL.md"),
    "task-intake-reviewer": ("gpt-5.6-terra", "medium", "review-cnc-policy/SKILL.md"),
    "speccer": ("gpt-5.6-sol", "high", "spec-cnc-task/SKILL.md"),
    "worker-sol": ("gpt-5.6-sol", "high", None),
    "worker-terra": ("gpt-5.6-terra", "medium", None),
    "worker-luna": ("gpt-5.6-luna", "medium", None),
    "integration-worker": ("gpt-5.6-luna", "medium", None),
    "integration-tester": ("gpt-5.6-luna", "medium", None),
    "commenter": ("gpt-5.6-luna", "medium", "comment-cnc-match/SKILL.md"),
    "policy-reviewer": ("gpt-5.6-luna", "medium", "review-cnc-policy/SKILL.md"),
    "policy-speccer": ("gpt-5.6-sol", "high", "review-cnc-policy/SKILL.md"),
    "policy-escalation": ("gpt-5.6-sol", "xhigh", "review-cnc-policy/SKILL.md"),
    "cycle-reviewer": ("gpt-5.6-luna", "medium", "review-cnc-pr/SKILL.md"),
    "reviewer": ("gpt-5.6-terra", "medium", "review-cnc-pr/SKILL.md"),
    "integrator": ("gpt-5.6-terra", "medium", "integrate-cnc-release/SKILL.md"),
}

POLICY_ROLES = {
    "task-intake-reviewer",
    "policy-reviewer",
    "policy-speccer",
    "policy-escalation",
}


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
    if scratchpad.is_symlink() or not scratchpad.is_file():
        raise SystemExit(
            "Policy Reviewer scratchpad must be the staged regular file "
            f"{scratchpad}"
        )
    try:
        scratchpad_text = scratchpad.read_text(encoding="utf-8")
    except UnicodeError as error:
        raise SystemExit(f"Policy Reviewer scratchpad must be UTF-8: {error}") from error
    if len(scratchpad_text) > 3000:
        raise SystemExit("Policy Reviewer scratchpad exceeds 3000 characters")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--role", required=True, choices=sorted(ROLES))
    parser.add_argument("--worktree", required=True, type=pathlib.Path)
    parser.add_argument("--job-file", required=True, type=pathlib.Path)
    parser.add_argument("--output-dir", required=True, type=pathlib.Path)
    parser.add_argument("--background", action="store_true")
    parser.add_argument("--print-command", action="store_true")
    parser.add_argument("--supervised", action="store_true", help=argparse.SUPPRESS)
    return parser.parse_args()


def build_command(args: argparse.Namespace) -> tuple[list[str], str]:
    model, effort, instruction_name = ROLES[args.role]
    worktree = args.worktree.resolve()
    job_file = args.job_file.resolve()
    output_dir = args.output_dir.resolve()
    last_message = output_dir / "last-message.md"
    analysis_role = args.role == "commenter" or args.role in POLICY_ROLES
    session_directory = output_dir if analysis_role else worktree
    sandbox = "workspace-write" if analysis_role else "danger-full-access"

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
            "task contract. Perform exactly the single development/test cycle or "
            "handoff action it currently authorizes, update durable state, then return."
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
    command, prompt = build_command(args)
    metadata = {
        "role": args.role,
        "model": ROLES[args.role][0],
        "reasoning_effort": ROLES[args.role][1],
        "worktree": str(args.worktree.resolve()),
        "job_file": str(args.job_file.resolve()),
        "prompt": prompt,
        "command": command,
        "started_utc": datetime.now(timezone.utc).isoformat(),
    }

    if args.print_command:
        print(json.dumps(metadata, indent=2))
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
