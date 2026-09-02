#!/usr/bin/env python3

import importlib.util
import sys
import tempfile
import unittest
import zipfile
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
MODULE_SPEC = importlib.util.spec_from_file_location(
    "benchmark_cnc_ai_modules", REPO_ROOT / "benchmark-cnc-ai-modules.py",
)
benchmark = importlib.util.module_from_spec(MODULE_SPEC)
sys.modules[MODULE_SPEC.name] = benchmark
MODULE_SPEC.loader.exec_module(benchmark)


class CncAiModuleBenchmarkTest(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)

    def tearDown(self):
        self.temporary.cleanup()

    def test_baseline_map_only_adds_the_explicit_module_override(self):
        source = self.root / "source.oramap"
        with zipfile.ZipFile(source, "w") as archive:
            archive.writestr("map.yaml", "MapFormat: 11\nTitle: Empire Earth4\n")
            archive.writestr("map.bin", b"map")

        destination = self.root / "baseline.oramap"
        benchmark.create_baseline_map(source, destination)

        with zipfile.ZipFile(destination, "r") as archive:
            self.assertEqual(archive.read("map.bin"), b"map")
            self.assertIn(
                "Rules: benchmark-baseline.yaml",
                archive.read("map.yaml").decode("utf-8"),
            )
            rules = archive.read("benchmark-baseline.yaml").decode("utf-8")
            self.assertEqual(rules.count("AdvancedSquadModulesInitiallyDisabled: true"), 4)
            self.assertEqual(rules.count("SimpleAttackMoveFallbackWhenDisabled: true"), 4)
            self.assertEqual(rules.count("FailsafeReconsiderInterval: 750"), 4)
            self.assertEqual(rules.count("heli, orca"), 4)
            for controller in (
                "TransportManagerBotModule", "CrateCollectorBotModule",
                "CrateCollectorBotModule@VIKI", "RedTiberiumBombBotModule",
                "OpeningGarrisonBotModule", "CaptureManagerBotModule", "SpecialOrderBotModule",
            ):
                self.assertIn(f"-{controller}:", rules)
            for bot in ("Brutalis", "VIKI", "SkyNet", "IronReaper"):
                self.assertIn(f"ModularBot@{bot}:", rules)

    def test_manifest_uses_fixed_corner_spawns_and_matched_seeds(self):
        manifest = benchmark.phase_manifest(
            "baseline", self.root / "map.oramap", 5, 1234, 100, True,
        )

        commands = manifest["defaults"]["lobby_commands"]
        for command in (
            "slot_bot Multi0 0 skynet 1 1",
            "slot_bot Multi1 0 viki 2 6",
            "slot_bot Multi2 0 brutalis 3 34",
            "slot_bot Multi3 0 ironreaper 4 35",
        ):
            self.assertIn(command, commands)
        self.assertEqual([run["seed"] for run in manifest["runs"]], list(range(1234, 1239)))
        self.assertNotIn("exit_at_tick", manifest["defaults"])
        self.assertNotIn("minimum_world_tick", manifest["defaults"])
        self.assertFalse(manifest["defaults"]["bot_debug"])
        self.assertIn(
            r"Failed to load rules", manifest["defaults"]["forbidden_log_patterns"],
        )
        self.assertTrue(all(
            "initially-disabled=True" in pattern
            for pattern in manifest["defaults"]["required_log_patterns"][-4:]
        ))
        self.assertFalse(any(
            "transition=disabled" in pattern
            for pattern in manifest["defaults"]["forbidden_log_patterns"]
        ))

        full = benchmark.phase_manifest(
            "full", self.root / "map.oramap", 5, 1234, 100, False,
        )
        self.assertTrue(any(
            "transition=disabled" in pattern
            for pattern in full["defaults"]["forbidden_log_patterns"]
        ))

    def test_periodic_report_exposes_cpu_tick_and_module_cost(self):
        report = self.root / "periodic.tsv"
        report.write_text(
            "distribution\ttick\tcount=10000\tmean_ms=2.500\tp50_ms=2\n"
            "runtime\ttick\tcpu_ms\tworking_set\n"
            "runtime\t1\t100.000\t0\n"
            "runtime\t10000\t20100.000\t0\n"
            "module\tidentity\tcalls\ttotal_ms\tmax_ms\tqueued_orders\n"
            "module\tplayer-1/SquadManagerBotModule\t10000\t1500.000\t1.000\t10\n"
            "module\tplayer-1/StealthSquad/strategy\t100\t900.000\t1.000\t10\n"
            "module\tplayer-2/SquadManagerBotModule\t10000\t2500.000\t1.000\t10\n",
            encoding="utf-8",
        )

        metrics = benchmark.periodic_metrics(report)

        self.assertEqual(metrics["cpu_seconds"], 20)
        self.assertEqual(metrics["tick_mean_milliseconds"], 2.5)
        self.assertEqual(metrics["module_cpu_seconds"], 4)

    def test_comparison_reports_overhead_and_realtime_gate(self):
        baseline = {"averages": {
            "wall_seconds": 100,
            "game_seconds": 400,
            "game_seconds_per_wall_second": 4,
            "cpu_seconds": 90,
            "cpu_seconds_per_1000_ticks": 0.9,
            "tick_mean_milliseconds": 2,
            "module_cpu_seconds": 10,
            "module_cpu_seconds_per_1000_ticks": 1,
        }}
        full = {"averages": {
            "wall_seconds": 125,
            "game_seconds": 500,
            "cpu_seconds": 108,
            "cpu_seconds_per_1000_ticks": 1.08,
            "tick_mean_milliseconds": 3,
            "module_cpu_seconds": 15,
            "module_cpu_seconds_per_1000_ticks": 1.25,
            "game_seconds_per_wall_second": 3.2,
        }}

        result = benchmark.comparison(baseline, full)

        self.assertAlmostEqual(result["wall_time_change_percent"], 25)
        self.assertAlmostEqual(result["game_time_change_percent"], 25)
        self.assertAlmostEqual(result["simulation_throughput_change_percent"], -20)
        self.assertAlmostEqual(result["cpu_time_change_percent"], 20)
        self.assertAlmostEqual(result["normalized_cpu_time_change_percent"], 20)
        self.assertAlmostEqual(result["mean_tick_time_change_percent"], 50)
        self.assertAlmostEqual(result["module_cpu_time_change_percent"], 50)
        self.assertAlmostEqual(result["normalized_module_cpu_change_percent"], 25)
        self.assertTrue(result["full_faster_than_wall_clock"])


if __name__ == "__main__":
    unittest.main()
