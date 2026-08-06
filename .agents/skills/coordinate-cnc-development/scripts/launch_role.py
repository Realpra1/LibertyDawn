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
    "task-reader": ("gpt-5.6-terra", "medium", "read-cnc-task/SKILL.md"),
    "task-maker": ("gpt-5.6-terra", "medium", "make-cnc-task/SKILL.md"),
    "speccer": ("gpt-5.6-sol", "xhigh", "spec-cnc-task/SKILL.md"),
    "worker": ("gpt-5.6-sol", "high", None),
    "reviewer": ("gpt-5.6-sol", "high", "review-cnc-pr/SKILL.md"),
    "integrator": ("gpt-5.6-sol", "high", "integrate-cnc-release/SKILL.md"),
}


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
        str(worktree),
        "-s",
        "danger-full-access",
        "-a",
        "never",
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
