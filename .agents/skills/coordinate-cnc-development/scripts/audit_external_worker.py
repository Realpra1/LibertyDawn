#!/usr/bin/env python3
"""Run one bounded, serialized audit of an external worker attempt."""

from __future__ import annotations

import argparse
import json
import pathlib
import sys

from external_worker_runtime import ProcessIdentityError, audit_attempt
from launch_role import relaunch_assignment


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output-dir", required=True, type=pathlib.Path)
    parser.add_argument("--lease-timeout", type=float, default=5.0)
    parser.add_argument("--resource-lock-dir", type=pathlib.Path)
    parser.add_argument("--recover", action="store_true")
    parser.add_argument("--resolve-grace", type=float, default=2.0)
    parser.add_argument("--resolve-kill", type=float, default=2.0)
    parser.add_argument("--launch-stale", type=float, default=30.0)
    args = parser.parse_args()
    try:
        result = audit_attempt(
            args.output_dir,
            lease_timeout_seconds=args.lease_timeout,
            resource_lock_dir=args.resource_lock_dir,
            recover=relaunch_assignment if args.recover else None,
            resolve_grace_seconds=args.resolve_grace,
            resolve_kill_seconds=args.resolve_kill,
            launch_stale_seconds=args.launch_stale,
        )
    except (OSError, ProcessIdentityError, TimeoutError, ValueError) as error:
        print(json.dumps({"event": "audit-error", "error": str(error)}))
        return 75
    print(json.dumps(result, sort_keys=True))
    return 0 if result["event"] != "blocked" else 75


if __name__ == "__main__":
    sys.exit(main())
