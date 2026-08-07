#!/usr/bin/env python3
"""Run a command while holding one or more cross-worktree resource slots."""

from __future__ import annotations

import argparse
import fcntl
import json
import os
import pathlib
import subprocess
import sys
import time
from datetime import datetime, timezone


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--lock-dir", required=True, type=pathlib.Path)
    parser.add_argument("--resource", required=True)
    parser.add_argument("--capacity", required=True, type=int)
    parser.add_argument("--slots", type=int, default=1)
    parser.add_argument("--timeout", type=float, default=3600)
    parser.add_argument("command", nargs=argparse.REMAINDER)
    args = parser.parse_args()
    if args.command and args.command[0] == "--":
        args.command = args.command[1:]
    if args.capacity < 1 or args.slots < 1 or args.slots > args.capacity:
        parser.error("require 1 <= slots <= capacity")
    if not args.command:
        parser.error("a command is required after --")
    if not args.resource.replace("-", "").replace("_", "").isalnum():
        parser.error("resource must contain only letters, digits, '-' or '_'")
    return args


def try_acquire(paths: list[pathlib.Path], count: int):
    held = []
    for path in paths:
        handle = path.open("a+", encoding="utf-8")
        try:
            fcntl.flock(handle, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            handle.close()
            continue
        held.append(handle)
        if len(held) == count:
            return held

    for handle in held:
        fcntl.flock(handle, fcntl.LOCK_UN)
        handle.close()
    return []


def main() -> int:
    args = parse_args()
    args.lock_dir.mkdir(parents=True, exist_ok=True)
    paths = [
        args.lock_dir / f"{args.resource}-{index}.lock"
        for index in range(1, args.capacity + 1)
    ]
    deadline = time.monotonic() + args.timeout
    held = []
    while not held:
        held = try_acquire(paths, args.slots)
        if held:
            break
        if time.monotonic() >= deadline:
            print(
                f"Timed out waiting for {args.slots}/{args.capacity} "
                f"{args.resource} slots",
                file=sys.stderr,
            )
            return 75
        time.sleep(1)

    owner = {
        "pid": os.getpid(),
        "resource": args.resource,
        "slots": args.slots,
        "command": args.command,
        "acquired_utc": datetime.now(timezone.utc).isoformat(),
    }
    for handle in held:
        handle.seek(0)
        handle.truncate()
        json.dump(owner, handle)
        handle.write("\n")
        handle.flush()

    try:
        return subprocess.run(args.command).returncode
    finally:
        for handle in held:
            fcntl.flock(handle, fcntl.LOCK_UN)
            handle.close()


if __name__ == "__main__":
    sys.exit(main())
