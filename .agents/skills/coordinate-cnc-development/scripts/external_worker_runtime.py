#!/usr/bin/env python3
"""Durable Linux identity primitives for coordinated external workers."""

from __future__ import annotations

import fcntl
import json
import os
import pathlib
import signal
import subprocess
import sys
import tempfile
import time
import uuid
from contextlib import contextmanager
from datetime import datetime, timezone


IDENTITY_SCHEMA = "libertydawn.process-identity/v1"
RECORD_SCHEMA = "libertydawn.external-worker-attempt/v1"
ASSIGNMENT_SCHEMA = "libertydawn.external-worker-assignment/v1"
INTERRUPTION_SCHEMA = "libertydawn.external-worker-interruption/v1"
QUARANTINE_SCHEMA = "libertydawn.external-worker-quarantine/v1"
STOP_SCHEMA = "libertydawn.external-worker-stop/v1"
START_SCHEMA = "libertydawn.external-worker-start/v1"
BLOCKED_SCHEMA = "libertydawn.external-worker-blocked/v1"
WATCHDOG_REGISTRY_SCHEMA = "libertydawn.external-worker-watchdog-registry/v1"
WATCHDOG_STATE_SCHEMA = "libertydawn.external-worker-watchdog-state/v1"
BOOT_ID_PATH = pathlib.Path("/proc/sys/kernel/random/boot_id")
RESOURCE_SCRIPT = pathlib.Path(__file__).with_name("with_resource_slots.py")
MAX_REGISTRATION_ANCESTRY_DEPTH = 64


class ProcessIdentityError(RuntimeError):
    """A stable Linux process identity could not be observed conclusively."""


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def age_seconds(timestamp: object, *, now: datetime | None = None) -> float:
    """Return the non-negative age of one UTC record timestamp."""
    if not isinstance(timestamp, str):
        raise ProcessIdentityError("record timestamp is missing")
    try:
        parsed = datetime.fromisoformat(timestamp)
    except ValueError as error:
        raise ProcessIdentityError(f"record timestamp is invalid: {error}") from error
    if parsed.tzinfo is None:
        raise ProcessIdentityError("record timestamp has no timezone")
    current = now or datetime.now(timezone.utc)
    return max(0.0, (current - parsed.astimezone(timezone.utc)).total_seconds())


def new_assignment_id(role: str, worktree: pathlib.Path, job_file: pathlib.Path) -> str:
    """Return a durable ID for one exact role/worktree/job assignment."""
    envelope = "\0".join((role, str(worktree.resolve()), str(job_file.resolve())))
    return str(uuid.uuid5(uuid.NAMESPACE_URL, f"libertydawn:external-worker:{envelope}"))


def new_attempt_id() -> str:
    return str(uuid.uuid4())


def current_git_branch(worktree: pathlib.Path) -> str | None:
    try:
        completed = subprocess.run(
            ["git", "-C", str(worktree.resolve()), "symbolic-ref", "--quiet", "--short", "HEAD"],
            capture_output=True,
            text=True,
            timeout=5,
        )
    except (OSError, subprocess.TimeoutExpired):
        return None
    branch = completed.stdout.strip()
    return branch if completed.returncode == 0 and branch else None


def _read_boot_id(path: pathlib.Path = BOOT_ID_PATH) -> str:
    try:
        boot_id = path.read_text(encoding="ascii").strip()
        return str(uuid.UUID(boot_id))
    except (OSError, ValueError) as error:
        raise ProcessIdentityError(f"cannot read a valid Linux boot ID: {error}") from error


def _parse_proc_stat(body: str) -> dict[str, object]:
    """Parse identity fields without assuming that comm contains no parentheses."""
    close = body.rfind(")")
    if close < 2 or close + 2 >= len(body):
        raise ProcessIdentityError("malformed /proc stat record")
    try:
        pid = int(body[: body.index(" ")])
        fields = body[close + 2 :].split()
        # fields starts at proc stat field 3 (state).
        return {
            "pid": pid,
            "process_state": fields[0],
            "parent_pid": int(fields[1]),
            "process_group_id": int(fields[2]),
            "session_id": int(fields[3]),
            "start_time_ticks": int(fields[19]),
        }
    except (ValueError, IndexError) as error:
        raise ProcessIdentityError(f"malformed /proc stat identity fields: {error}") from error


def read_process_identity(
    pid: int,
    *,
    boot_id_path: pathlib.Path = BOOT_ID_PATH,
    proc_root: pathlib.Path = pathlib.Path("/proc"),
) -> dict[str, object]:
    """Read a stable boot/PID/start-time identity and its immediate tree facts."""
    if not isinstance(pid, int) or pid <= 0:
        raise ProcessIdentityError(f"invalid process PID: {pid!r}")
    stat_path = proc_root / str(pid) / "stat"
    try:
        parsed = _parse_proc_stat(stat_path.read_text(encoding="ascii"))
    except OSError as error:
        raise ProcessIdentityError(f"cannot inspect process {pid}: {error}") from error
    if parsed["pid"] != pid:
        raise ProcessIdentityError(
            f"/proc identity PID mismatch: requested {pid}, observed {parsed['pid']}"
        )
    return {
        "schema": IDENTITY_SCHEMA,
        "boot_id": _read_boot_id(boot_id_path),
        **parsed,
    }


def compare_process_identity(expected: object, observed: object) -> dict[str, object]:
    """Compare only stable identity fields and name every mismatch."""
    stable_fields = ("schema", "boot_id", "pid", "start_time_ticks")
    if not isinstance(expected, dict) or not isinstance(observed, dict):
        return {"match": False, "mismatches": ["record-type"]}
    mismatches = [
        field for field in stable_fields if expected.get(field) != observed.get(field)
    ]
    return {"match": not mismatches, "mismatches": mismatches}


def _stable_ancestry_to(
    descendant_pid: int,
    ancestor: dict[str, object],
    *,
    boot_id_path: pathlib.Path = BOOT_ID_PATH,
    proc_root: pathlib.Path = pathlib.Path("/proc"),
    max_depth: int = MAX_REGISTRATION_ANCESTRY_DEPTH,
) -> list[dict[str, object]]:
    """Return a bounded, stable descendant-to-ancestor chain or reject it."""
    if max_depth < 1:
        raise ValueError("registration ancestry depth must be positive")
    ancestor_pid = ancestor.get("pid")
    if not isinstance(ancestor_pid, int) or ancestor_pid <= 0:
        raise ProcessIdentityError("registration ancestor identity is invalid")
    chain: list[dict[str, object]] = []
    seen: set[int] = set()
    current_pid = descendant_pid
    for _ in range(max_depth):
        if current_pid in seen:
            raise ProcessIdentityError("registration ancestry contains a PID cycle")
        seen.add(current_pid)
        observed = read_process_identity(
            current_pid, boot_id_path=boot_id_path, proc_root=proc_root
        )
        chain.append(observed)
        if current_pid == ancestor_pid:
            comparison = compare_process_identity(ancestor, observed)
            if not comparison["match"]:
                raise ProcessIdentityError(
                    "registration ancestor stable identity changed: "
                    + ", ".join(comparison["mismatches"])
                )
            return chain
        parent_pid = observed.get("parent_pid")
        if not isinstance(parent_pid, int) or parent_pid <= 1:
            break
        # Revalidate every link before trusting its recorded parent PID.
        reobserved = read_process_identity(
            current_pid, boot_id_path=boot_id_path, proc_root=proc_root
        )
        if not compare_process_identity(observed, reobserved)["match"]:
            raise ProcessIdentityError(
                f"registration ancestry identity changed while inspecting PID {current_pid}"
            )
        if reobserved.get("parent_pid") != parent_pid:
            raise ProcessIdentityError(
                f"registration ancestry parent changed while inspecting PID {current_pid}"
            )
        current_pid = parent_pid
    raise ProcessIdentityError(
        f"PID {descendant_pid} is not below stable ancestor {ancestor_pid} "
        f"within {max_depth} processes"
    )


def prove_assignment_descendant_registration(
    *,
    target_pid: int,
    registrar_pid: int,
    worker_identity: dict[str, object],
    boot_id_path: pathlib.Path = BOOT_ID_PATH,
    proc_root: pathlib.Path = pathlib.Path("/proc"),
    max_depth: int = MAX_REGISTRATION_ANCESTRY_DEPTH,
) -> dict[str, object]:
    """Prove the target and registrar are in one bounded live worker ancestry."""
    registrar_identity = read_process_identity(
        registrar_pid, boot_id_path=boot_id_path, proc_root=proc_root
    )
    target_to_registrar = _stable_ancestry_to(
        target_pid,
        registrar_identity,
        boot_id_path=boot_id_path,
        proc_root=proc_root,
        max_depth=max_depth,
    )
    registrar_to_worker = _stable_ancestry_to(
        registrar_pid,
        worker_identity,
        boot_id_path=boot_id_path,
        proc_root=proc_root,
        max_depth=max_depth,
    )
    return {
        "target_identity": target_to_registrar[0],
        "registrar_identity": registrar_identity,
        "worker_identity": registrar_to_worker[-1],
        "target_to_registrar": target_to_registrar,
        "registrar_to_worker": registrar_to_worker,
        "max_depth": max_depth,
    }


def atomic_write_json(path: pathlib.Path, value: dict[str, object]) -> None:
    """Replace one JSON record atomically and durably in its existing directory."""
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary: pathlib.Path | None = None
    try:
        descriptor, name = tempfile.mkstemp(
            prefix=f".{path.name}.", suffix=".tmp", dir=path.parent
        )
        temporary = pathlib.Path(name)
        with os.fdopen(descriptor, "w", encoding="utf-8") as stream:
            json.dump(value, stream, indent=2)
            stream.write("\n")
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
        temporary = None
        directory = os.open(path.parent, os.O_RDONLY | os.O_DIRECTORY)
        try:
            os.fsync(directory)
        finally:
            os.close(directory)
    finally:
        if temporary is not None:
            try:
                temporary.unlink()
            except FileNotFoundError:
                pass


def read_json_record(path: pathlib.Path) -> dict[str, object]:
    """Read one complete object record or reject it without guessing."""
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as error:
        raise ProcessIdentityError(f"cannot read valid record {path}: {error}") from error
    if not isinstance(value, dict):
        raise ProcessIdentityError(f"record is not a JSON object: {path}")
    return value


def assignment_record_path(output_dir: pathlib.Path) -> pathlib.Path:
    return output_dir / "assignment.json"


def register_watchdog_assignment(
    registry_path: pathlib.Path,
    assignment_root: pathlib.Path,
) -> dict[str, object]:
    """Atomically add one exact assignment to a stable watchdog registry."""
    assignment_root = assignment_root.resolve(strict=True)
    assignment = read_json_record(assignment_record_path(assignment_root))
    if assignment.get("schema") != ASSIGNMENT_SCHEMA:
        raise ProcessIdentityError("cannot register an assignment with an unknown schema")
    assignment_id = assignment.get("assignment_id")
    if not isinstance(assignment_id, str):
        raise ProcessIdentityError("cannot register an assignment without an identity")
    registry_path = registry_path.resolve()
    lease_path = registry_path.with_suffix(registry_path.suffix + ".lock")
    with assignment_lease(lease_path):
        if registry_path.exists():
            registry = read_json_record(registry_path)
            if registry.get("schema") != WATCHDOG_REGISTRY_SCHEMA:
                raise ProcessIdentityError("watchdog registry has an unknown schema")
            entries = registry.get("assignments")
            if not isinstance(entries, list):
                raise ProcessIdentityError("watchdog registry assignments must be a list")
            entries = list(entries)
        else:
            entries = []
        normalized = {
            "assignment_id": assignment_id,
            "assignment_root": str(assignment_root),
            "enabled": True,
        }
        matches = [
            item for item in entries
            if isinstance(item, dict)
            and (
                item.get("assignment_id") == assignment_id
                or item.get("assignment_root") == str(assignment_root)
            )
        ]
        if matches and any(
            item.get("assignment_id") != assignment_id
            or item.get("assignment_root") != str(assignment_root)
            for item in matches
        ):
            raise ProcessIdentityError("watchdog registry identity/root collision")
        entries = [item for item in entries if item not in matches]
        entries.append(normalized)
        value = {
            "schema": WATCHDOG_REGISTRY_SCHEMA,
            "assignments": entries,
            "updated_utc": utc_now(),
        }
        atomic_write_json(registry_path, value)
        return normalized


def initialize_assignment_record(
    output_dir: pathlib.Path,
    *,
    assignment_id: str,
    attempt_id: str,
    generation: int,
    role: str,
    worktree: pathlib.Path,
    job_file: pathlib.Path,
    branch: str | None = None,
) -> dict[str, object]:
    """Create the stable current-assignment view before the first child starts."""
    path = assignment_record_path(output_dir)
    with assignment_lease(output_dir / ".recovery.lock"):
        if path.exists():
            raise ProcessIdentityError(
                f"assignment record already exists; explicit recovery is required: {path}"
            )
        value = {
            "schema": ASSIGNMENT_SCHEMA,
            "assignment_id": assignment_id,
            "current_attempt_id": attempt_id,
            "generation": generation,
            "next_generation": generation + 1,
            "status": "launching",
            "role": role,
            "worktree": str(worktree.resolve()),
            "job_file": str(job_file.resolve()),
            "branch": branch,
            "current_attempt_dir": str(output_dir.resolve()),
            "registrations": {"attempt_id": attempt_id, "descendants": [], "resources": []},
            "stop_intent": None,
            "updated_utc": utc_now(),
        }
        atomic_write_json(path, value)
    return value


def register_assignment_ownership(
    output_dir: pathlib.Path,
    *,
    assignment_id: str,
    attempt_id: str,
    generation: int,
    descendant_pid: int | None = None,
    registrar_pid: int | None = None,
    resource: dict[str, object] | None = None,
) -> dict[str, object]:
    """Register one verified descendant/resource claim under the assignment lease."""
    output_dir = output_dir.resolve(strict=True)
    if (descendant_pid is None) == (resource is None):
        raise ValueError("register exactly one descendant or resource")
    with assignment_lease(output_dir / ".recovery.lock"):
        path = assignment_record_path(output_dir)
        value = read_json_record(path)
        expected = (assignment_id, attempt_id, generation)
        observed = (
            value.get("assignment_id"),
            value.get("current_attempt_id"),
            value.get("generation"),
        )
        if value.get("schema") != ASSIGNMENT_SCHEMA or observed != expected:
            raise ProcessIdentityError(
                f"registration lineage changed: expected {expected!r}, observed {observed!r}"
            )
        if value.get("status") not in {"launching", "running"}:
            raise ProcessIdentityError(
                f"assignment does not accept registrations in status {value.get('status')!r}"
            )
        registrations = value.get("registrations")
        if not isinstance(registrations, dict) or registrations.get("attempt_id") != attempt_id:
            raise ProcessIdentityError("assignment registrations do not match current attempt")
        registrations = {
            "attempt_id": attempt_id,
            "descendants": list(registrations.get("descendants", [])),
            "resources": list(registrations.get("resources", [])),
        }
        if descendant_pid is not None:
            if registrar_pid is None:
                raise ProcessIdentityError(
                    "descendant registration requires the live registrar PID"
                )
            attempt_dir_value = value.get("current_attempt_dir")
            if not isinstance(attempt_dir_value, str):
                raise ProcessIdentityError("assignment current attempt directory is invalid")
            attempt_dir = pathlib.Path(attempt_dir_value).resolve(strict=True)
            if attempt_dir.parent != output_dir / "attempts" and attempt_dir != output_dir:
                raise ProcessIdentityError(
                    "assignment current attempt directory escapes its protected root"
                )
            process = read_json_record(attempt_dir / "process.json")
            if (
                process.get("assignment_id"),
                process.get("attempt_id"),
                process.get("generation"),
            ) != expected:
                raise ProcessIdentityError(
                    "assignment worker record does not match registration lineage"
                )
            worker_identity = process.get("identity")
            if not isinstance(worker_identity, dict):
                raise ProcessIdentityError("assignment worker identity is unavailable")
            proof = prove_assignment_descendant_registration(
                target_pid=descendant_pid,
                registrar_pid=registrar_pid,
                worker_identity=worker_identity,
            )
            identity = dict(proof["target_identity"])
            identity["registration_proof"] = proof
            if not any(
                compare_process_identity(identity, item).get("match")
                for item in registrations["descendants"]
            ):
                registrations["descendants"].append(identity)
        else:
            if not isinstance(resource, dict):
                raise ValueError("resource registration must be an object")
            name = resource.get("resource")
            path_value = resource.get("path")
            device = resource.get("device")
            inode = resource.get("inode")
            if name not in {"game", "large-build"}:
                raise ValueError(f"unsupported registered resource: {name!r}")
            if not isinstance(path_value, str) or not isinstance(device, int) or not isinstance(inode, int):
                raise ValueError("resource registration requires path, device, and inode")
            normalized = {
                "resource": name,
                "path": str(pathlib.Path(path_value).resolve()),
                "device": device,
                "inode": inode,
            }
            if normalized not in registrations["resources"]:
                registrations["resources"].append(normalized)
        value = dict(value)
        value["registrations"] = registrations
        value["updated_utc"] = utc_now()
        atomic_write_json(path, value)
        return registrations


def request_stop(
    output_dir: pathlib.Path,
    *,
    reason: str,
    requested_by: str,
) -> dict[str, object]:
    """Durably publish stop intent before any process is considered for signalling."""
    output_dir = output_dir.resolve(strict=True)
    if not reason.strip() or not requested_by.strip():
        raise ValueError("stop reason and requester must be non-empty")
    with assignment_lease(output_dir / ".recovery.lock"):
        path = assignment_record_path(output_dir)
        value = read_json_record(path)
        if value.get("schema") != ASSIGNMENT_SCHEMA:
            raise ProcessIdentityError("assignment record has an unknown schema")
        intent = {
            "schema": STOP_SCHEMA,
            "assignment_id": value.get("assignment_id"),
            "attempt_id": value.get("current_attempt_id"),
            "generation": value.get("generation"),
            "requested_utc": utc_now(),
            "requested_by": requested_by,
            "reason": reason,
        }
        value = dict(value)
        value["stop_intent"] = intent
        value["status"] = "stop-requested"
        value["updated_utc"] = intent["requested_utc"]
        atomic_write_json(path, value)
        return intent


def authorize_stopped_start(
    output_dir: pathlib.Path,
    *,
    reason: str,
    requested_by: str,
    recover: object,
    lease_timeout_seconds: float = 5.0,
    boot_id_path: pathlib.Path = BOOT_ID_PATH,
    proc_root: pathlib.Path = pathlib.Path("/proc"),
    resource_lock_dir: pathlib.Path | None = None,
) -> dict[str, object]:
    """Explicitly supersede one resolved stop and start exactly one new attempt."""
    output_dir = output_dir.resolve(strict=True)
    if not reason.strip() or not requested_by.strip():
        raise ValueError("start reason and requester must be non-empty")
    with assignment_lease(output_dir / ".recovery.lock", lease_timeout_seconds):
        assignment_path = assignment_record_path(output_dir)
        assignment = read_json_record(assignment_path)
        if assignment.get("schema") != ASSIGNMENT_SCHEMA:
            raise ProcessIdentityError("assignment record has an unknown schema")
        if assignment.get("status") != "stopped":
            raise ProcessIdentityError(
                f"explicit start requires stopped status, observed {assignment.get('status')!r}"
            )
        stop_intent = assignment.get("stop_intent")
        if not isinstance(stop_intent, dict) or stop_intent.get("schema") != STOP_SCHEMA:
            raise ProcessIdentityError("stopped assignment has no valid durable stop intent")
        lineage = (
            assignment.get("assignment_id"),
            assignment.get("current_attempt_id"),
            assignment.get("generation"),
        )
        if (
            stop_intent.get("assignment_id"),
            stop_intent.get("attempt_id"),
            stop_intent.get("generation"),
        ) != lineage:
            raise ProcessIdentityError("stop intent does not match the stopped generation")
        attempt_dir_value = assignment.get("current_attempt_dir")
        if not isinstance(attempt_dir_value, str):
            raise ProcessIdentityError("assignment current attempt directory is invalid")
        attempt_dir = pathlib.Path(attempt_dir_value).resolve(strict=True)
        if attempt_dir != output_dir and not attempt_dir.is_relative_to(output_dir / "attempts"):
            raise ProcessIdentityError("assignment current attempt directory escapes its root")

        observations: list[dict[str, object]] = []
        for record_name in ("process.json", "supervisor.json"):
            record_path = attempt_dir / record_name
            if record_path.exists():
                record = read_json_record(record_path)
                if record.get("status") != "stopped":
                    raise ProcessIdentityError(
                        f"stopped assignment has non-stopped {record_name}"
                    )
                observations.append(
                    observe_process_identity(
                        record.get("identity"),
                        boot_id_path=boot_id_path,
                        proc_root=proc_root,
                    )
                )
        descendants, registered_resources = _registered_observations(
            assignment,
            attempt_id=str(assignment.get("current_attempt_id")),
            boot_id_path=boot_id_path,
            proc_root=proc_root,
        )
        observations.extend(descendants)
        if any(item.get("state") == "live" for item in observations):
            raise ProcessIdentityError("stopped assignment still has a verified live process")
        if any(not item.get("conclusive", True) for item in observations):
            raise ProcessIdentityError("stopped assignment process inspection is inconclusive")
        resources = _matching_resource_observations(
            resource_lock_dir, registered_resources
        )
        if any(item.get("state") != "observed" for item in resources):
            raise ProcessIdentityError("stopped assignment resource inspection is inconclusive")
        if any(
            slot.get("availability") != "available"
            for item in resources
            for slot in item.get("slots", [])
        ):
            raise ProcessIdentityError("stopped assignment resource remains contended")

        authorization = {
            "schema": START_SCHEMA,
            "assignment_id": assignment.get("assignment_id"),
            "predecessor_attempt_id": assignment.get("current_attempt_id"),
            "predecessor_generation": assignment.get("generation"),
            "authorized_utc": utc_now(),
            "requested_by": requested_by,
            "reason": reason,
            "superseded_stop_intent": stop_intent,
            "process_observations": observations,
            "resource_observations": resources,
        }
        start_path = attempt_dir / "start.json"
        atomic_write_json(start_path, authorization)
        authorized_assignment = dict(assignment)
        authorized_assignment.update(
            {
                "status": "start-authorized",
                "superseded_stop_intent": stop_intent,
                "start_authorization": str(start_path),
                "updated_utc": authorization["authorized_utc"],
            }
        )
        result = recover(
            output_dir,
            authorized_assignment,
            {"start_authorization": str(start_path)},
        )
        return {**result, "start_authorization": str(start_path)}


def _update_assignment_record_unlocked(
    output_dir: pathlib.Path,
    *,
    assignment_id: str,
    attempt_id: str,
    generation: int,
    status: str,
) -> dict[str, object]:
    """Compare and update the current view while its assignment lease is held."""
    path = assignment_record_path(output_dir)
    value = read_json_record(path)
    if value.get("schema") != ASSIGNMENT_SCHEMA:
        raise ProcessIdentityError("assignment record has an unknown schema")
    expected = (assignment_id, attempt_id, generation)
    observed = (
        value.get("assignment_id"),
        value.get("current_attempt_id"),
        value.get("generation"),
    )
    if observed != expected:
        raise ProcessIdentityError(
            f"assignment generation changed: expected {expected!r}, observed {observed!r}"
        )
    value = dict(value)
    value.update({"status": status, "updated_utc": utc_now()})
    atomic_write_json(path, value)
    return value


def update_assignment_record(
    output_dir: pathlib.Path,
    *,
    assignment_id: str,
    attempt_id: str,
    generation: int,
    status: str,
) -> dict[str, object]:
    """Serialize a current-view update and reject an ABA generation change."""
    with assignment_lease(output_dir / ".recovery.lock"):
        return _update_assignment_record_unlocked(
            output_dir,
            assignment_id=assignment_id,
            attempt_id=attempt_id,
            generation=generation,
            status=status,
        )


@contextmanager
def assignment_lease(path: pathlib.Path, timeout_seconds: float = 5.0):
    """Serialize assignment transitions on a stable, never-unlinked flock inode."""
    if timeout_seconds < 0:
        raise ValueError("lease timeout must be non-negative")
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor = os.open(path, os.O_RDWR | os.O_CREAT, 0o600)
    deadline = time.monotonic() + timeout_seconds
    try:
        while True:
            try:
                fcntl.flock(descriptor, fcntl.LOCK_EX | fcntl.LOCK_NB)
                break
            except BlockingIOError:
                if time.monotonic() >= deadline:
                    raise TimeoutError(f"assignment lease remained contended: {path}")
                time.sleep(min(0.05, max(0.0, deadline - time.monotonic())))
        yield
    finally:
        os.close(descriptor)


def observe_process_identity(
    expected: object,
    *,
    boot_id_path: pathlib.Path = BOOT_ID_PATH,
    proc_root: pathlib.Path = pathlib.Path("/proc"),
) -> dict[str, object]:
    """Classify one expected identity without treating PID existence as proof."""
    if not isinstance(expected, dict) or not isinstance(expected.get("pid"), int):
        return {"state": "invalid-record", "conclusive": False}
    pid = expected["pid"]
    try:
        observed = read_process_identity(
            pid, boot_id_path=boot_id_path, proc_root=proc_root
        )
    except ProcessIdentityError as error:
        if not (proc_root / str(pid)).exists():
            return {
                "state": "missing",
                "conclusive": True,
                "expected": expected,
                "detail": str(error),
            }
        return {
            "state": "inspection-error",
            "conclusive": False,
            "expected": expected,
            "detail": str(error),
        }
    comparison = compare_process_identity(expected, observed)
    if not comparison["match"]:
        return {
            "state": "identity-mismatch",
            "conclusive": True,
            "expected": expected,
            "observed": observed,
            "mismatches": comparison["mismatches"],
        }
    if observed.get("process_state") == "Z":
        return {
            "state": "zombie",
            "conclusive": True,
            "expected": expected,
            "observed": observed,
        }
    return {
        "state": "live",
        "conclusive": True,
        "expected": expected,
        "observed": observed,
    }


def observe_registered_descendants(
    identities: object,
    *,
    boot_id_path: pathlib.Path = BOOT_ID_PATH,
    proc_root: pathlib.Path = pathlib.Path("/proc"),
) -> list[dict[str, object]]:
    """Inspect only explicitly registered descendants; never discover by PID text."""
    if identities is None:
        return []
    if not isinstance(identities, list):
        return [{"state": "invalid-record", "conclusive": False}]
    return [
        observe_process_identity(
            identity, boot_id_path=boot_id_path, proc_root=proc_root
        )
        for identity in identities
    ]


def observe_resource_status(
    lock_dir: pathlib.Path,
    resources: tuple[str, ...],
    *,
    timeout_seconds: float = 5.0,
) -> list[dict[str, object]]:
    """Consume CNC-94's canonical status CLI without interpreting lock JSON."""
    observations: list[dict[str, object]] = []
    for resource in resources:
        if resource == "game":
            policy = ["--resource", "game", "--capacity", "2"]
        elif resource == "large-build":
            policy = ["--large-build-entry", "worker"]
        else:
            observations.append(
                {"resource": resource, "state": "unsupported", "conclusive": False}
            )
            continue
        command = [
            sys.executable,
            str(RESOURCE_SCRIPT),
            "--lock-dir",
            str(lock_dir),
            *policy,
            "--status",
        ]
        try:
            completed = subprocess.run(
                command, capture_output=True, text=True, timeout=timeout_seconds
            )
        except (OSError, subprocess.TimeoutExpired) as error:
            observations.append(
                {"resource": resource, "state": "inspection-error", "detail": str(error)}
            )
            continue
        parsed = []
        for line in completed.stderr.splitlines():
            prefix = "resource-slot "
            if not line.startswith(prefix):
                continue
            try:
                event = json.loads(line[len(prefix) :])
            except json.JSONDecodeError:
                continue
            if event.get("event") == "status":
                event.pop("last_known_metadata", None)
                parsed.append(event)
        observations.append(
            {
                "resource": resource,
                "state": "observed" if completed.returncode == 0 and parsed else "inspection-error",
                "exit_code": completed.returncode,
                "slots": parsed,
            }
        )
    return observations


def _validate_attempt_pair(
    process: dict[str, object], supervisor: dict[str, object]
) -> tuple[str, str, int]:
    fields = ("assignment_id", "attempt_id", "generation")
    if (
        process.get("schema") != RECORD_SCHEMA
        or supervisor.get("schema") != RECORD_SCHEMA
    ):
        raise ProcessIdentityError("attempt records have an unknown schema")
    if any(process.get(field) != supervisor.get(field) for field in fields):
        raise ProcessIdentityError("process and supervisor lineage do not match")
    assignment_id = process.get("assignment_id")
    attempt_id = process.get("attempt_id")
    generation = process.get("generation")
    if not isinstance(assignment_id, str) or not isinstance(attempt_id, str):
        raise ProcessIdentityError("attempt lineage IDs are invalid")
    if not isinstance(generation, int) or generation < 1:
        raise ProcessIdentityError("attempt generation is invalid")
    return assignment_id, attempt_id, generation


def _registered_observations(
    assignment: dict[str, object],
    *,
    attempt_id: str,
    boot_id_path: pathlib.Path,
    proc_root: pathlib.Path,
) -> tuple[list[dict[str, object]], list[dict[str, object]]]:
    registrations = assignment.get("registrations")
    if not isinstance(registrations, dict) or registrations.get("attempt_id") != attempt_id:
        raise ProcessIdentityError("assignment registrations do not match current attempt")
    descendants = observe_registered_descendants(
        registrations.get("descendants"),
        boot_id_path=boot_id_path,
        proc_root=proc_root,
    )
    resources = registrations.get("resources")
    if not isinstance(resources, list) or not all(isinstance(item, dict) for item in resources):
        raise ProcessIdentityError("assignment resource registrations are invalid")
    return descendants, resources


def _matching_resource_observations(
    lock_dir: pathlib.Path | None,
    registrations: list[dict[str, object]],
) -> list[dict[str, object]]:
    if not registrations:
        return []
    if lock_dir is None:
        return [{"state": "inspection-error", "detail": "resource lock directory required"}]
    names = tuple(sorted({str(item["resource"]) for item in registrations}))
    observations = observe_resource_status(lock_dir, names)
    for item in observations:
        registered = [entry for entry in registrations if entry.get("resource") == item.get("resource")]
        slots = []
        for slot in item.get("slots", []):
            if any(
                str(pathlib.Path(str(slot.get("path"))).resolve()) == entry.get("path")
                and slot.get("device") == entry.get("device")
                and slot.get("inode") == entry.get("inode")
                for entry in registered
            ):
                slots.append(slot)
        item["slots"] = slots
        item["registered_slots_found"] = len(slots)
        item["registered_slots_expected"] = len(registered)
        if len(slots) != len(registered):
            item["state"] = "inspection-error"
            item["detail"] = "registered resource path/device/inode no longer matches canonical status"
    return observations


def _resolve_verified_identities(
    observations: list[dict[str, object]],
    *,
    boot_id_path: pathlib.Path,
    proc_root: pathlib.Path,
    grace_seconds: float,
    kill_seconds: float,
) -> list[dict[str, object]]:
    """Boundedly stop only identities revalidated immediately before each signal."""
    if grace_seconds < 0 or kill_seconds < 0:
        raise ValueError("process resolution timeouts must be non-negative")
    outcomes: list[dict[str, object]] = []
    live_expected = [item.get("expected") for item in observations if item.get("state") == "live"]
    for expected in live_expected:
        current = observe_process_identity(expected, boot_id_path=boot_id_path, proc_root=proc_root)
        outcome = {"expected": expected, "pre_signal": current, "signals": []}
        if current.get("state") != "live":
            outcome["result"] = "resolved-without-signal"
            outcomes.append(outcome)
            continue
        pid = int(expected["pid"])
        os.kill(pid, signal.SIGTERM)
        outcome["signals"].append("SIGTERM")
        deadline = time.monotonic() + grace_seconds
        while time.monotonic() < deadline:
            current = observe_process_identity(expected, boot_id_path=boot_id_path, proc_root=proc_root)
            if current.get("state") != "live":
                break
            time.sleep(min(0.05, max(0.0, deadline - time.monotonic())))
        if current.get("state") == "live":
            revalidated = observe_process_identity(expected, boot_id_path=boot_id_path, proc_root=proc_root)
            if revalidated.get("state") != "live":
                outcome["result"] = "resolved-without-kill"
                outcomes.append(outcome)
                continue
            os.kill(pid, signal.SIGKILL)
            outcome["signals"].append("SIGKILL")
            deadline = time.monotonic() + kill_seconds
            while time.monotonic() < deadline:
                current = observe_process_identity(expected, boot_id_path=boot_id_path, proc_root=proc_root)
                if current.get("state") != "live":
                    break
                time.sleep(min(0.05, max(0.0, deadline - time.monotonic())))
        final = observe_process_identity(expected, boot_id_path=boot_id_path, proc_root=proc_root)
        outcome["final"] = final
        outcome["result"] = "resolved" if final.get("state") != "live" else "timeout"
        outcomes.append(outcome)
    return outcomes


def _attempt_artifact_facts(
    attempt_dir: pathlib.Path, *, excluded_names: set[str]
) -> list[dict[str, object]]:
    facts: list[dict[str, object]] = []
    for path in sorted(attempt_dir.iterdir(), key=lambda item: item.name):
        if path.name in excluded_names:
            continue
        try:
            facts.append(
                {
                    "path": str(path),
                    "size": path.stat().st_size,
                    "kind": "file" if path.is_file() else "other",
                }
            )
        except OSError as error:
            facts.append({"path": str(path), "inspection_error": str(error)})
    return facts


def _persist_unverified_resource_block(
    assignment_root: pathlib.Path,
    attempt_dir: pathlib.Path,
    *,
    assignment: dict[str, object],
    process: dict[str, object],
    supervisor: dict[str, object],
    base_result: dict[str, object],
) -> dict[str, object]:
    """Quarantine a dead attempt without touching an unknown flock owner."""
    blocked_path = attempt_dir / "blocked.json"
    quarantine_path = attempt_dir / "quarantine.json"
    if (
        assignment.get("status") == "blocked"
        and assignment.get("blocked_reason") == "canonical-resource-owner-unverified"
        and blocked_path.exists()
    ):
        return {
            "event": "blocked",
            "reason": "canonical-resource-owner-unverified",
            "blocked_record": str(blocked_path),
            "quarantine": str(quarantine_path),
            **base_result,
        }

    observed_utc = utc_now()
    artifact_facts = _attempt_artifact_facts(
        attempt_dir,
        excluded_names={blocked_path.name, quarantine_path.name},
    )
    blocked = {
        "schema": BLOCKED_SCHEMA,
        "status": "blocked",
        "observed_utc": observed_utc,
        "reason": "canonical-resource-owner-unverified",
        "diagnostic": (
            "recorded worker and supervisor are not live, no verified registered "
            "descendant owns the contended canonical resource, and lock metadata "
            "is last-known only"
        ),
        "signal_policy": "no process signalled; canonical lock path and inode preserved",
        "old_process_record": process,
        "old_supervisor_record": supervisor,
        "quarantine": str(quarantine_path),
        **base_result,
    }
    quarantine = {
        "schema": QUARANTINE_SCHEMA,
        "assignment_id": base_result["assignment_id"],
        "attempt_id": base_result["attempt_id"],
        "generation": base_result["generation"],
        "status": "non-acceptance",
        "reason": "owning worker tree is dead while a canonical resource owner is unverified",
        "observed_utc": observed_utc,
        "artifacts": artifact_facts,
        "blocked_record": str(blocked_path),
    }
    atomic_write_json(quarantine_path, quarantine)
    atomic_write_json(blocked_path, blocked)
    for path, record in (
        (attempt_dir / "process.json", process),
        (attempt_dir / "supervisor.json", supervisor),
    ):
        updated = dict(record)
        updated.update(
            {
                "status": "blocked",
                "blocked_utc": observed_utc,
                "blocked_reason": blocked["reason"],
                "blocked_record": str(blocked_path),
            }
        )
        atomic_write_json(path, updated)
    assignment.update(
        {
            "status": "blocked",
            "blocked_reason": blocked["reason"],
            "blocked_record": str(blocked_path),
            "updated_utc": observed_utc,
        }
    )
    atomic_write_json(assignment_record_path(assignment_root), assignment)
    return {
        "event": "blocked",
        "reason": blocked["reason"],
        "blocked_record": str(blocked_path),
        "quarantine": str(quarantine_path),
        **base_result,
    }


def audit_attempt(
    output_dir: pathlib.Path,
    *,
    lease_timeout_seconds: float = 5.0,
    boot_id_path: pathlib.Path = BOOT_ID_PATH,
    proc_root: pathlib.Path = pathlib.Path("/proc"),
    resource_lock_dir: pathlib.Path | None = None,
    recover: object | None = None,
    resolve_grace_seconds: float = 2.0,
    resolve_kill_seconds: float = 2.0,
    launch_stale_seconds: float = 30.0,
) -> dict[str, object]:
    """Audit one attempt and terminalize a conclusively dead running claim."""
    output_dir = output_dir.resolve(strict=True)
    if not output_dir.is_dir():
        raise ProcessIdentityError(f"attempt output is not a directory: {output_dir}")
    assignment_root = output_dir
    lease_path = assignment_root / ".recovery.lock"
    with assignment_lease(lease_path, lease_timeout_seconds):
        # Every decision is made from records reread after kernel serialization.
        assignment = read_json_record(assignment_record_path(assignment_root))
        if assignment.get("schema") != ASSIGNMENT_SCHEMA:
            raise ProcessIdentityError("assignment record has an unknown schema")
        attempt_dir_value = assignment.get("current_attempt_dir")
        if not isinstance(attempt_dir_value, str):
            raise ProcessIdentityError("assignment current attempt directory is invalid")
        attempt_dir = pathlib.Path(attempt_dir_value).resolve(strict=True)
        if attempt_dir != assignment_root and not attempt_dir.is_relative_to(assignment_root / "attempts"):
            raise ProcessIdentityError("assignment current attempt directory escapes its root")
        process_path = attempt_dir / "process.json"
        supervisor_path = attempt_dir / "supervisor.json"
        stop_intent = assignment.get("stop_intent")
        if launch_stale_seconds <= 0:
            raise ValueError("launch stale timeout must be positive")
        if stop_intent is not None and not process_path.exists():
            supervisor_record = (
                read_json_record(supervisor_path) if supervisor_path.exists() else None
            )
            supervisor_observation = (
                observe_process_identity(
                    supervisor_record.get("identity"),
                    boot_id_path=boot_id_path,
                    proc_root=proc_root,
                )
                if supervisor_record is not None
                else {"state": "missing", "conclusive": True}
            )
            outcomes = _resolve_verified_identities(
                [supervisor_observation],
                boot_id_path=boot_id_path,
                proc_root=proc_root,
                grace_seconds=resolve_grace_seconds,
                kill_seconds=resolve_kill_seconds,
            )
            if any(item.get("result") == "timeout" for item in outcomes):
                return {
                    "event": "blocked",
                    "reason": "stop-launching-supervisor-timeout",
                    "stop_intent": stop_intent,
                    "resolution": outcomes,
                }
            stopped_utc = utc_now()
            stop_record = attempt_dir / "stop.json"
            stopped = {
                "schema": STOP_SCHEMA,
                "status": "stopped",
                "stopped_utc": stopped_utc,
                "stop_intent": stop_intent,
                "supervisor": supervisor_observation,
                "resolution": outcomes,
                "reason": "stop won before worker process record was published",
            }
            atomic_write_json(stop_record, stopped)
            if supervisor_record is not None:
                supervisor_record.update(
                    {
                        "status": "stopped",
                        "stopped_utc": stopped_utc,
                        "stop_record": str(stop_record),
                    }
                )
                atomic_write_json(supervisor_path, supervisor_record)
            assignment.update({"status": "stopped", "updated_utc": stopped_utc})
            atomic_write_json(assignment_record_path(assignment_root), assignment)
            return {
                "event": "stopped",
                "stop_record": str(stop_record),
                "resolution": outcomes,
            }
        if assignment.get("status") == "blocked" and not process_path.exists():
            return {
                "event": "blocked",
                "reason": assignment.get("blocked_reason", "assignment is blocked"),
                "assignment_id": assignment.get("assignment_id"),
                "attempt_id": assignment.get("current_attempt_id"),
                "generation": assignment.get("generation"),
                "attempt_dir": str(attempt_dir),
            }
        if assignment.get("status") in {"launching", "recovering"} and not process_path.exists():
            phase = str(assignment["status"])
            supervisor_record = (
                read_json_record(supervisor_path) if supervisor_path.exists() else None
            )
            supervisor_observation = (
                observe_process_identity(
                    supervisor_record.get("identity"),
                    boot_id_path=boot_id_path,
                    proc_root=proc_root,
                )
                if supervisor_record is not None
                else {"state": "missing", "conclusive": True}
            )
            elapsed = age_seconds(assignment.get("updated_utc"))
            if (
                supervisor_observation.get("state") == "live" or supervisor_record is None
            ) and elapsed < launch_stale_seconds:
                return {
                    "event": f"{phase}-in-progress",
                    "assignment_id": assignment.get("assignment_id"),
                    "attempt_id": assignment.get("current_attempt_id"),
                    "generation": assignment.get("generation"),
                    "attempt_dir": str(attempt_dir),
                    "age_seconds": elapsed,
                    "supervisor": supervisor_observation,
                }
            resolution = _resolve_verified_identities(
                [supervisor_observation],
                boot_id_path=boot_id_path,
                proc_root=proc_root,
                grace_seconds=resolve_grace_seconds,
                kill_seconds=resolve_kill_seconds,
            )
            reason = (
                f"{phase}-supervisor-not-live"
                if supervisor_observation.get("state") != "live"
                else f"{phase}-timeout"
            )
            if not supervisor_observation.get("conclusive", True):
                reason = f"{phase}-supervisor-inspection-inconclusive"
            if any(item.get("result") == "timeout" for item in resolution):
                reason = f"{phase}-supervisor-resolution-timeout"
            blocked = dict(assignment)
            blocked.update(
                {
                    "status": "blocked",
                    "blocked_reason": reason,
                    "blocked_observation": {
                        "age_seconds": elapsed,
                        "stale_after_seconds": launch_stale_seconds,
                        "supervisor": supervisor_observation,
                        "resolution": resolution,
                    },
                    "updated_utc": utc_now(),
                }
            )
            atomic_write_json(assignment_record_path(assignment_root), blocked)
            return {
                "event": "blocked",
                "reason": reason,
                "assignment_id": assignment.get("assignment_id"),
                "attempt_id": assignment.get("current_attempt_id"),
                "generation": assignment.get("generation"),
                "attempt_dir": str(attempt_dir),
                "age_seconds": elapsed,
                "supervisor": supervisor_observation,
                "resolution": resolution,
            }
        process = read_json_record(process_path)
        supervisor = read_json_record(supervisor_path)
        assignment_id, attempt_id, generation = _validate_attempt_pair(
            process, supervisor
        )
        current = (
            assignment.get("assignment_id"),
            assignment.get("current_attempt_id"),
            assignment.get("generation"),
        )
        if current != (assignment_id, attempt_id, generation):
            return {
                "event": "superseded",
                "assignment_id": assignment_id,
                "attempt_id": attempt_id,
                "generation": generation,
                "current": current,
            }
        recoverable_signal_failure = (
            process.get("status") == "failed"
            and assignment.get("role") == "worker"
            and isinstance(process.get("child_exit_code"), int)
            and process["child_exit_code"] < 0
            and stop_intent is None
        )
        recoverable_resource_block = (
            process.get("status") == "blocked"
            and assignment.get("status") == "blocked"
            and process.get("blocked_reason") == "canonical-resource-owner-unverified"
            and assignment.get("blocked_reason") == "canonical-resource-owner-unverified"
            and stop_intent is None
        )
        if (
            process.get("status") != "running"
            and not recoverable_signal_failure
            and not recoverable_resource_block
            and (stop_intent is None or assignment.get("status") == "stopped")
        ):
            return {
                "event": "already-terminal",
                "assignment_id": assignment_id,
                "attempt_id": attempt_id,
                "generation": generation,
                "status": process.get("status"),
            }

        worker = observe_process_identity(
            process.get("identity"),
            boot_id_path=boot_id_path,
            proc_root=proc_root,
        )
        supervisor_observation = observe_process_identity(
            process.get("supervisor_identity") or supervisor.get("identity"),
            boot_id_path=boot_id_path,
            proc_root=proc_root,
        )
        descendants, registered_resources = _registered_observations(
            assignment,
            attempt_id=attempt_id,
            boot_id_path=boot_id_path,
            proc_root=proc_root,
        )
        resource_observations = _matching_resource_observations(
            resource_lock_dir, registered_resources
        )
        states = (worker["state"], supervisor_observation["state"])
        base_result = {
            "assignment_id": assignment_id,
            "attempt_id": attempt_id,
            "generation": generation,
            "worker": worker,
            "supervisor": supervisor_observation,
            "descendants": descendants,
            "resources": resource_observations,
        }
        if stop_intent is not None:
            stop_targets = [worker, supervisor_observation, *descendants]
            outcomes = _resolve_verified_identities(
                stop_targets,
                boot_id_path=boot_id_path,
                proc_root=proc_root,
                grace_seconds=resolve_grace_seconds,
                kill_seconds=resolve_kill_seconds,
            )
            if any(item.get("result") == "timeout" for item in outcomes):
                return {"event": "blocked", "reason": "stop-tree-resolution-timeout", "stop_intent": stop_intent, "resolution": outcomes, **base_result}
            stopped_resources = _matching_resource_observations(resource_lock_dir, registered_resources)
            if any(item.get("state") != "observed" for item in stopped_resources) or any(
                slot.get("availability") == "contended"
                for item in stopped_resources for slot in item.get("slots", [])
            ):
                return {"event": "blocked", "reason": "stop-resource-remained-contended", "stop_intent": stop_intent, "resolution": outcomes, "resources_after_resolution": stopped_resources, **base_result}
            stopped_utc = utc_now()
            stop_record = attempt_dir / "stop.json"
            atomic_write_json(stop_record, {"schema": STOP_SCHEMA, "status": "stopped", "stopped_utc": stopped_utc, "stop_intent": stop_intent, "resolution": outcomes, **base_result})
            for path, record in ((process_path, process), (supervisor_path, supervisor)):
                updated = dict(record)
                updated.update({"status": "stopped", "stopped_utc": stopped_utc, "stop_record": str(stop_record)})
                atomic_write_json(path, updated)
            _update_assignment_record_unlocked(assignment_root, assignment_id=assignment_id, attempt_id=attempt_id, generation=generation, status="stopped")
            return {"event": "stopped", "stop_record": str(stop_record), "resolution": outcomes, **base_result}

        if states == ("live", "live"):
            return {"event": "healthy", **base_result}
        if "live" in states:
            return {"event": "partial-tree", **base_result}
        if any(item.get("state") == "live" for item in descendants) and recover is None:
            return {
                "event": "partial-tree",
                "reason": "registered-descendant-still-live",
                **base_result,
            }
        if any(not item.get("conclusive", True) for item in descendants):
            return {
                "event": "blocked",
                "reason": "descendant-inspection-inconclusive",
                **base_result,
            }
        resource_error = any(
            item.get("state") != "observed" for item in resource_observations
        )
        if resource_error:
            return {
                "event": "blocked",
                "reason": "resource-inspection-inconclusive",
                **base_result,
            }
        if any(
            slot.get("availability") == "contended"
            for item in resource_observations
            for slot in item.get("slots", [])
        ) and not any(item.get("state") == "live" for item in descendants):
            return _persist_unverified_resource_block(
                assignment_root,
                attempt_dir,
                assignment=assignment,
                process=process,
                supervisor=supervisor,
                base_result=base_result,
            )
        resolution = []
        if any(item.get("state") == "live" for item in descendants):
            resolution = _resolve_verified_identities(
                descendants,
                boot_id_path=boot_id_path,
                proc_root=proc_root,
                grace_seconds=resolve_grace_seconds,
                kill_seconds=resolve_kill_seconds,
            )
            if any(not str(item.get("result", "")).startswith("resolved") for item in resolution):
                return {"event": "blocked", "reason": "verified-descendant-resolution-failed", "resolution": resolution, **base_result}
            resource_observations = _matching_resource_observations(resource_lock_dir, registered_resources)
            base_result["resources_after_resolution"] = resource_observations
            if any(item.get("state") != "observed" for item in resource_observations) or any(
                slot.get("availability") == "contended"
                for item in resource_observations for slot in item.get("slots", [])
            ):
                return {"event": "blocked", "reason": "resource-remained-contended-after-resolution", "resolution": resolution, **base_result}
        if not worker["conclusive"] or not supervisor_observation["conclusive"]:
            return {
                "event": "blocked",
                "reason": "identity-inspection-inconclusive",
                **base_result,
            }

        observed_utc = utc_now()
        interruption_path = attempt_dir / "interruption.json"
        quarantine_path = attempt_dir / "quarantine.json"
        artifact_facts = _attempt_artifact_facts(
            attempt_dir,
            excluded_names={
                lease_path.name,
                interruption_path.name,
                quarantine_path.name,
            },
        )
        interruption = {
            "schema": INTERRUPTION_SCHEMA,
            "assignment_id": assignment_id,
            "attempt_id": attempt_id,
            "generation": generation,
            "status": "interrupted",
            "observed_utc": observed_utc,
            "reason": (
                "worker-exited-by-signal-and-supervisor-identity-not-live"
                if recoverable_signal_failure
                else "recorded-worker-and-supervisor-identities-not-live"
            ),
            "worker": worker,
            "supervisor": supervisor_observation,
            "descendants": descendants,
            "resources": resource_observations,
            "resources_after_resolution": base_result.get("resources_after_resolution", []),
            "resolution": resolution,
            "old_process_record": process,
            "old_supervisor_record": supervisor,
            "lease": {"path": str(lease_path)},
            "quarantine": str(quarantine_path),
        }
        quarantine = {
            "schema": QUARANTINE_SCHEMA,
            "assignment_id": assignment_id,
            "attempt_id": attempt_id,
            "generation": generation,
            "status": "non-acceptance",
            "reason": "owning worker tree was interrupted",
            "observed_utc": observed_utc,
            "artifacts": artifact_facts,
        }
        # Receipts exist durably before readers can observe the terminal state.
        atomic_write_json(quarantine_path, quarantine)
        atomic_write_json(interruption_path, interruption)
        transitioned_process = dict(process)
        transitioned_process.update(
            {
                "status": "interrupted",
                "interrupted_utc": observed_utc,
                "interruption_record": str(interruption_path),
                "quarantine_record": str(quarantine_path),
            }
        )
        atomic_write_json(process_path, transitioned_process)
        transitioned_supervisor = dict(supervisor)
        transitioned_supervisor.update(
            {
                "status": "interrupted",
                "interrupted_utc": observed_utc,
                "interruption_record": str(interruption_path),
            }
        )
        atomic_write_json(supervisor_path, transitioned_supervisor)
        _update_assignment_record_unlocked(
            assignment_root,
            assignment_id=assignment_id,
            attempt_id=attempt_id,
            generation=generation,
            status="interrupted",
        )
        result = {
            "event": "interrupted",
            **base_result,
            "resolution": resolution,
            "interruption_record": str(interruption_path),
            "quarantine_record": str(quarantine_path),
        }
        if recover is not None:
            try:
                result = recover(assignment_root, read_json_record(assignment_record_path(assignment_root)), result)
            except (OSError, ProcessIdentityError, RuntimeError, ValueError) as error:
                current_assignment = read_json_record(assignment_record_path(assignment_root))
                if (
                    current_assignment.get("current_attempt_id") == attempt_id
                    and current_assignment.get("generation") == generation
                ):
                    current_assignment.update(
                        {
                            "status": "blocked",
                            "blocked_reason": f"relaunch rejected: {error}",
                            "updated_utc": utc_now(),
                        }
                    )
                    atomic_write_json(assignment_record_path(assignment_root), current_assignment)
                return {**result, "event": "blocked", "reason": "relaunch-rejected", "error": str(error)}
        return result
