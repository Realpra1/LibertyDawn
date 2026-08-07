import importlib.util
import sys
import tempfile
import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
MODULE_SPEC = importlib.util.spec_from_file_location(
    "run_cnc_performance_baseline", REPO_ROOT / "run-cnc-performance-baseline.py"
)
baseline = importlib.util.module_from_spec(MODULE_SPEC)
sys.modules[MODULE_SPEC.name] = baseline
MODULE_SPEC.loader.exec_module(baseline)


class PerformanceBaselineMetadataTest(unittest.TestCase):
    def test_artifact_inventory_hashes_files_and_excludes_symlinks(self):
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary)
            run = output / "normal-1"
            run.mkdir()
            artifact = run / "summary.json"
            artifact.write_text("evidence\n", encoding="utf-8")
            (run / "content-link").symlink_to(output)

            inventory = baseline.artifact_inventory(output, ["normal-1"])

            self.assertEqual(len(inventory), 1)
            self.assertEqual(inventory[0]["path"], "normal-1/summary.json")
            self.assertEqual(inventory[0]["size_bytes"], 9)
            self.assertEqual(inventory[0]["sha256"], baseline.sha256(artifact))

    def test_host_and_revision_metadata_use_explicit_nullable_fields(self):
        host = baseline.host_identity()
        revision = baseline.revision_identity(REPO_ROOT)

        self.assertIn("process_affinity", host)
        self.assertIn("load_average", host)
        self.assertEqual(len(revision["commit"]), 40)
        self.assertIsInstance(revision["dirty_paths"], list)


if __name__ == "__main__":
    unittest.main()
