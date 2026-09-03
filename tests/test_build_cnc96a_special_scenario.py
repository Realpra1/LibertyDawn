#!/usr/bin/env python3

import importlib.util
import struct
import sys
import tempfile
import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
MODULE_SPEC = importlib.util.spec_from_file_location(
    "build_cnc96a_special_scenario", REPO_ROOT / "tests/build_cnc96a_special_scenario.py",
)
scenario = importlib.util.module_from_spec(MODULE_SPEC)
sys.modules[MODULE_SPEC.name] = scenario
MODULE_SPEC.loader.exec_module(scenario)


class Cnc96aSpecialScenarioTest(unittest.TestCase):
    def test_fixture_disables_production_but_keeps_combat_managers(self):
        self.assertIn("-CrateSpawner:", scenario.RULES_HEADER)
        self.assertIn("-UnitBuilderBotModule@viki:", scenario.RULES_HEADER)
        self.assertIn("-UnitBuilderBotModule@brutalis:", scenario.RULES_HEADER)
        self.assertIn("-BaseBuilderBotModule@viki:", scenario.RULES_HEADER)
        self.assertIn("-BaseBuilderBotModule@brutalis:", scenario.RULES_HEADER)
        self.assertNotIn("-SquadManagerBotModule@brutalis:", scenario.RULES_HEADER)

    def test_dense_compound_is_closed_and_requires_its_center_structure(self):
        actors = scenario.dense_compound_actors()

        self.assertEqual(actors.count(": brik\n"), 168)
        self.assertEqual(actors.count(": e3\n"), 4)
        self.assertIn("CompoundPower: nuk2", actors)
        for x in range(68, 96):
            for y in (37, 38, 53, 54):
                self.assertIn(f"Location: {x},{y}", actors)
        for y in range(39, 53):
            for x in (68, 69, 94, 95):
                self.assertIn(f"Location: {x},{y}", actors)

    def test_resource_layer_is_cleared_without_touching_tiles(self):
        width, height = 2, 2
        tiles = bytes(range(12))
        resources = bytes([1, 2] * width * height)
        header = struct.pack("<BHHIII", 2, width, height, 17, 0, 17 + len(tiles))

        result = scenario.without_resources(header + tiles + resources)

        self.assertEqual(result[:17 + len(tiles)], header + tiles)
        self.assertEqual(result[17 + len(tiles):], bytes(len(resources)))

    def test_viki_override_preserves_manager_config_and_removes_chemical_profile(self):
        source = """Player:
    SquadManagerBotModule@viki:
\t\tUseModularStealthLifecycle: true
\t\tStealthSquadDefinitions:
\t\t\tstealth-tank:
\t\t\t\tClaimAllEligible: true
\t\t\tchemical:
\t\t\t\tUnitTypes: ctnk
\t\tRequiresCondition: enable-viki-ai
\t\tGroundTargetDebugLogging: false
\tUnitBuilderBotModule@viki:
"""
        with tempfile.TemporaryDirectory() as temporary:
            ai_rules = Path(temporary) / "ai.yaml"
            ai_rules.write_text(source, encoding="utf-8")

            result = scenario.viki_manager_override(ai_rules)

        self.assertIn("SquadManagerBotModule@cnc96a:", result)
        self.assertIn("MaximumHarassmentGroups: 3", result)
        self.assertIn("ReserveOpeningPair: false", result)
        self.assertIn("RequiresCondition: enable-viki-ai", result)
        self.assertIn("GroundTargetDebugLogging: false", result)
        self.assertNotIn("chemical:", result)
        self.assertNotIn("UnitTypes: ctnk", result)


if __name__ == "__main__":
    unittest.main()
