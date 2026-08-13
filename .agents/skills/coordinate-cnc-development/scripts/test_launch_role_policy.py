#!/usr/bin/env python3
"""Focused regressions for the Policy Reviewer output transaction."""

from __future__ import annotations

import importlib.util
import json
import os
import pathlib
import subprocess
import tempfile
import unittest
from unittest import mock


SCRIPT = pathlib.Path(__file__).with_name("launch_role.py")
SPEC = importlib.util.spec_from_file_location("launch_role", SCRIPT)
assert SPEC and SPEC.loader
launch_role = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(launch_role)


class PolicyOutputTransactionTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.root = pathlib.Path(self.temporary.name)
        self.output = self.root / "output"
        self.output.mkdir()
        self.canonical = self.root / "canonical.md"
        self.canonical.write_text("old memory\n", encoding="utf-8")

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def write_outputs(self, scratchpad: str = "new memory\n") -> None:
        (self.output / "POLICY-REVIEW.md").write_text("useful review\n", encoding="utf-8")
        (self.output / "POLICY-SCRATCHPAD.md").write_text(scratchpad, encoding="utf-8")

    def test_valid_multibyte_3000_character_replacement_is_promoted(self) -> None:
        replacement = "é" * 3000
        self.write_outputs(replacement)
        result = launch_role.validate_and_promote_policy_outputs(
            self.output, self.canonical
        )
        self.assertEqual(replacement, self.canonical.read_text(encoding="utf-8"))
        self.assertEqual(3000, result["scratchpad_characters"])
        self.assertTrue((self.output / "POLICY-SCRATCHPAD.md").is_file())
        self.assertFalse(list(self.root.glob(".canonical.md.*.tmp")))
        self.assertEqual(0o644, self.canonical.stat().st_mode & 0o777)

    def test_invalid_outputs_preserve_canonical(self) -> None:
        cases = {
            "missing review": lambda: (
                self.output / "POLICY-SCRATCHPAD.md"
            ).write_text("replacement", encoding="utf-8"),
            "missing scratchpad": lambda: (self.output / "POLICY-REVIEW.md").write_text(
                "review", encoding="utf-8"
            ),
            "invalid utf8": lambda: (
                (self.output / "POLICY-REVIEW.md").write_text("review", encoding="utf-8"),
                (self.output / "POLICY-SCRATCHPAD.md").write_bytes(b"\xff"),
            ),
            "over limit": lambda: self.write_outputs("é" * 3001),
            "symlink": lambda: (
                (self.output / "POLICY-REVIEW.md").write_text("review", encoding="utf-8"),
                (self.output / "POLICY-SCRATCHPAD.md").symlink_to(self.canonical),
            ),
        }
        for label, setup in cases.items():
            with self.subTest(label=label):
                for path in self.output.iterdir():
                    path.unlink()
                setup()
                before = self.canonical.read_bytes()
                with self.assertRaises(launch_role.PolicyOutputError):
                    launch_role.validate_and_promote_policy_outputs(
                        self.output, self.canonical
                    )
                self.assertEqual(before, self.canonical.read_bytes())

    def test_replace_fault_preserves_canonical_and_retry_succeeds(self) -> None:
        self.write_outputs("first valid replacement")
        real_replace = launch_role.os.replace
        with mock.patch.object(launch_role.os, "replace", side_effect=OSError("injected")):
            with self.assertRaisesRegex(launch_role.PolicyOutputError, "atomically promote"):
                launch_role.validate_and_promote_policy_outputs(
                    self.output, self.canonical
                )
        self.assertEqual(b"old memory\n", self.canonical.read_bytes())
        self.assertFalse(list(self.root.glob(".canonical.md.*.tmp")))
        with mock.patch.object(launch_role.os, "replace", side_effect=real_replace):
            launch_role.validate_and_promote_policy_outputs(self.output, self.canonical)
        self.assertEqual("first valid replacement", self.canonical.read_text(encoding="utf-8"))

    def test_stale_outputs_are_archived_before_attempt(self) -> None:
        self.write_outputs("stale")
        archived = launch_role.prepare_policy_outputs(self.output)
        self.assertEqual(2, len(archived))
        self.assertFalse((self.output / "POLICY-REVIEW.md").exists())
        self.assertFalse((self.output / "POLICY-SCRATCHPAD.md").exists())
        with self.assertRaisesRegex(launch_role.PolicyOutputError, "review is missing"):
            launch_role.validate_and_promote_policy_outputs(self.output, self.canonical)
        self.write_outputs("fresh")
        launch_role.validate_and_promote_policy_outputs(self.output, self.canonical)
        self.assertEqual("fresh", self.canonical.read_text(encoding="utf-8"))


class PolicyLauncherProcessTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.root = pathlib.Path(self.temporary.name)
        self.worktree = self.root / "worktree"
        (self.worktree / ".agents/skills/review-cnc-policy").mkdir(parents=True)
        (self.worktree / ".agents/references").mkdir(parents=True)
        (self.worktree / ".agents/skills/review-cnc-policy/SKILL.md").write_text(
            "role instructions\n", encoding="utf-8"
        )
        (self.worktree / ".agents/references/LIBERTY-DAWN-DESIGN.md").write_text(
            "design\n", encoding="utf-8"
        )
        self.canonical = (
            self.worktree
            / ".agents/references/LIBERTY-DAWN-POLICY-SCRATCHPAD.md"
        )
        self.canonical.write_text("old memory\n", encoding="utf-8")
        self.bin_dir = self.root / "bin"
        self.bin_dir.mkdir()
        fake = self.bin_dir / "codex"
        fake.write_text(
            "#!/usr/bin/env python3\n"
            "import os, pathlib, sys\n"
            "out = pathlib.Path(sys.argv[sys.argv.index('-C') + 1])\n"
            "(out / 'POLICY-REVIEW.md').write_text('useful review\\n', encoding='utf-8')\n"
            "if os.environ.get('FAKE_MODE') == 'valid':\n"
            "    (out / 'POLICY-SCRATCHPAD.md').write_text('new memory ✓\\n', encoding='utf-8')\n"
            "if os.environ.get('FAKE_MODE') == 'nonzero':\n"
            "    (out / 'POLICY-SCRATCHPAD.md').write_text('must not promote\\n', encoding='utf-8')\n"
            "    raise SystemExit(7)\n",
            encoding="utf-8",
        )
        fake.chmod(0o755)

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def run_launcher(
        self, launcher: pathlib.Path, mode: str, name: str,
        role: str = "policy-reviewer", extra: tuple[str, ...] = (),
    ) -> subprocess.CompletedProcess[str]:
        output = self.root / name
        inputs = output / "inputs"
        inputs.mkdir(parents=True)
        (inputs / "TASK-CONTEXT.md").write_text("task\n", encoding="utf-8")
        (inputs / "NARRATIVE.md").write_text("narrative\n", encoding="utf-8")
        (inputs / "POLICY-SCRATCHPAD.md").write_text("old memory\n", encoding="utf-8")
        job = {
            "design_reference": str(
                self.worktree / ".agents/references/LIBERTY-DAWN-DESIGN.md"
            ),
            "task_context": str(inputs / "TASK-CONTEXT.md"),
            "narrative": str(inputs / "NARRATIVE.md"),
            "output": str(output / "POLICY-REVIEW.md"),
        }
        job_path = output / "job.json"
        job_path.write_text(json.dumps(job), encoding="utf-8")
        environment = os.environ.copy()
        environment["PATH"] = f"{self.bin_dir}:{environment['PATH']}"
        environment["FAKE_MODE"] = mode
        return subprocess.run(
            [
                "python3", str(launcher), "--role", role,
                "--worktree", str(self.worktree), "--job-file", str(job_path),
                "--output-dir", str(output), *extra,
            ],
            capture_output=True, text=True, timeout=10, check=False, env=environment,
        )

    def test_changed_launcher_rejects_omission_then_valid_retry_promotes(self) -> None:
        omitted = self.run_launcher(SCRIPT, "omitted", "changed-omitted")
        self.assertNotEqual(0, omitted.returncode)
        self.assertIn("scratchpad replacement is missing", omitted.stderr)
        self.assertEqual("old memory\n", self.canonical.read_text(encoding="utf-8"))
        metadata = json.loads((self.root / "changed-omitted/process.json").read_text())
        self.assertEqual("failed", metadata["status"])
        self.assertEqual(0, metadata["child_exit_code"])

        valid = self.run_launcher(SCRIPT, "valid", "changed-valid")
        self.assertEqual(0, valid.returncode, valid.stderr)
        self.assertEqual("new memory ✓\n", self.canonical.read_text(encoding="utf-8"))
        metadata = json.loads((self.root / "changed-valid/process.json").read_text())
        self.assertEqual("complete", metadata["status"])
        self.assertEqual(13, metadata["policy_output_contract"]["scratchpad_characters"])

    def test_every_policy_role_uses_contract_and_background_is_rejected(self) -> None:
        for role in sorted(launch_role.POLICY_ROLES):
            with self.subTest(role=role):
                self.canonical.write_text("old memory\n", encoding="utf-8")
                result = self.run_launcher(SCRIPT, "valid", f"role-{role}", role)
                self.assertEqual(0, result.returncode, result.stderr)
                self.assertEqual("new memory ✓\n", self.canonical.read_text(encoding="utf-8"))
        blocked = self.run_launcher(
            SCRIPT, "valid", "background", extra=("--background",)
        )
        self.assertEqual(64, blocked.returncode)
        self.assertIn("require foreground launch", blocked.stderr)

    def test_child_failure_with_outputs_does_not_promote(self) -> None:
        result = self.run_launcher(SCRIPT, "nonzero", "child-failed")
        self.assertEqual(7, result.returncode)
        self.assertEqual("old memory\n", self.canonical.read_text(encoding="utf-8"))
        metadata = json.loads((self.root / "child-failed/process.json").read_text())
        self.assertEqual("failed", metadata["status"])
        self.assertEqual(7, metadata["child_exit_code"])
        self.assertNotIn("policy_output_contract", metadata)

    def test_common_base_accepts_omission_and_does_not_promote_valid_output(self) -> None:
        old_launcher = self.root / "old-launch-role.py"
        old_launcher.write_bytes(
            subprocess.check_output(
                ["git", "show", "45d2acf6f3893d9718c3c1e0927af525f8b6e387:"
                 ".agents/skills/coordinate-cnc-development/scripts/launch_role.py"],
                cwd=SCRIPT.parents[3], timeout=10,
            )
        )
        omitted = self.run_launcher(old_launcher, "omitted", "old-omitted")
        self.assertEqual(0, omitted.returncode, omitted.stderr)
        self.assertEqual("old memory\n", self.canonical.read_text(encoding="utf-8"))
        valid = self.run_launcher(old_launcher, "valid", "old-valid")
        self.assertEqual(0, valid.returncode, valid.stderr)
        self.assertEqual("old memory\n", self.canonical.read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
