#!/usr/bin/env python3

import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
MODULE_SPEC = importlib.util.spec_from_file_location("launch_ai_parallel", REPO_ROOT / "launch-ai-parallel.py")
parallel = importlib.util.module_from_spec(MODULE_SPEC)
sys.modules[MODULE_SPEC.name] = parallel
MODULE_SPEC.loader.exec_module(parallel)


FAKE_LAUNCHER = r'''#!/usr/bin/env python3
import pathlib
import sys
import time

arguments = dict(argument.split("=", 1) for argument in sys.argv[1:] if "=" in argument)
support = pathlib.Path(arguments["Engine.SupportDir"])
logs = support / "Logs"
logs.mkdir(parents=True, exist_ok=True)
source = pathlib.Path(arguments["Launch.Map"]).read_text(encoding="utf-8")
exit_tick = int(arguments.get("Launch.ExitAtTick", "5000"))
time.sleep(0.15)
debug = logs / "debug.log"
if source == "fail":
    debug.write_text("Fatal error from fake launcher\n", encoding="utf-8")
    raise SystemExit(7)
mode = "paced" if arguments.get("Launch.Paced") == "true" else "headless"
debug.write_text(
    ("Paced rendered automation enabled\n" if mode == "paced" else "Headless MAX automation enabled\n")
    + ("Paced rendered automation started map 'Fake Map' with bots: Fake: bot=viki.\n" if mode == "paced" else "Headless MAX automation started map 'Fake Map' with bots: Fake: bot=viki.\n")
    + ("" if mode == "paced" else "MAX game speed enabled at world tick 0.\n")
    + f"MAX progress: world={exit_tick}, local={exit_tick}, net={exit_tick}, queued-orders=0.\n"
    + (f"Paced rendered automation reached configured exit at world tick {exit_tick}; exiting.\n" if mode == "paced" else f"Headless MAX automation reached configured exit at world tick {exit_tick}; exiting.\n")
    + "Loaded ordinary bot VIKI on isolated map\n",
    encoding="utf-8",
)
(logs / "fake-tick_time.csv").write_text("tick,time [ms]\n", encoding="utf-8")
if "Launch.SaveGameAtTick" in arguments:
    saves = support / "Saves" / "cnc" / "test"
    saves.mkdir(parents=True)
    (saves / arguments["Launch.SaveGameName"]).write_text("save", encoding="utf-8")
'''


class ParallelLauncherTest(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self.content = self.root / "content"
        self.content.mkdir()
        self.launcher = self.root / "fake-launcher.py"
        self.launcher.write_text(FAKE_LAUNCHER, encoding="utf-8")
        self.launcher.chmod(0o755)

    def tearDown(self):
        self.temporary.cleanup()

    def write_manifest(self, runs):
        manifest = self.root / f"manifest-{len(list(self.root.glob('manifest-*.json')))}.json"
        manifest.write_text(json.dumps({"runs": runs}), encoding="utf-8")
        return manifest

    def map(self, name, contents="pass"):
        path = self.root / name
        path.write_text(contents, encoding="utf-8")
        return path

    def test_rejects_duplicate_names_and_non_max_map(self):
        game_map = self.map("map.oramap")
        duplicate = self.write_manifest([
            {"name": "same", "map": str(game_map), "lobby_commands": "option gamespeed max"},
            {"name": "same", "map": str(game_map), "lobby_commands": "option gamespeed max"},
        ])
        with self.assertRaisesRegex(parallel.ConfigurationError, "duplicated"):
            parallel.load_manifest(duplicate, 10)

        non_max = self.write_manifest([
            {"name": "test", "map": str(game_map), "lobby_commands": "option gamespeed fastest"}
        ])
        with self.assertRaisesRegex(parallel.ConfigurationError, "gamespeed max"):
            parallel.load_manifest(non_max, 10)

        paced_max = self.write_manifest([
            {"name": "paced", "mode": "paced", "map": str(game_map), "lobby_commands": "option gamespeed max"}
        ])
        with self.assertRaisesRegex(parallel.ConfigurationError, "gamespeed normal"):
            parallel.load_manifest(paced_max, 10)

    def test_failed_child_does_not_hide_or_stop_healthy_sibling(self):
        passing_map = self.map("passing.oramap")
        failing_map = self.map("failing.oramap", "fail")
        manifest = self.write_manifest([
            {
                "name": "healthy",
                "map": str(passing_map),
                "lobby_commands": "spectate;option gamespeed max",
                "exit_at_tick": 5000,
                "required_log_patterns": ["ordinary bot VIKI", "isolated map"],
                "save_at_tick": 1000,
                "expected_artifacts": ["Saves/**/*.orasav"],
            },
            {
                "name": "broken",
                "map": str(failing_map),
                "lobby_commands": "spectate;option gamespeed max",
                "exit_at_tick": 5000,
            },
        ])
        output = self.root / "output"
        exit_code = parallel.main([
            "--manifest", str(manifest),
            "--output", str(output),
            "--jobs", "2",
            "--launcher", str(self.launcher),
            "--content", str(self.content),
            "--no-xvfb",
            "--poll-interval", "0.02",
            "--progress-interval", "1",
        ])

        self.assertEqual(exit_code, 1)
        summary = json.loads((output / "batch-summary.json").read_text(encoding="utf-8"))
        results = {item["name"]: item for item in summary["runs"]}
        self.assertEqual(results["healthy"]["status"], "passed")
        self.assertEqual(results["healthy"]["maximum_world_tick"], 5000)
        self.assertTrue(results["healthy"]["benchmarks"])
        self.assertTrue(results["healthy"]["saves"])
        self.assertEqual(results["broken"]["status"], "failed")
        self.assertIn("exit code 7", results["broken"]["reasons"])
        self.assertNotEqual(results["healthy"]["support_directory"], results["broken"]["support_directory"])

    def test_game_save_is_staged_in_the_isolated_server_save_directory(self):
        game_save = self.map("checkpoint.orasav", "save-data")
        manifest = self.write_manifest([{
            "name": "load",
            "game_save": str(game_save),
            "exit_at_tick": 7000,
        }])
        _, specs = parallel.load_manifest(manifest, 10)
        output = self.root / "save-output"
        output.mkdir()
        _, support, runtime_input, command = parallel.prepare_run(
            specs[0], output, self.content, None, self.launcher, "test-version", 90, True
        )

        self.assertEqual(
            runtime_input,
            support / "Saves" / "cnc" / "test-version" / "input.orasav",
        )
        self.assertEqual(runtime_input.read_text(encoding="utf-8"), "save-data")
        self.assertIn(f"Launch.GameSave={runtime_input}", command)

    def test_same_workloads_run_serially_and_concurrently(self):
        game_maps = [self.map(f"map-{index}.oramap") for index in range(3)]
        runs = [
            {
                "name": f"run-{index}",
                "map": str(game_map),
                "lobby_commands": "option gamespeed max",
                "exit_at_tick": 6000 + index,
            }
            for index, game_map in enumerate(game_maps)
        ]
        manifest = self.write_manifest(runs)
        durations = []
        for jobs in (1, 3):
            output = self.root / f"output-{jobs}"
            exit_code = parallel.main([
                "--manifest", str(manifest), "--output", str(output), "--jobs", str(jobs),
                "--launcher", str(self.launcher), "--content", str(self.content), "--no-xvfb",
                "--poll-interval", "0.02", "--progress-interval", "1",
            ])
            self.assertEqual(exit_code, 0)
            summary = json.loads((output / "batch-summary.json").read_text(encoding="utf-8"))
            self.assertEqual(summary["passed"], 3)
            durations.append(summary["duration_seconds"])
        self.assertLess(durations[1], durations[0])

    def test_paced_run_uses_rendered_automation_without_max_marker(self):
        game_map = self.map("paced.oramap")
        manifest = self.write_manifest([{
            "name": "paced", "mode": "paced", "map": str(game_map),
            "lobby_commands": "option gamespeed normal", "exit_at_tick": 100,
        }])
        output = self.root / "paced-output"
        exit_code = parallel.main([
            "--manifest", str(manifest), "--output", str(output), "--jobs", "1",
            "--launcher", str(self.launcher), "--content", str(self.content), "--no-xvfb",
            "--poll-interval", "0.02", "--progress-interval", "1",
        ])
        self.assertEqual(exit_code, 0)
        command = json.loads((output / "paced" / "command.json").read_text(encoding="utf-8"))
        self.assertIn("Launch.Paced=true", command)
        self.assertNotIn("Launch.Headless=true", command)


if __name__ == "__main__":
    unittest.main()
