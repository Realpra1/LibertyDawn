#!/usr/bin/env python3

import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace


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
commands = arguments.get("Launch.LobbyCommands", "option gamespeed max")
speed = next(part.rsplit(" ", 1)[-1] for part in commands.split(";") if part.startswith("option gamespeed "))
speeds = {
    "default": ("Normal", 40, False, "paced"),
    "fastest": ("Fastest", 20, False, "paced"),
    "max": ("MAX", 20, True, "MAX"),
}
speed_name, timestep, maximum, label = speeds[speed]
time.sleep(0.15)
debug = logs / "debug.log"
if source == "fail":
    debug.write_text("Fatal error from fake launcher\n", encoding="utf-8")
    raise SystemExit(7)
debug.write_text(
    "Headless automation enabled\n"
    f"Headless {label} automation started map 'Fake Map' with bots: Fake: bot=viki.\n"
    f"Headless automation accepted gamespeed key={speed}, name={speed_name}, timestep={timestep}, maximum={maximum}.\n"
    f"MAX progress: world={exit_tick}, local={exit_tick}, net={exit_tick}, queued-orders=0.\n"
    f"Headless {label} automation reached configured exit at world tick {exit_tick}; exiting.\n"
    "Loaded ordinary bot VIKI on isolated map\n",
    encoding="utf-8",
)
(logs / f"{arguments['Launch.Benchmark']}tick_time.csv").write_text(
    f"tick,time [ms]\n{exit_tick},1\n", encoding="utf-8"
)
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

    def test_rejects_duplicate_names_and_unsupported_map_speed(self):
        game_map = self.map("map.oramap")
        duplicate = self.write_manifest([
            {"name": "same", "map": str(game_map), "lobby_commands": "option gamespeed max"},
            {"name": "same", "map": str(game_map), "lobby_commands": "option gamespeed max"},
        ])
        with self.assertRaisesRegex(parallel.ConfigurationError, "duplicated"):
            parallel.load_manifest(duplicate, 10)

        unsupported = self.write_manifest([
            {"name": "test", "map": str(game_map), "lobby_commands": "option gamespeed fast"}
        ])
        with self.assertRaisesRegex(parallel.ConfigurationError, "exactly one"):
            parallel.load_manifest(unsupported, 10)

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

    def measurement_run(self, ticks, under_floor=False):
        game_map = self.map(f"measurement-{len(list(self.root.glob('measurement-*')))}.oramap")
        manifest = self.write_manifest([{
            "name": "measured",
            "map": str(game_map),
            "lobby_commands": "option gamespeed default",
            "exit_at_tick": 3000,
            "measurement": {
                "warmup_tick": 500,
                "measurement_ticks": 2500,
                "sample_interval": 100,
                "minimum_bots": 5,
                "minimum_live_mobile": 300,
                "expected_short_game": False,
                "expected_starting_cash": 20000,
                "expected_players": [
                    {
                        "player": f"Multi{player}",
                        "bot_type": "skynet" if player < 2 else "brutalis",
                        "faction": "gdi" if player % 2 == 0 else "nod",
                        "team": 1 if player < 3 else 2,
                        "spawn": player + 1,
                    }
                    for player in range(5)
                ],
            },
        }])
        _, specs = parallel.load_manifest(manifest, 10)
        support = self.root / f"measurement-support-{len(list(self.root.glob('measurement-support-*')))}"
        logs = support / "Logs"
        logs.mkdir(parents=True)
        header = (
            "world_tick,local_tick,elapsed_ms,warmup_elapsed_ms,total_live_actors,total_effects,"
            "player,bot_type,faction,team,spawn,live_mobile,queued,moving,busy,orders,cash,"
            "resources,earned,spent,units_killed,units_dead"
        )
        rows = [header]
        for tick in ticks:
            for player in range(5):
                live = 299 if under_floor and tick == 600 and player == 4 else 300 + player
                rows.append(",".join(str(value) for value in (
                    tick, tick, (tick - 500) * 40, 12345.5, 1700, 1,
                    f"Multi{player}", "skynet" if player < 2 else "brutalis",
                    "gdi" if player % 2 == 0 else "nod", 1 if player < 3 else 2,
                    player + 1, live, 1, 0 if tick == 500 else 1, 1,
                    tick // 100, 10000, 0, tick, tick, 1 if tick > 500 else 0, 0,
                )))
        (logs / "performance-baseline.csv").write_text("\n".join(rows) + "\n", encoding="utf-8")
        return SimpleNamespace(spec=specs[0], support_dir=support)

    def test_measurement_requires_exact_window_and_each_bot_floor(self):
        ticks = list(range(500, 3001, 100))
        run = self.measurement_run(ticks)
        summary, reasons = parallel.analyze_measurement(run)

        self.assertEqual(reasons, [])
        self.assertTrue(summary["complete_window"])
        self.assertEqual(summary["start_world_tick"], 500)
        self.assertEqual(summary["end_world_tick"], 3000)
        self.assertEqual(summary["warmup_wall_milliseconds"], 12345.5)
        self.assertEqual(summary["real_game_time_ratio"], 1)
        self.assertEqual(summary["players"]["Multi0"]["live_mobile_min"], 300)

        under_floor = self.measurement_run(ticks, under_floor=True)
        _, reasons = parallel.analyze_measurement(under_floor)
        self.assertIn("Multi4 live-mobile count 299 below 300 at tick 600", reasons)

    def test_incomplete_measurement_reports_observed_not_configured_end(self):
        run = self.measurement_run([500, 600, 700])
        summary, reasons = parallel.analyze_measurement(run)

        self.assertIn("measurement samples do not cover the exact configured tick window", reasons)
        self.assertFalse(summary["complete_window"])
        self.assertEqual(summary["end_world_tick"], 700)
        self.assertEqual(summary["configured_end_world_tick"], 3000)
        self.assertIsNone(summary["real_game_time_ratio"])

    def test_benchmark_summary_uses_true_median_and_rejects_absent_streams(self):
        run = self.measurement_run([500, 600, 700])
        benchmark_files = []
        for stream in ("tick_time", "tick_actors", "bot_tick"):
            relative = f"Logs/measured-{stream}.csv"
            (run.support_dir / relative).write_text(
                "tick,time [ms]\n500,1\n600,2\n700,10\n800,20\n", encoding="utf-8"
            )
            benchmark_files.append(relative)

        summary, reasons = parallel.summarize_benchmarks(run, benchmark_files, 500, 800)
        self.assertEqual(reasons, [])
        self.assertEqual(summary["tick_time"]["median_ms"], 6)
        self.assertEqual(summary["tick_time"]["p95_ms"], 20)

        _, reasons = parallel.summarize_benchmarks(run, benchmark_files[:1], 500, 800)
        self.assertIn("required benchmark stream tick_actors missing", reasons)
        self.assertIn("required benchmark stream bot_tick missing", reasons)

    def test_measurement_csv_does_not_satisfy_engine_benchmark_gate(self):
        run = self.measurement_run([500, 600])
        self.assertEqual(parallel.engine_benchmark_files(run), [])

        benchmark = run.support_dir / "Logs" / "measured-tick_time.csv"
        benchmark.write_text("tick,time [ms]\n500,1\n", encoding="utf-8")
        self.assertEqual(parallel.engine_benchmark_files(run), ["Logs/measured-tick_time.csv"])

    def test_profile_summary_is_bounded_and_ranked_by_cumulative_time(self):
        run = self.measurement_run([500, 600])
        run.spec.profile = {
            "kind": "simulation_perf_log",
            "long_tick_threshold_ms": 100,
            "max_bytes": 1024,
            "top": 2,
        }
        (run.support_dir / "Logs" / "perf.log").write_text(
            "  125 ms [700] Trait tick: Foo\n"
            "  200 ms [701] Trait tick: Foo\n"
            "  300 ms [702] Activity tick: Bar\n",
            encoding="utf-8",
        )

        summary, reasons = parallel.summarize_profile(run)

        self.assertEqual(reasons, [])
        self.assertEqual(summary["event_count"], 3)
        self.assertEqual(summary["max_bytes"], 1024)
        self.assertEqual(summary["hotspots"][0]["label"], "Trait tick: Foo")
        self.assertEqual(summary["hotspots"][0]["total_ms"], 325)
        self.assertEqual(summary["hotspots"][1]["label"], "Activity tick: Bar")

    def test_effective_lobby_identity_is_recorded_and_must_match_request(self):
        run = self.measurement_run([500, 600])
        players = ";".join(
            f"Multi{player}|{'skynet' if player < 2 else 'brutalis'}|"
            f"{'gdi' if player % 2 == 0 else 'nod'}|{1 if player < 3 else 2}|{player + 1}"
            for player in range(5)
        )
        text = (
            "Performance baseline accepted lobby identity: "
            f"shortgame=False, startingcash=20000, bots={players}."
        )
        effective, reasons = parallel.analyze_effective_lobby(run, text)

        self.assertEqual(reasons, [])
        self.assertFalse(effective["short_game"])
        self.assertEqual(effective["starting_cash"], 20000)
        self.assertEqual(len(effective["players"]), 5)

        _, mismatch_reasons = parallel.analyze_effective_lobby(
            run, text.replace("shortgame=False", "shortgame=True")
        )
        self.assertRegex(mismatch_reasons[0], "does not match requested")


if __name__ == "__main__":
    unittest.main()
