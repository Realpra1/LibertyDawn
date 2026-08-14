#!/usr/bin/env python3
"""Register assignment-owned descendants or canonical resource slots."""

from __future__ import annotations

import argparse
import json
import os
import pathlib
import sys

from external_worker_runtime import ProcessIdentityError, register_assignment_ownership


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--assignment-root", required=True, type=pathlib.Path)
    choice = parser.add_mutually_exclusive_group(required=True)
    choice.add_argument("--descendant-pid", type=int)
    choice.add_argument("--resource-json")
    args = parser.parse_args()
    try:
        assignment_id = os.environ["LIBERTY_DAWN_ASSIGNMENT_ID"]
        attempt_id = os.environ["LIBERTY_DAWN_ATTEMPT_ID"]
        generation = int(os.environ["LIBERTY_DAWN_ATTEMPT_GENERATION"])
        expected_root = pathlib.Path(os.environ["LIBERTY_DAWN_ASSIGNMENT_ROOT"]).resolve(strict=True)
        supplied_root = args.assignment_root.resolve(strict=True)
        if supplied_root != expected_root:
            raise ProcessIdentityError("registration assignment root does not match protected environment")
        resource = json.loads(args.resource_json) if args.resource_json is not None else None
        result = register_assignment_ownership(
            supplied_root,
            assignment_id=assignment_id,
            attempt_id=attempt_id,
            generation=generation,
            descendant_pid=args.descendant_pid,
            registrar_pid=os.getppid() if args.descendant_pid is not None else None,
            resource=resource,
        )
    except (KeyError, json.JSONDecodeError, OSError, ProcessIdentityError, ValueError) as error:
        print(json.dumps({"event": "registration-error", "error": str(error)}))
        return 75
    print(json.dumps({"event": "registered", "registrations": result}, sort_keys=True))
    return 0


if __name__ == "__main__":
    sys.exit(main())
