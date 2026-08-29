import importlib.util
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("score_stealth_replay", ROOT / "score_stealth_replay.py")
SCORER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SCORER)


class ScoreStealthReplayTest(unittest.TestCase):
	def test_version_compatible_runtime_does_not_modify_source_manifest(self):
		with tempfile.TemporaryDirectory() as directory:
			repo = Path(directory) / "repo"
			(repo / "mods/cnc").mkdir(parents=True)
			source = repo / "mods/cnc/mod.yaml"
			source.write_text("Metadata:\n\tVersion: {DEV_VERSION}\n", encoding="utf-8")
			output = Path(directory) / "output"
			mod = SCORER.stage_replay_runtime(repo, output, "playtest-20260829")
			self.assertEqual(source.read_text(encoding="utf-8"),
				"Metadata:\n\tVersion: {DEV_VERSION}\n")
			self.assertIn("Version: playtest-20260829",
				(mod / "mod.yaml").read_text(encoding="utf-8"))

	def test_command_is_rendered_benchmark_bounded_and_resource_owned(self):
		command = SCORER.build_command(ROOT, Path("input.orarep"), Path("artifacts"),
			Path("locks"), Path("runtime/mods/cnc"))
		joined = " ".join(map(str, command))
		self.assertIn("with_resource_slots.py", joined)
		self.assertIn("--resource game", joined)
		self.assertIn("xvfb-run --auto-servernum", joined)
		self.assertIn("Game.Mod=runtime/mods/cnc", joined)
		self.assertIn("Launch.Benchmark=artifacts/benchmark", joined)
		self.assertIn("Launch.Replay=input.orarep", joined)
		self.assertNotIn("Launch.Headless", joined)

	def test_owner_filter_keeps_group_membership_and_matching_terminal_metric(self):
		log = "\n".join([
			"Stealth efficiency control membership owner=Commander bot_id=7 control=human generation=1 summary=terminal",
			"stealth_efficiency_watchdog|summary=terminal|bot_id=7|damage_adjusted=0.5",
			"Stealth efficiency control membership owner=VIKI bot_id=8 control=bot generation=2 summary=terminal",
			"stealth_efficiency_watchdog|summary=terminal|bot_id=8|damage_adjusted=1",
		])
		selected = SCORER.terminal_watchdog_lines(log, "Commander")
		self.assertEqual(len(selected), 2)
		self.assertTrue(all("bot_id=7" in line for line in selected))

	def test_summary_keeps_permanent_stationary_failures(self):
		line = "AI stationary watchdog failure owner=Commander unit=stnk#4 tick=1600"
		self.assertEqual(SCORER.terminal_watchdog_lines(line, None), [line])

	def test_help_disclaims_owner_aggregate_cadence_comparability(self):
		self.assertIn("live per-squad cadence is reported unavailable", SCORER.__doc__)

	def test_sha256_reads_without_modifying_replay(self):
		with tempfile.TemporaryDirectory() as directory:
			path = Path(directory) / "input.orarep"
			path.write_bytes(b"replay-bytes")
			before = path.read_bytes()
			self.assertEqual(SCORER.sha256(path),
				"d9670d435b6ddc50db172aaffadf8612a4632e7880bd55787c6938b01d1fce59")
			self.assertEqual(path.read_bytes(), before)


if __name__ == "__main__":
	unittest.main()
