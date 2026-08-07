import importlib.util
import subprocess
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
    def git(self, root, *arguments):
        return subprocess.run(
            ["git", *arguments], cwd=root, check=True, text=True,
            stdout=subprocess.PIPE, stderr=subprocess.PIPE,
        ).stdout.strip()

    def initialize_checkout(self, root, version, include_runtime=False):
        (root / "mods" / "cnc").mkdir(parents=True)
        (root / "mods" / "cnc" / "mod.yaml").write_text(
            f"Metadata:\n\tVersion: {version}\n", encoding="utf-8"
        )
        if include_runtime:
            (root / "bin").mkdir()
            (root / "bin" / "OpenRA.dll").write_bytes(b"measured engine")
            (root / "launch-game.sh").write_text("#!/bin/sh\n", encoding="utf-8")
        self.git(root, "init")
        self.git(root, "config", "user.name", "CNC-47 Test")
        self.git(root, "config", "user.email", "cnc47@example.invalid")
        self.git(root, "add", ".")
        self.git(root, "commit", "-m", "fixture")

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

    def test_alternate_launcher_records_measured_and_workload_checkouts(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            workload = root / "workload"
            measured = root / "measured"
            workload.mkdir()
            measured.mkdir()
            self.initialize_checkout(workload, "workload-version")
            self.initialize_checkout(measured, "measured-version", include_runtime=True)
            (measured / "dirty-evidence.txt").write_text("dirty\n", encoding="utf-8")

            provenance = baseline.provenance_identity(
                workload, measured / "launch-game.sh"
            )

            self.assertEqual(
                provenance["workload_source"]["revision"]["commit"],
                self.git(workload, "rev-parse", "HEAD"),
            )
            self.assertFalse(provenance["workload_source"]["revision"]["dirty"])
            self.assertEqual(
                provenance["measured_checkout"]["revision"]["commit"],
                self.git(measured, "rev-parse", "HEAD"),
            )
            self.assertTrue(provenance["measured_checkout"]["revision"]["dirty"])
            self.assertEqual(
                provenance["measured_checkout"]["cnc_mod_version"],
                "measured-version",
            )
            self.assertEqual(
                provenance["measured_checkout"]["engine_assembly_sha256"],
                baseline.sha256(measured / "bin" / "OpenRA.dll"),
            )


if __name__ == "__main__":
    unittest.main()
