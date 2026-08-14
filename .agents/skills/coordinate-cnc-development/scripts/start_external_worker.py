#!/usr/bin/env python3
"""Explicitly authorize one stopped coordinated worker to start a new attempt."""

from __future__ import annotations

import argparse
import json
import pathlib
import sys

from external_worker_runtime import ProcessIdentityError, authorize_stopped_start
from launch_role import relaunch_assignment


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output-dir", required=True, type=pathlib.Path)
    parser.add_argument("--reason", required=True)
    parser.add_argument("--requested-by", required=True)
    parser.add_argument("--resource-lock-dir", type=pathlib.Path)
    parser.add_argument("--lease-timeout", type=float, default=5.0)
    args = parser.parse_args()
    try:
        result = authorize_stopped_start(
            args.output_dir,
            reason=args.reason,
            requested_by=args.requested_by,
            recover=relaunch_assignment,
            resource_lock_dir=args.resource_lock_dir,
            lease_timeout_seconds=args.lease_timeout,
        )
    except (OSError, ProcessIdentityError, TimeoutError, ValueError) as error:
        print(json.dumps({"event": "start-error", "error": str(error)}))
        return 75
    print(json.dumps(result, sort_keys=True))
    return 0


if __name__ == "__main__":
    sys.exit(main())
