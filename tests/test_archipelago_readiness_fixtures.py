#!/usr/bin/env python3

import hashlib
import importlib.util
import tempfile
import unittest
import zipfile
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
MODULE_SPEC = importlib.util.spec_from_file_location(
    "build_archipelago_readiness_fixtures",
    REPO_ROOT / "build-archipelago-readiness-fixtures.py",
)
fixtures = importlib.util.module_from_spec(MODULE_SPEC)
MODULE_SPEC.loader.exec_module(fixtures)


class ArchipelagoReadinessFixtureTest(unittest.TestCase):
    def test_archives_are_deterministic_and_rules_own_lua(self):
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary)
            stock = REPO_ROOT / "mods/cnc/maps/archipelago"
            source = REPO_ROOT / "tests/fixtures/cnc89"
            hashes = []
            for attempt in range(2):
                target = output / str(attempt) / "valid.oramap"
                fixtures.build_fixture(stock, source, target, "valid")
                hashes.append(hashlib.sha256(target.read_bytes()).hexdigest())

                with zipfile.ZipFile(target) as archive:
                    self.assertEqual(
                        sorted(archive.namelist()),
                        ["cnc89-valid.lua", "map.bin", "map.png", "map.yaml", "rules.yaml"],
                    )
                    map_yaml = archive.read("map.yaml").decode("utf-8")
                    rules_yaml = archive.read("rules.yaml").decode("utf-8")
                    self.assertIn("\nRules: rules.yaml\n", map_yaml)
                    self.assertNotIn("\nWorld:", map_yaml)
                    self.assertIn("PlayerReference@CNC89Fixture", map_yaml)
                    self.assertIn("World:\n\tLuaScript:\n\t\tScripts: cnc89-valid.lua", rules_yaml)
                    self.assertTrue(all(info.date_time == fixtures.ARCHIVE_TIMESTAMP for info in archive.infolist()))

            self.assertEqual(hashes[0], hashes[1])

    def test_malformed_fixture_is_loadable_but_emits_ready_without_build_evidence(self):
        entries = fixtures.fixture_entries(
            REPO_ROOT / "mods/cnc/maps/archipelago",
            REPO_ROOT / "tests/fixtures/cnc89",
            "premature",
        )
        script = entries["cnc89-premature.lua"].decode("utf-8")
        self.assertIn("CNC89 ACTOR", script)
        self.assertIn("CNC89 READY", script)
        self.assertNotIn("CNC89 BUILD", script)

    def test_fatal_fixture_fails_after_world_progress_but_before_readiness(self):
        entries = fixtures.fixture_entries(
            REPO_ROOT / "mods/cnc/maps/archipelago",
            REPO_ROOT / "tests/fixtures/cnc89",
            "fatal",
        )
        script = entries["cnc89-fatal.lua"].decode("utf-8")
        self.assertIn("CNC89 ACTOR", script)
        self.assertIn("Trigger.AfterDelay(1250", script)
        self.assertIn(
            "error(\"CNC89 deliberate fatal before readiness tick=\" .. DateTime.GameTime)",
            script,
        )
        self.assertNotIn("CNC89 READY", script)

    def test_duplicate_fixture_emits_ordered_ready_then_delayed_duplicate(self):
        entries = fixtures.fixture_entries(
            REPO_ROOT / "mods/cnc/maps/archipelago",
            REPO_ROOT / "tests/fixtures/cnc89",
            "duplicate",
        )
        script = entries["cnc89-duplicate.lua"].decode("utf-8")
        self.assertIn("CNC89 ACTOR", script)
        self.assertIn("CNC89 BUILD", script)
        self.assertEqual(script.count("CNC89 READY"), 2)
        self.assertIn("Trigger.AfterDelay(1250", script)


if __name__ == "__main__":
    unittest.main()
