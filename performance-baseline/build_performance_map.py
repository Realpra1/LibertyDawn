#!/usr/bin/env python3
"""Build the deterministic CNC performance workload map from Empire Earth4."""

from __future__ import annotations

import argparse
import re
import zipfile
from pathlib import Path


FIXED_ZIP_TIME = (2020, 1, 1, 0, 0, 0)


def build_map(source: Path, rules: Path, output: Path) -> None:
    if output.exists():
        raise FileExistsError(f"output already exists: {output}")

    with zipfile.ZipFile(source) as package:
        names = package.namelist()
        if "map.yaml" not in names or "map.bin" not in names:
            raise ValueError(f"source is not a valid map package: {source}")
        map_yaml = package.read("map.yaml").decode("utf-8")
        if re.search(r"^Rules:", map_yaml, re.MULTILINE):
            raise ValueError("base map unexpectedly defines custom rules")
        map_yaml, substitutions = re.subn(
            r"^Title:\s*.*$", "Title: CNC47 Performance Baseline", map_yaml,
            count=1, flags=re.MULTILINE,
        )
        if substitutions != 1:
            raise ValueError("base map title is missing or ambiguous")
        map_yaml = map_yaml.rstrip() + "\n\nRules: rules.yaml\n"

        output.parent.mkdir(parents=True, exist_ok=True)
        with zipfile.ZipFile(output, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as target:
            for name in names:
                data = map_yaml.encode("utf-8") if name == "map.yaml" else package.read(name)
                info = zipfile.ZipInfo(name, FIXED_ZIP_TIME)
                info.compress_type = zipfile.ZIP_DEFLATED
                info.external_attr = 0o644 << 16
                target.writestr(info, data)
            info = zipfile.ZipInfo("rules.yaml", FIXED_ZIP_TIME)
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o644 << 16
            target.writestr(info, rules.read_bytes())


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", required=True, type=Path)
    parser.add_argument("--rules", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    build_map(args.source.resolve(), args.rules.resolve(), args.output.resolve())
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
