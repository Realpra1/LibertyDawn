#!/usr/bin/env python3
"""Run a command while holding one or more cross-worktree resource slots."""

from __future__ import annotations

import argparse
import ctypes
import fcntl
import json
import os
import pathlib
import signal
import subprocess
import sys
import time
from datetime import datetime, timezone


LARGE_BUILD_ENTRY_ROLES = ("worker", "reviewer", "integrator")
LARGE_BUILD_RESOURCE = "large-build"
LARGE_BUILD_CAPACITY = 1
GLOBAL_RESOURCE_CAPACITIES = {
    "game": 2,
    LARGE_BUILD_RESOURCE: LARGE_BUILD_CAPACITY,
    "policy-scratchpad": 1,
}
POLL_INTERVAL_SECONDS = 0.05
DESCENDANT_GRACE_SECONDS = 1.0
DESCENDANT_TERMINATE_SECONDS = 2.0
SUBREAPER_OPTION = 36  # Linux PR_SET_CHILD_SUBREAPER


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--lock-dir", required=True, type=pathlib.Path)
    policy = parser.add_mutually_exclusive_group(required=True)
    policy.add_argument("--resource")
    policy.add_argument("--large-build-entry", choices=LARGE_BUILD_ENTRY_ROLES)
    parser.add_argument("--capacity", type=int)
    parser.add_argument("--slots", type=int, default=1)
    parser.add_argument("--timeout", type=float, default=3600)
    parser.add_argument(
        "--status",
        action="store_true",
        help="probe registered resource slots without running a command",
    )
    parser.add_argument("command", nargs=argparse.REMAINDER)
    args = parser.parse_args()
    if args.command and args.command[0] == "--":
        args.command = args.command[1:]
    if args.large_build_entry:
        if args.capacity is not None or args.slots != 1:
            parser.error(
                "--large-build-entry owns capacity one and does not accept "
                "--capacity/--slots"
            )
        args.resource = LARGE_BUILD_RESOURCE
        args.capacity = LARGE_BUILD_CAPACITY
    elif args.capacity is None:
        parser.error("--capacity is required with --resource")
    elif args.resource == LARGE_BUILD_RESOURCE:
        parser.error(
            "large-build policy is protected; use --large-build-entry "
            "worker, reviewer, or integrator"
        )
    if args.capacity < 1 or args.slots < 1 or args.slots > args.capacity:
        parser.error("require 1 <= slots <= capacity")
    if not args.command and not args.status:
        parser.error("a command is required after --")
    if args.command and args.status:
        parser.error("--status does not accept a command")
    if not args.resource.replace("-", "").replace("_", "").isalnum():
        parser.error("resource must contain only letters, digits, '-' or '_'")
    return args


def repository_global_lock_dir() -> pathlib.Path:
    """Return the one lock namespace shared by this repository's worktrees."""
    checkout = pathlib.Path(__file__).resolve().parents[4]
    try:
        completed = subprocess.run(
            [
                "git",
                "-C",
                str(checkout),
                "rev-parse",
                "--path-format=absolute",
                "--git-common-dir",
            ],
            capture_output=True,
            text=True,
            timeout=10,
            check=True,
        )
    except (OSError, subprocess.SubprocessError) as error:
        raise RuntimeError(f"cannot resolve repository-global lock namespace: {error}") from error
    common_git_dir = pathlib.Path(completed.stdout.strip()).resolve()
    return common_git_dir.parent / ".agents" / "locks"


def enforce_resource_policy(args: argparse.Namespace) -> pathlib.Path:
    """Validate registered global resource ownership before any lock is opened."""
    requested = args.lock_dir.expanduser().resolve()
    registered_capacity = GLOBAL_RESOURCE_CAPACITIES.get(args.resource)
    if registered_capacity is None:
        if not args.lock_dir.is_absolute():
            raise RuntimeError("local resource --lock-dir must be absolute")
        return requested

    canonical = repository_global_lock_dir().resolve()
    if requested != canonical:
        raise RuntimeError(
            f"registered global resource {args.resource!r} requires canonical "
            f"--lock-dir {canonical}; rejected conflicting namespace {requested}"
        )
    if args.capacity != registered_capacity:
        raise RuntimeError(
            f"registered global resource {args.resource!r} owns capacity "
            f"{registered_capacity}; rejected caller capacity {args.capacity}"
        )
    return canonical


def emit(event: str, **fields: object) -> None:
    value = {"event": event, "monotonic": time.monotonic(), **fields}
    print(f"resource-slot {json.dumps(value, sort_keys=True)}", file=sys.stderr, flush=True)


def large_build_paths(lock_dir: pathlib.Path) -> tuple[pathlib.Path, pathlib.Path]:
    return lock_dir / "large-build-1.lock", lock_dir / "large-build.lock"


def reject_mixed_large_build_namespace(args: argparse.Namespace) -> None:
    if not args.large_build_entry:
        return
    canonical, legacy = large_build_paths(args.lock_dir)
    if legacy.exists():
        emit(
            "rejected",
            reason="mixed-large-build-namespace",
            canonical_path=str(canonical),
            legacy_path=str(legacy),
        )
        raise RuntimeError(
            "Mixed large-build lock namespace is unsafe: canonical path "
            f"{canonical}; legacy direct-flock path {legacy}. Stop legacy holders "
            "and remove the unlocked legacy file deliberately; no automatic unlink "
            "was attempted."
        )


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


def inspect_paths(resource: str, capacity: int, paths: list[pathlib.Path]) -> int:
    """Report kernel flock state; file contents are explicitly historical only."""
    for index, path in enumerate(paths, start=1):
        handle = path.open("a+", encoding="utf-8")
        handle.seek(0)
        metadata = handle.read()
        try:
            fcntl.flock(handle, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            availability = "contended"
        else:
            availability = "available"
            fcntl.flock(handle, fcntl.LOCK_UN)
        stat_result = os.fstat(handle.fileno())
        handle.close()
        emit(
            "status",
            resource=resource,
            capacity=capacity,
            slot=index,
            path=str(path),
            device=stat_result.st_dev,
            inode=stat_result.st_ino,
            availability=availability,
            metadata_classification="last-known",
            last_known_metadata=metadata.rstrip("\n"),
        )
    return 0


def enable_subreaper() -> None:
    if not sys.platform.startswith("linux"):
        raise RuntimeError("process-tree reservations require Linux subreaper support")
    libc = ctypes.CDLL(None, use_errno=True)
    if libc.prctl(SUBREAPER_OPTION, 1, 0, 0, 0) != 0:
        error_number = ctypes.get_errno()
        raise OSError(error_number, os.strerror(error_number))


def _children() -> list[int]:
    children_path = pathlib.Path(f"/proc/self/task/{os.getpid()}/children")
    try:
        body = children_path.read_text(encoding="ascii").strip()
    except OSError as error:
        raise RuntimeError(f"cannot inspect assigned child process tree: {error}") from error
    return [int(value) for value in body.split()] if body else []


def _signal_process_group(pid: int, signum: int) -> None:
    try:
        os.killpg(pid, signum)
    except ProcessLookupError:
        pass


def _reap_children(cancellation) -> dict:
    natural_deadline = time.monotonic() + DESCENDANT_GRACE_SECONDS
    termination_deadline = None
    seen: set[int] = set()
    terminated: set[int] = set()
    killed: set[int] = set()
    while True:
        while True:
            try:
                waited, _ = os.waitpid(-1, os.WNOHANG)
            except ChildProcessError:
                return {
                    "descendant_pids": sorted(seen),
                    "terminated_pids": sorted(terminated),
                    "killed_pids": sorted(killed),
                }
            if waited == 0:
                break
        children = _children()
        if not children:
            return {
                "descendant_pids": sorted(seen),
                "terminated_pids": sorted(terminated),
                "killed_pids": sorted(killed),
            }
        seen.update(children)
        cancel_signal = cancellation()
        now = time.monotonic()
        if cancel_signal is None and now < natural_deadline:
            time.sleep(POLL_INTERVAL_SECONDS)
            continue
        for pid in children:
            if pid not in terminated:
                try:
                    os.kill(pid, signal.SIGTERM)
                except ProcessLookupError:
                    continue
                terminated.add(pid)
        if termination_deadline is None:
            termination_deadline = now + DESCENDANT_TERMINATE_SECONDS
        if now >= termination_deadline:
            for pid in children:
                if pid in killed:
                    continue
                try:
                    os.kill(pid, signal.SIGKILL)
                except ProcessLookupError:
                    pass
                else:
                    killed.add(pid)
        time.sleep(POLL_INTERVAL_SECONDS)


def _shell_exit_code(return_code: int) -> int:
    return 128 + (-return_code) if return_code < 0 else return_code


def run_guardian(command: list[str], lock_fds: tuple[int, ...], entry_role: str) -> int:
    enable_subreaper()
    cancellation = {"signal": None}
    process: subprocess.Popen | None = None

    def forward(signum, _frame):
        cancellation["signal"] = signum
        if process is not None:
            _signal_process_group(process.pid, signum)

    for signum in (signal.SIGTERM, signal.SIGINT, signal.SIGHUP):
        signal.signal(signum, forward)

    process = subprocess.Popen(
        command,
        start_new_session=True,
        pass_fds=lock_fds,
    )
    emit(
        "child-start",
        entry_role=entry_role,
        child_pid=process.pid,
        process_group=process.pid,
    )
    return_code = process.wait()
    emit(
        "child-exit",
        entry_role=entry_role,
        child_pid=process.pid,
        command_returncode=return_code,
        cancellation_signal=cancellation["signal"],
    )
    cleanup = _reap_children(lambda: cancellation["signal"])
    emit(
        "tree-resolved",
        entry_role=entry_role,
        child_pid=process.pid,
        **cleanup,
    )
    if cancellation["signal"] is not None:
        return 128 + cancellation["signal"]
    return _shell_exit_code(return_code)


def run_supervised_tree(
    args: argparse.Namespace, held: list, owner: dict
) -> int:
    lock_fds = tuple(handle.fileno() for handle in held)
    for descriptor in lock_fds:
        os.set_inheritable(descriptor, True)

    guardian_pid = os.fork()
    if guardian_pid == 0:
        try:
            os.setsid()
            return_code = run_guardian(
                args.command, lock_fds, args.large_build_entry or args.resource
            )
        except BaseException as error:
            emit(
                "guardian-failed",
                entry_role=args.large_build_entry or args.resource,
                error=f"{type(error).__name__}: {error}",
            )
            return_code = 70
        finally:
            for handle in held:
                handle.close()
        os._exit(return_code)

    owner["guardian_pid"] = guardian_pid
    for handle in held:
        handle.seek(0)
        handle.truncate()
        json.dump(owner, handle)
        handle.write("\n")
        handle.flush()

    def forward(signum, _frame):
        emit(
            "cancellation-requested",
            entry_role=args.large_build_entry or args.resource,
            signal=signum,
            guardian_pid=guardian_pid,
        )
        _signal_process_group(guardian_pid, signum)

    previous_handlers = {}
    for signum in (signal.SIGTERM, signal.SIGINT, signal.SIGHUP):
        previous_handlers[signum] = signal.signal(signum, forward)
    try:
        while True:
            try:
                _, status = os.waitpid(guardian_pid, 0)
                break
            except InterruptedError:
                continue
        return _shell_exit_code(os.waitstatus_to_exitcode(status))
    finally:
        for signum, previous in previous_handlers.items():
            signal.signal(signum, previous)


def main() -> int:
    args = parse_args()
    try:
        args.lock_dir = enforce_resource_policy(args)
        args.lock_dir.mkdir(parents=True, exist_ok=True)
        reject_mixed_large_build_namespace(args)
    except (OSError, RuntimeError) as error:
        print(f"Resource reservation rejected: {error}", file=sys.stderr)
        return 78
    paths = [
        args.lock_dir / f"{args.resource}-{index}.lock"
        for index in range(1, args.capacity + 1)
    ]
    if args.status:
        try:
            return inspect_paths(args.resource, args.capacity, paths)
        except OSError as error:
            emit("rejected", resource=args.resource, reason=str(error))
            print(f"Resource status rejected: {error}", file=sys.stderr)
            return 73
    deadline = time.monotonic() + args.timeout
    held = []
    entry_role = args.large_build_entry or args.resource
    emit(
        "requested",
        resource=args.resource,
        capacity=args.capacity,
        slots=args.slots,
        entry_role=entry_role,
        lock_paths=[str(path) for path in paths],
    )
    queued = False
    while not held:
        try:
            held = try_acquire(paths, args.slots)
        except OSError as error:
            emit("rejected", entry_role=entry_role, reason=str(error))
            print(f"Resource reservation rejected: {error}", file=sys.stderr)
            return 73
        if held:
            break
        if not queued:
            emit("queued", resource=args.resource, entry_role=entry_role)
            queued = True
        if time.monotonic() >= deadline:
            emit("timed-out", resource=args.resource, entry_role=entry_role)
            print(
                f"Timed out waiting for {args.slots}/{args.capacity} "
                f"{args.resource} slots",
                file=sys.stderr,
            )
            return 75
        time.sleep(POLL_INTERVAL_SECONDS)

    try:
        reject_mixed_large_build_namespace(args)
    except RuntimeError as error:
        for handle in held:
            handle.close()
        print(f"Resource reservation rejected: {error}", file=sys.stderr)
        return 78

    held_paths = [str(pathlib.Path(handle.name)) for handle in held]

    owner = {
        "pid": os.getpid(),
        "resource": args.resource,
        "slots": args.slots,
        "command": args.command,
        "entry_role": entry_role,
        "lock_paths": held_paths,
        "acquired_utc": datetime.now(timezone.utc).isoformat(),
        "acquired_monotonic": time.monotonic(),
    }
    for handle in held:
        handle.seek(0)
        handle.truncate()
        json.dump(owner, handle)
        handle.write("\n")
        handle.flush()

    emit(
        "acquired",
        resource=args.resource,
        entry_role=entry_role,
        owner_pid=os.getpid(),
        lock_paths=held_paths,
    )

    try:
        return_code = run_supervised_tree(args, held, owner)
        emit(
            "command-outcome",
            resource=args.resource,
            entry_role=entry_role,
            returncode=return_code,
        )
        return return_code
    finally:
        for handle in held:
            handle.close()
        emit("released", resource=args.resource, entry_role=entry_role)


if __name__ == "__main__":
    sys.exit(main())
