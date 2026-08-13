#!/usr/bin/env python3
"""Build deterministic CNC-89 readiness fixtures from the stock Archipelago map."""

from __future__ import annotations

import argparse
import zipfile
from pathlib import Path


ARCHIVE_TIMESTAMP = (1980, 1, 1, 0, 0, 0)
STOCK_ENTRIES = ("map.bin", "map.png")


def archive_info(name: str) -> zipfile.ZipInfo:
    info = zipfile.ZipInfo(name, ARCHIVE_TIMESTAMP)
    info.compress_type = zipfile.ZIP_DEFLATED
    info.external_attr = 0o100644 << 16
    return info


def fixture_entries(stock_map: Path, fixture_source: Path, variant: str) -> dict[str, bytes]:
    map_yaml = (stock_map / "map.yaml").read_text(encoding="utf-8")
    if "\nRules:" in map_yaml or "\nWorld:" in map_yaml:
        raise ValueError("Stock Archipelago unexpectedly owns rules or World data.")

    fixture_player = (
        "\tPlayerReference@CNC89Fixture:\n"
        "\t\tName: CNC89Fixture\n"
        "\t\tNonCombatant: True\n"
        "\t\tFaction: gdi\n"
    )
    if "\nActors:\n" not in map_yaml:
        raise ValueError("Stock Archipelago has no Actors section.")
    map_yaml = map_yaml.replace("\nActors:\n", "\n" + fixture_player + "\nActors:\n", 1)
    script_name = f"cnc89-{variant}.lua"
    map_yaml = map_yaml.rstrip() + "\n\nRules: rules.yaml\n"
    rules_yaml = "World:\n\tLuaScript:\n\t\tScripts: " + script_name + "\n"
    entries = {
        "map.yaml": map_yaml.encode("utf-8"),
        "rules.yaml": rules_yaml.encode("utf-8"),
        script_name: (fixture_source / script_name).read_bytes(),
    }
    entries.update({name: (stock_map / name).read_bytes() for name in STOCK_ENTRIES})
    return entries


def build_fixture(stock_map: Path, fixture_source: Path, output: Path, variant: str) -> None:
    output.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(output, "w") as archive:
        for name, contents in sorted(fixture_entries(stock_map, fixture_source, variant).items()):
            archive.writestr(archive_info(name), contents)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    root = Path(__file__).resolve().parent
    parser.add_argument(
        "--stock-map", type=Path, default=root / "mods/cnc/maps/archipelago",
    )
    parser.add_argument(
        "--fixture-source", type=Path, default=root / "tests/fixtures/cnc89",
    )
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    for variant in ("valid", "premature"):
        build_fixture(
            args.stock_map.resolve(), args.fixture_source.resolve(),
            args.output.resolve() / f"archipelago-cnc89-{variant}.oramap", variant,
        )


if __name__ == "__main__":
    main()
