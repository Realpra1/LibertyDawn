#!/usr/bin/env python3
"""Build the reproducible CNC-96A four-squad human-review map."""

from __future__ import annotations

import argparse
import re
import struct
import zipfile
from pathlib import Path


RULES_HEADER = """World:
\t-SpawnStartingUnits:
\t-CrateSpawner:

Player:
\t-SquadManagerBotModule@viki:
\t-BaseBuilderBotModule@viki:
\t-UnitBuilderBotModule@viki:
\t-BaseBuilderBotModule@brutalis:
\t-UnitBuilderBotModule@brutalis:
"""

RULES_FOOTER = """
cnc96a-viki-anchor:
\tInherits: NUKE
\tRenderSprites:
\t\tImage: nuke
\t-Targetable:
\tHealth:
\t\tHP: 100000000
"""

ACTORS = """Actors:
\tSpawn1: mpspawn
\t\tOwner: Neutral
\t\tLocation: 20,37
\tSpawn2: mpspawn
\t\tOwner: Neutral
\t\tLocation: 77,20
\tVikiAnchor: cnc96a-viki-anchor
\t\tOwner: Multi0
\t\tLocation: 2,54
\tVikiStank1: stnk
\t\tOwner: Multi0
\t\tLocation: 17,34
\tVikiStank2: stnk
\t\tOwner: Multi0
\t\tLocation: 19,34
\tVikiStank3: stnk
\t\tOwner: Multi0
\t\tLocation: 21,36
\tVikiStank4: stnk
\t\tOwner: Multi0
\t\tLocation: 23,36
\tVikiStank5: stnk
\t\tOwner: Multi0
\t\tLocation: 17,40
\tVikiStank6: stnk
\t\tOwner: Multi0
\t\tLocation: 19,40
\tVikiStank7: stnk
\t\tOwner: Multi0
\t\tLocation: 21,42
\tVikiStank8: stnk
\t\tOwner: Multi0
\t\tLocation: 23,42
\tBrutalisPower1: nuk2
\t\tOwner: Multi1
\t\tLocation: 84,10
\tBrutalisPower2: nuk2
\t\tOwner: Multi1
\t\tLocation: 89,10
\tBrutalisHq: hq
\t\tOwner: Multi1
\t\tLocation: 78,10
\tBrutalisRefinery: proc
\t\tOwner: Multi1
\t\tLocation: 83,19
\tBrutalisFactory: weap
\t\tOwner: Multi1
\t\tLocation: 86,29
\tBrutalisObelisk1: obli
\t\tOwner: Multi1
\t\tLocation: 92,17
\tBrutalisObelisk2: obli
\t\tOwner: Multi1
\t\tLocation: 92,32
\tBrutalisHarvester1: harv
\t\tOwner: Multi1
\t\tLocation: 78,21
\tBrutalisHarvester2: harv
\t\tOwner: Multi1
\t\tLocation: 80,25
\tBrutalisMammoth1: htnk
\t\tOwner: Multi1
\t\tLocation: 65,19
\tBrutalisMammoth2: htnk
\t\tOwner: Multi1
\t\tLocation: 65,24
\tBrutalisMedium: mtnk
\t\tOwner: Multi1
\t\tLocation: 68,21
\tBrutalisArtillery1: arty
\t\tOwner: Multi1
\t\tLocation: 62,17
\tBrutalisArtillery2: arty
\t\tOwner: Multi1
\t\tLocation: 62,26
\tBrutalisRifle1: e1
\t\tOwner: Multi1
\t\tLocation: 57,17
\tBrutalisRifle2: e1
\t\tOwner: Multi1
\t\tLocation: 58,19
\tBrutalisRifle3: e1
\t\tOwner: Multi1
\t\tLocation: 57,22
\tBrutalisRifle4: e1
\t\tOwner: Multi1
\t\tLocation: 58,24
\tBrutalisRifle5: e1
\t\tOwner: Multi1
\t\tLocation: 57,27
\tBrutalisRifle6: e1
\t\tOwner: Multi1
\t\tLocation: 59,29
\tBrutalisRocket1: e3
\t\tOwner: Multi1
\t\tLocation: 60,18
\tBrutalisRocket2: e3
\t\tOwner: Multi1
\t\tLocation: 60,21
\tBrutalisRocket3: e3
\t\tOwner: Multi1
\t\tLocation: 60,24
\tBrutalisRocket4: e3
\t\tOwner: Multi1
\t\tLocation: 60,27
"""


def dense_compound_actors() -> str:
    """A two-wall-deep local-combat fixture that must be breached to finish."""
    actors = [
        ("CompoundPower", "nuk2", 81, 45),
        ("CompoundObelisk1", "obli", 77, 41),
        ("CompoundObelisk2", "obli", 86, 41),
        ("CompoundObelisk3", "obli", 77, 50),
        ("CompoundObelisk4", "obli", 86, 50),
        ("CompoundMammoth1", "htnk", 79, 43),
        ("CompoundMammoth2", "htnk", 84, 48),
        ("CompoundMedium1", "mtnk", 84, 43),
        ("CompoundMedium2", "mtnk", 79, 48),
        ("CompoundRifle1", "e1", 80, 41),
        ("CompoundRifle2", "e1", 83, 41),
        ("CompoundRifle3", "e1", 80, 50),
        ("CompoundRifle4", "e1", 83, 50),
        ("CompoundRocket1", "e3", 79, 45),
        ("CompoundRocket2", "e3", 84, 45),
        ("CompoundRocket3", "e3", 79, 46),
        ("CompoundRocket4", "e3", 84, 46),
    ]
    # Keep the required center farther than the stealth tank's eight-cell range
    # from every exterior cell. The ordinary priority-1 wall fallback must open
    # the otherwise sealed route after safer local targets have been cleared.
    perimeter = [(x, y) for x in range(68, 96) for y in (37, 38, 53, 54)]
    perimeter += [(x, y) for x in (68, 69, 94, 95) for y in range(39, 53)]
    actors += [(f"CompoundWall{index}", "brik", x, y)
               for index, (x, y) in enumerate(perimeter, 1)]
    return "".join(
        f"\t{name}: {actor_type}\n\t\tOwner: Multi1\n\t\tLocation: {x},{y}\n"
        for name, actor_type, x, y in actors
    )


def viki_manager_override(ai_rules: Path) -> str:
    lines = ai_rules.read_text(encoding="utf-8").splitlines(keepends=True)
    start = next(i for i, line in enumerate(lines) if line.strip() == "SquadManagerBotModule@viki:")
    end = next(i for i in range(start + 1, len(lines)) if lines[i].strip() == "UnitBuilderBotModule@viki:")
    block = lines[start:end]
    block[0] = block[0].replace("SquadManagerBotModule@viki:", "SquadManagerBotModule@cnc96a:")

    chemical = next(i for i, line in enumerate(block) if line.strip() == "chemical:")
    chemical_indent = len(block[chemical]) - len(block[chemical].lstrip())
    chemical_end = next(i for i in range(chemical + 1, len(block))
                        if block[i].strip() and len(block[i]) - len(block[i].lstrip()) < chemical_indent)
    del block[chemical:chemical_end]

    claim = next(i for i, line in enumerate(block) if line.strip() == "ClaimAllEligible: true")
    indent = block[claim][:-len(block[claim].lstrip())]
    block[claim + 1:claim + 1] = [
        f"{indent}MaximumHarassmentGroups: 3\n",
        f"{indent}ReserveOpeningPair: false\n",
    ]
    return "".join(block)


def without_resources(data: bytes) -> bytes:
    result = bytearray(data)
    format_version, width, height = struct.unpack_from("<BHH", result)
    if format_version == 1:
        resources_offset = 5 + 3 * width * height
    elif format_version == 2:
        resources_offset = struct.unpack_from("<I", result, 13)[0]
    else:
        raise ValueError(f"unsupported map binary format {format_version}")

    resources_end = resources_offset + 2 * width * height
    if resources_offset == 0 or resources_end > len(result):
        raise ValueError("map binary has an invalid resource layer")
    result[resources_offset:resources_end] = bytes(resources_end - resources_offset)
    return bytes(result)


def build(source: Path, ai_rules: Path, output: Path) -> None:
    rules = RULES_HEADER + viki_manager_override(ai_rules) + RULES_FOOTER
    with zipfile.ZipFile(source, "r") as archive:
        map_yaml = archive.read("map.yaml").decode("utf-8")
        map_yaml = re.sub(r"^Title:.*$", "Title: StankChallenge", map_yaml, count=1, flags=re.MULTILINE)
        map_yaml = re.sub(r"^Actors:\n.*\Z", ACTORS + dense_compound_actors(), map_yaml,
                          count=1, flags=re.MULTILINE | re.DOTALL)
        map_yaml = re.sub(r"\nRules: cnc96a-special-rules\.yaml\n?", "\n", map_yaml)
        map_yaml = map_yaml.rstrip() + "\n\nRules: cnc96a-special-rules.yaml\n"
        with zipfile.ZipFile(output, "w", zipfile.ZIP_DEFLATED) as generated:
            for item in archive.infolist():
                if item.filename == "cnc96a-special-rules.yaml":
                    continue
                data = archive.read(item.filename)
                if item.filename == "map.yaml":
                    data = map_yaml.encode("utf-8")
                elif item.filename == "map.bin":
                    data = without_resources(data)
                generated.writestr(item, data)
            rules_info = zipfile.ZipInfo("cnc96a-special-rules.yaml", (1980, 1, 1, 0, 0, 0))
            rules_info.compress_type = zipfile.ZIP_DEFLATED
            generated.writestr(rules_info, rules)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", required=True, type=Path)
    parser.add_argument("--ai-rules", default=Path("mods/cnc/rules/ai.yaml"), type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    args.output.parent.mkdir(parents=True, exist_ok=True)
    build(args.source.resolve(), args.ai_rules.resolve(), args.output.resolve())


if __name__ == "__main__":
    main()
