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
        self.assertNotIn("chemical:", result)
        self.assertNotIn("UnitTypes: ctnk", result)


if __name__ == "__main__":
    unittest.main()
