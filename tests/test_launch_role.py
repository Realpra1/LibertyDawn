#!/usr/bin/env python3

import argparse
import importlib.util
import json
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
LAUNCHER_PATH = (
    REPO_ROOT
    / ".agents/skills/coordinate-cnc-development/scripts/launch_role.py"
)
MODULE_SPEC = importlib.util.spec_from_file_location("launch_role", LAUNCHER_PATH)
launch_role = importlib.util.module_from_spec(MODULE_SPEC)
sys.modules[MODULE_SPEC.name] = launch_role
MODULE_SPEC.loader.exec_module(launch_role)


class LaunchRoleTest(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="cnc87-role-test-")
        self.root = Path(self.temporary.name)

    def tearDown(self):
        self.temporary.cleanup()

    def make_args(self, role):
        output_dir = self.root / role
        inputs = output_dir / "inputs"
        inputs.mkdir(parents=True, exist_ok=True)
        if role == "commenter":
            artifact = inputs / "summary.json"
            artifact.write_text("{}\n", encoding="utf-8")
            job = {
                "artifacts": [str(artifact)],
                "output": str(output_dir / "NARRATIVE.md"),
            }
        elif role in launch_role.POLICY_ROLES:
            task_context = inputs / "TASK-CONTEXT.md"
            narrative = inputs / "NARRATIVE.md"
            task_context.write_text("Task CNC-87\n", encoding="utf-8")
            narrative.write_text("Factual narrative\n", encoding="utf-8")
            job = {
                "design_reference": str(
                    REPO_ROOT / ".agents/references/LIBERTY-DAWN-DESIGN.md"
                ),
                "task_context": str(task_context),
                "narrative": str(narrative),
                "output": str(output_dir / "POLICY-REVIEW.md"),
            }
        else:
            job = {}
        job_file = output_dir / "job.json"
        job_file.write_text(json.dumps(job), encoding="utf-8")
        return argparse.Namespace(
            role=role,
            worktree=REPO_ROOT,
            job_file=job_file,
            output_dir=output_dir,
        )

    def test_role_policy_and_protected_command_contract(self):
        expected = {
            "commenter": ("gpt-5.6-terra", "medium", "workspace-write"),
            "policy-reviewer": ("gpt-5.6-terra", "medium", "workspace-write"),
            "policy-speccer": ("gpt-5.6-sol", "high", "workspace-write"),
            "policy-escalation": ("gpt-5.6-sol", "xhigh", "workspace-write"),
        }
        for role, (model, effort, sandbox) in expected.items():
            with self.subTest(role=role):
                args = self.make_args(role)
                launch_role.validate_analysis_job(args)
                command, prompt = launch_role.build_command(args)
                self.assertEqual(command[:4], ["codex", "exec", "--ephemeral", "--json"])
                self.assertEqual(command[command.index("-C") + 1], str(args.output_dir))
                self.assertEqual(command[command.index("-s") + 1], sandbox)
                self.assertEqual(
                    command[command.index("-m") + 1], model
                )
                self.assertIn('approval_policy="never"', command)
                self.assertIn(f'model_reasoning_effort="{effort}"', command)
                self.assertEqual(
                    command[command.index("-o") + 1],
                    str(args.output_dir / "last-message.md"),
                )
                self.assertEqual(command[-1], prompt)

    @unittest.skipUnless(shutil.which("codex"), "installed Codex CLI required")
    def test_installed_cli_parser_accepts_production_and_rejects_legacy_control(self):
        for role in sorted(launch_role.ROLES):
            with self.subTest(role=role):
                args = self.make_args(role)
                command, _ = launch_role.build_command(args)
                evidence = launch_role.validate_cli_parser(command)
                self.assertEqual(evidence["production_exit"], 0, evidence)
                self.assertNotEqual(evidence["legacy_negative_exit"], 0, evidence)
                self.assertIn("unexpected argument '-a'", evidence["legacy_stderr"])
                self.assertFalse((args.output_dir / "last-message.md").exists())
                self.assertEqual(evidence["agent_events"], 0)

    def test_strict_analysis_envelopes_reject_inline_and_escaped_inputs(self):
        args = self.make_args("commenter")
        job = json.loads(args.job_file.read_text(encoding="utf-8"))
        job["inline_context"] = "must not reach the role"
        args.job_file.write_text(json.dumps(job), encoding="utf-8")
        with self.assertRaisesRegex(SystemExit, "permits only"):
            launch_role.validate_analysis_job(args)

        args = self.make_args("commenter")
        outside = self.root / "outside.log"
        outside.write_text("outside\n", encoding="utf-8")
        symlink = args.output_dir / "inputs" / "escaped.log"
        symlink.symlink_to(outside)
        job = json.loads(args.job_file.read_text(encoding="utf-8"))
        job["artifacts"] = [str(symlink)]
        args.job_file.write_text(json.dumps(job), encoding="utf-8")
        with self.assertRaisesRegex(SystemExit, "staged regular file"):
            launch_role.validate_analysis_job(args)

        args = self.make_args("policy-reviewer")
        job = json.loads(args.job_file.read_text(encoding="utf-8"))
        job["output"] = str(args.output_dir / "wrong-name.md")
        args.job_file.write_text(json.dumps(job), encoding="utf-8")
        with self.assertRaisesRegex(SystemExit, "POLICY-REVIEW.md"):
            launch_role.validate_analysis_job(args)

        args = self.make_args("policy-speccer")
        job = json.loads(args.job_file.read_text(encoding="utf-8"))
        Path(job["task_context"]).unlink()
        with self.assertRaisesRegex(SystemExit, "task context"):
            launch_role.validate_analysis_job(args)

        args = self.make_args("policy-escalation")
        job = json.loads(args.job_file.read_text(encoding="utf-8"))
        job["design_reference"] = str(self.root / "wrong-design.md")
        Path(job["design_reference"]).write_text("wrong\n", encoding="utf-8")
        args.job_file.write_text(json.dumps(job), encoding="utf-8")
        with self.assertRaisesRegex(SystemExit, "design_reference"):
            launch_role.validate_analysis_job(args)

    def test_missing_codex_executable_is_actionable(self):
        args = self.make_args("commenter")
        command, _ = launch_role.build_command(args)
        with self.assertRaisesRegex(RuntimeError, "could not run"):
            launch_role.validate_cli_parser(
                command, str(self.root / "definitely-missing-codex")
            )

    def make_fake_codex(self, exit_code=0):
        fake_bin = self.root / f"fake-bin-{exit_code}"
        fake_bin.mkdir()
        fake = fake_bin / "codex"
        fake.write_text(
            "#!/usr/bin/env python3\n"
            "import json,pathlib,sys\n"
            "args=sys.argv[1:]\n"
            "output=pathlib.Path(args[args.index('-o')+1])\n"
            "output.write_text('fake final message\\n')\n"
            "print(json.dumps({'type':'fake-event'}), flush=True)\n"
            f"raise SystemExit({exit_code})\n",
            encoding="utf-8",
        )
        fake.chmod(0o755)
        return fake_bin

    def test_foreground_fake_exit_and_background_supervision_metadata(self):
        failed_args = self.make_args("commenter")
        environment = dict(__import__("os").environ)
        environment["PATH"] = f"{self.make_fake_codex(7)}:{environment['PATH']}"
        failed = subprocess.run(
            [
                sys.executable,
                str(LAUNCHER_PATH),
                "--role",
                "commenter",
                "--worktree",
                str(REPO_ROOT),
                "--job-file",
                str(failed_args.job_file),
                "--output-dir",
                str(failed_args.output_dir),
            ],
            env=environment,
            capture_output=True,
            text=True,
        )
        self.assertEqual(failed.returncode, 7)
        failed_process = json.loads(
            (failed_args.output_dir / "process.json").read_text(encoding="utf-8")
        )
        self.assertEqual(failed_process["status"], "failed")
        self.assertEqual(failed_process["exit_code"], 7)

        background_args = self.make_args("policy-reviewer")
        environment["PATH"] = f"{self.make_fake_codex(0)}:{environment['PATH']}"
        launched = subprocess.run(
            [
                sys.executable,
                str(LAUNCHER_PATH),
                "--role",
                "policy-reviewer",
                "--worktree",
                str(REPO_ROOT),
                "--job-file",
                str(background_args.job_file),
                "--output-dir",
                str(background_args.output_dir),
                "--background",
            ],
            env=environment,
            capture_output=True,
            text=True,
        )
        self.assertEqual(launched.returncode, 0, launched.stderr)
        supervisor = json.loads(
            (background_args.output_dir / "supervisor.json").read_text(encoding="utf-8")
        )
        self.assertEqual(int(launched.stdout.strip()), supervisor["pid"])
        deadline = __import__("time").monotonic() + 3
        process_path = background_args.output_dir / "process.json"
        while __import__("time").monotonic() < deadline:
            if process_path.exists():
                value = json.loads(process_path.read_text(encoding="utf-8"))
                if value.get("status") == "complete":
                    break
            __import__("time").sleep(.02)
        else:
            self.fail("background supervisor did not complete")
        self.assertEqual(value["model"], "gpt-5.6-terra")
        self.assertEqual(value["reasoning_effort"], "medium")
        self.assertEqual(value["sandbox"], "workspace-write")
        self.assertEqual(value["session_directory"], str(background_args.output_dir))
        self.assertEqual(value["output_dir"], str(background_args.output_dir))
        self.assertTrue(value["supervised"])
        self.assertEqual(value["supervisor_pid"], supervisor["pid"])
        self.assertEqual(supervisor["role"], "policy-reviewer")
        self.assertEqual(supervisor["model"], "gpt-5.6-terra")
        self.assertEqual(supervisor["reasoning_effort"], "medium")
        self.assertEqual(supervisor["sandbox"], "workspace-write")

    def test_callers_cannot_supply_protected_model_or_effort(self):
        completed = subprocess.run(
            [
                sys.executable,
                str(LAUNCHER_PATH),
                "--role",
                "commenter",
                "--worktree",
                str(REPO_ROOT),
                "--job-file",
                str(self.root / "missing.json"),
                "--output-dir",
                str(self.root / "output"),
                "--model",
                "gpt-5.6-sol",
            ],
            capture_output=True,
            text=True,
        )
        self.assertEqual(completed.returncode, 2)
        self.assertIn("unrecognized arguments: --model", completed.stderr)


if __name__ == "__main__":
    unittest.main()
