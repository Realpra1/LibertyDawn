#!/usr/bin/env python3

import argparse
import importlib.util
import json
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest import mock
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
LAUNCHER_PATH = (
    REPO_ROOT
    / ".agents/skills/coordinate-cnc-development/scripts/launch_role.py"
)
sys.path.insert(0, str(LAUNCHER_PATH.parent))
MODULE_SPEC = importlib.util.spec_from_file_location("launch_role", LAUNCHER_PATH)
launch_role = importlib.util.module_from_spec(MODULE_SPEC)
sys.modules[MODULE_SPEC.name] = launch_role
MODULE_SPEC.loader.exec_module(launch_role)
from external_worker_runtime import (
    ProcessIdentityError,
    compare_process_identity,
    read_process_identity,
)


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
            scratchpad = inputs / "POLICY-SCRATCHPAD.md"
            task_context.write_text("Task CNC-87\n", encoding="utf-8")
            narrative.write_text("Factual narrative\n", encoding="utf-8")
            shutil.copyfile(
                REPO_ROOT
                / ".agents/references/LIBERTY-DAWN-POLICY-SCRATCHPAD.md",
                scratchpad,
            )
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

    def make_fake_codex(self, exit_code=0, probe_stdin=False):
        fake_bin = self.root / f"fake-bin-{exit_code}"
        fake_bin.mkdir()
        fake = fake_bin / "codex"
        fake.write_text(
            "#!/usr/bin/env python3\n"
            "import json,os,pathlib,sys\n"
            "args=sys.argv[1:]\n"
            "output=pathlib.Path(args[args.index('-o')+1])\n"
            + (
                "output.write_text(os.readlink('/proc/self/fd/0') + '\\n')\n"
                if probe_stdin
                else "output.write_text('fake final message\\n')\n"
            )
            + "print(json.dumps({'type':'fake-event'}), flush=True)\n"
            f"raise SystemExit({exit_code})\n",
            encoding="utf-8",
        )
        fake.chmod(0o755)
        for name in ("systemctl", "systemd-run"):
            unavailable = fake_bin / name
            unavailable.write_text("#!/bin/sh\nexit 1\n", encoding="utf-8")
            unavailable.chmod(0o755)
        return fake_bin

    def test_host_supervision_capability_and_durable_supported_metadata(self):
        capability = {
            "checked_utc": "2026-08-14T00:00:00+00:00",
            "probe_timeout_seconds": 3.0,
            "mechanism": "systemd-user-service",
            "systemctl": "/bin/systemctl",
            "systemd_run": "/bin/systemd-run",
            "supported": True,
            "fallback_reason": None,
        }
        args = self.make_args("worker")
        policy = launch_role.runtime_policy(args)
        identity = {
            "boot_id": "test-boot",
            "pid": 4242,
            "start_time_ticks": 100,
            "parent_pid": 1,
            "process_group_id": 4242,
            "session_id": 4242,
            "process_state": "S",
        }
        completed = subprocess.CompletedProcess([], 0, "", "")
        shown = subprocess.CompletedProcess([], 0, "4242\n", "")
        with mock.patch.object(
            launch_role, "host_supervision_capability", return_value=capability
        ), mock.patch.object(
            launch_role.subprocess, "run", side_effect=[completed, shown]
        ) as run, mock.patch.object(
            launch_role, "read_process_identity", return_value=identity
        ):
            record = launch_role._start_supervisor(
                args,
                assignment_root=args.output_dir,
                assignment_id="a" * 36,
                attempt_id="b" * 36,
                generation=2,
                policy=policy,
                started_utc="2026-08-14T00:00:00+00:00",
            )
        self.assertEqual(run.call_count, 2)
        self.assertEqual(record["pid"], 4242)
        self.assertEqual(record["supervision"]["mechanism"], "systemd-user-service")
        self.assertTrue(record["supervision"]["strong_host_supervision"])
        self.assertEqual(
            record["supervision"]["service_unit"],
            "libertydawn-worker-aaaaaaaaaaaa-g2-bbbbbbbb.service",
        )
        self.assertIsNone(record["supervision"]["fallback_reason"])
        self.assertEqual(
            record["supervision"]["stdout"],
            str((args.output_dir / "supervisor.log").resolve()),
        )

    def test_host_supervision_probe_timeout_is_actionable_fallback(self):
        with mock.patch.object(
            launch_role.shutil, "which", side_effect=lambda name: f"/bin/{name}"
        ), mock.patch.object(
            launch_role.subprocess,
            "run",
            side_effect=subprocess.TimeoutExpired(["systemctl"], 3),
        ):
            capability = launch_role.host_supervision_capability()
        self.assertFalse(capability["supported"])
        self.assertIn("exceeded 3.0s", capability["fallback_reason"])

    def test_foreground_fake_exit_and_policy_background_rejection(self):
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

        policy_args = self.make_args("policy-reviewer")
        blocked = subprocess.run(
            [
                sys.executable,
                str(LAUNCHER_PATH),
                "--role",
                "policy-reviewer",
                "--worktree",
                str(REPO_ROOT),
                "--job-file",
                str(policy_args.job_file),
                "--output-dir",
                str(policy_args.output_dir),
                "--background",
            ],
            env=environment,
            capture_output=True,
            text=True,
        )
        self.assertEqual(blocked.returncode, 64)
        self.assertIn("require foreground launch", blocked.stderr)
        self.assertFalse((policy_args.output_dir / "supervisor.json").exists())

    def test_background_supervision_metadata(self):
        background_args = self.make_args("commenter")
        environment = dict(__import__("os").environ)
        environment["PATH"] = (
            f"{self.make_fake_codex(0, probe_stdin=True)}:{environment['PATH']}"
        )
        launched = subprocess.run(
            [
                sys.executable,
                str(LAUNCHER_PATH),
                "--role",
                "commenter",
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
        self.assertEqual(value["schema"], "libertydawn.external-worker-attempt/v1")
        self.assertEqual(value["assignment_id"], supervisor["assignment_id"])
        self.assertEqual(value["attempt_id"], supervisor["attempt_id"])
        self.assertEqual(value["generation"], 1)
        self.assertTrue(
            compare_process_identity(
                supervisor["identity"], value["supervisor_identity"]
            )["match"]
        )
        self.assertEqual(
            (background_args.output_dir / "last-message.md").read_text().strip(),
            "/dev/null",
        )
        self.assertEqual(supervisor["role"], "commenter")
        self.assertEqual(supervisor["model"], "gpt-5.6-terra")
        self.assertEqual(supervisor["reasoning_effort"], "medium")
        self.assertEqual(supervisor["sandbox"], "workspace-write")
        self.assertEqual(
            supervisor["supervision"]["mechanism"],
            "detached-session-watchdog-fallback",
        )
        self.assertFalse(supervisor["supervision"]["strong_host_supervision"])
        self.assertFalse(supervisor["supervision"]["capability"]["supported"])
        self.assertIn(
            "systemd user manager is unavailable",
            supervisor["supervision"]["fallback_reason"],
        )
        self.assertEqual(supervisor["supervision"]["stdin"], "/dev/null")
        self.assertEqual(
            supervisor["supervision"]["stdout"],
            str((background_args.output_dir / "supervisor.log").resolve()),
        )

    def test_stable_process_identity_detects_pid_reuse_fields_without_signalling(self):
        sleeper = subprocess.Popen(
            [sys.executable, "-c", "import time; time.sleep(30)"],
            start_new_session=True,
        )
        try:
            identity = read_process_identity(sleeper.pid)
            self.assertEqual(identity["pid"], sleeper.pid)
            self.assertEqual(identity["process_group_id"], sleeper.pid)
            self.assertEqual(identity["session_id"], sleeper.pid)
            self.assertTrue(compare_process_identity(identity, dict(identity))["match"])
            reused = dict(identity)
            reused["start_time_ticks"] += 1
            comparison = compare_process_identity(identity, reused)
            self.assertFalse(comparison["match"])
            self.assertEqual(comparison["mismatches"], ["start_time_ticks"])
            rebooted = dict(identity)
            rebooted["boot_id"] = "00000000-0000-0000-0000-000000000000"
            self.assertEqual(
                compare_process_identity(identity, rebooted)["mismatches"],
                ["boot_id"],
            )
            self.assertIsNone(sleeper.poll())
        finally:
            sleeper.terminate()
            sleeper.wait(timeout=3)

    def test_malformed_proc_identity_is_actionable_not_healthy(self):
        proc_root = self.root / "proc"
        process_dir = proc_root / "123"
        process_dir.mkdir(parents=True)
        (process_dir / "stat").write_text("partial record\n", encoding="ascii")
        boot_id = self.root / "boot-id"
        boot_id.write_text("8e08e4c3-8636-4a17-bd81-c1542bcffe31\n", encoding="ascii")
        with self.assertRaisesRegex(ProcessIdentityError, "malformed /proc stat"):
            read_process_identity(123, boot_id_path=boot_id, proc_root=proc_root)

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
