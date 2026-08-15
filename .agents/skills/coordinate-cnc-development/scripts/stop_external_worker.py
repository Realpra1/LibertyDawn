#!/usr/bin/env python3
"""Durably request a coordinated external worker stop and resolve it by audit."""

from __future__ import annotations

import argparse
import json
import pathlib
import sys

from external_worker_runtime import ProcessIdentityError, audit_attempt, request_stop


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output-dir", required=True, type=pathlib.Path)
    parser.add_argument("--reason", required=True)
    parser.add_argument("--requested-by", required=True)
    parser.add_argument("--resource-lock-dir", type=pathlib.Path)
    parser.add_argument("--resolve-grace", type=float, default=2.0)
    parser.add_argument("--resolve-kill", type=float, default=2.0)
    args = parser.parse_args()
    try:
        intent = request_stop(args.output_dir, reason=args.reason, requested_by=args.requested_by)
        result = audit_attempt(
            args.output_dir,
            resource_lock_dir=args.resource_lock_dir,
            resolve_grace_seconds=args.resolve_grace,
            resolve_kill_seconds=args.resolve_kill,
        )
    except (OSError, ProcessIdentityError, TimeoutError, ValueError) as error:
        print(json.dumps({"event": "stop-error", "error": str(error)}))
        return 75
    print(json.dumps({"event": result["event"], "intent": intent, "audit": result}, sort_keys=True))
    return 0 if result["event"] == "stopped" else 75


if __name__ == "__main__":
    sys.exit(main())
