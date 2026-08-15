#!/usr/bin/env python3

import hashlib
import fcntl
import json
import os
import signal
import subprocess
import sys
import tempfile
import time
import unittest
from datetime import datetime, timedelta, timezone
from unittest import mock
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
SCRIPT_ROOT = REPO_ROOT / ".agents/skills/coordinate-cnc-development/scripts"
CANONICAL_LOCKS = Path("/root/github/LibertyDawn/.agents/locks")
LAUNCHER = SCRIPT_ROOT / "launch_role.py"
AUDITOR = SCRIPT_ROOT / "audit_external_worker.py"
WATCHDOG = SCRIPT_ROOT / "watch_external_workers.py"
STARTER = SCRIPT_ROOT / "start_external_worker.py"
sys.path.insert(0, str(SCRIPT_ROOT))

from external_worker_runtime import (
    ProcessIdentityError,
    WATCHDOG_REGISTRY_SCHEMA,
    audit_attempt,
    authorize_stopped_start,
    compare_process_identity,
    read_process_identity,
    register_assignment_ownership,
    register_watchdog_assignment,
    request_stop,
)
from launch_role import relaunch_assignment
from watch_external_workers import audit_registry_once


class ExternalWorkerRecoveryTest(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="cnc95-recovery-test-")
        self.root = Path(self.temporary.name)
        subprocess.run(["git", "init", "-q", "-b", "cnc95-test", str(self.root)], check=True)

    def tearDown(self):
        self.temporary.cleanup()

    def wait_json(self, path, predicate, timeout=5):
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            try:
                value = json.loads(path.read_text())
            except (FileNotFoundError, json.JSONDecodeError):
                time.sleep(.01)
                continue
            if predicate(value):
                return value
            time.sleep(.01)
        self.fail(f"timed out waiting for {path}")

    def wait_text(self, path, timeout=5):
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            try:
                value = path.read_text().strip()
            except FileNotFoundError:
                value = ""
            if value:
                return value
            time.sleep(.01)
        self.fail(f"timed out waiting for {path}")

    def make_sleeping_codex(self):
        fake_bin = self.root / "bin"
        fake_bin.mkdir(exist_ok=True)
        fake = fake_bin / "codex"
        fake.write_text(
            "#!/usr/bin/env python3\n"
            "import pathlib,sys,time\n"
            "args=sys.argv[1:]\n"
            "pathlib.Path(args[args.index('-o')+1]).write_text('started\\n')\n"
            "print('{\"type\":\"started\"}', flush=True)\n"
            "time.sleep(60)\n",
            encoding="utf-8",
        )
        fake.chmod(0o755)
        for name in ("systemctl", "systemd-run"):
            unavailable = fake_bin / name
            unavailable.write_text("#!/bin/sh\nexit 1\n", encoding="utf-8")
            unavailable.chmod(0o755)
        return fake_bin

    def launch_attempt(self, role="commenter", name="attempt-1", watchdog_registry=None):
        output = self.root / name
        inputs = output / "inputs"
        inputs.mkdir(parents=True)
        job = output / "job.json"
        if role == "commenter":
            artifact = inputs / "summary.json"
            artifact.write_text("{}\n")
            job.write_text(json.dumps({"artifacts": [str(artifact)], "output": str(output / "NARRATIVE.md")}) + "\n")
        else:
            job.write_text("worker state\n")
        environment = dict(os.environ)
        environment["PATH"] = f"{self.make_sleeping_codex()}:{environment['PATH']}"
        worktree = REPO_ROOT if role == "commenter" else self.root
        command = [
            sys.executable,
            str(LAUNCHER),
            "--role",
            role,
            "--worktree",
            str(worktree),
            "--job-file",
            str(job),
            "--output-dir",
            str(output),
            "--background",
        ]
        if watchdog_registry is not None:
            command.extend(["--watchdog-registry", str(watchdog_registry)])
        launched = subprocess.run(
            command,
            env=environment,
            capture_output=True,
            text=True,
            timeout=5,
        )
        self.assertEqual(launched.returncode, 0, launched.stderr)
        process = self.wait_json(
            output / "process.json", lambda value: value.get("status") == "running"
        )
        supervisor = json.loads((output / "supervisor.json").read_text())
        assignment = json.loads((output / "assignment.json").read_text())
        self.assertEqual(assignment["current_attempt_id"], process["attempt_id"])
        self.assertEqual(assignment["generation"], 1)
        self.assertEqual(assignment["next_generation"], 2)
        return output, process, supervisor

    def kill_verified(self, identity):
        observed = read_process_identity(identity["pid"])
        self.assertTrue(compare_process_identity(identity, observed)["match"])
        os.kill(identity["pid"], signal.SIGKILL)

    def stop_attempt(self, output):
        request_stop(output, reason="fault-test stop", requested_by="unit-test")
        result = audit_attempt(
            output, resolve_grace_seconds=.1, resolve_kill_seconds=.5
        )
        self.assertEqual(result["event"], "stopped")
        assignment_path = output / "assignment.json"
        return assignment_path, assignment_path.read_bytes()

    def register_descendant_fixture(self, output, process, descendant_pid):
        identity = read_process_identity(descendant_pid)
        proof = {
            "target_identity": identity,
            "registrar_identity": read_process_identity(os.getpid()),
            "worker_identity": process["identity"],
            "target_to_registrar": [identity],
            "registrar_to_worker": [process["identity"]],
            "max_depth": 64,
        }
        with mock.patch(
            "external_worker_runtime.prove_assignment_descendant_registration",
            return_value=proof,
        ):
            return register_assignment_ownership(
                output,
                assignment_id=process["assignment_id"],
                attempt_id=process["attempt_id"],
                generation=process["generation"],
                descendant_pid=descendant_pid,
                registrar_pid=os.getpid(),
            )

    def wait_not_live(self, identity, timeout=3):
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            try:
                if read_process_identity(identity["pid"]).get("process_state") == "Z":
                    return
            except Exception:
                return
            time.sleep(.01)
        self.fail(f"identity remained live: {identity}")

    def write_registry(self, path, assignments):
        path.write_text(
            json.dumps(
                {
                    "schema": WATCHDOG_REGISTRY_SCHEMA,
                    "assignments": [
                        {
                            "assignment_id": process["assignment_id"],
                            "assignment_root": str(output),
                        }
                        for output, process in assignments
                    ],
                }
            )
            + "\n"
        )

    def test_watchdog_registration_is_atomic_and_idempotent(self):
        registry = self.root / "registry.json"
        output, process, supervisor = self.launch_attempt(
            role="worker", watchdog_registry=registry
        )
        first = json.loads(registry.read_text())["assignments"][0]
        second = register_watchdog_assignment(registry, output)
        self.assertEqual(first, second)
        record = json.loads(registry.read_text())
        self.assertEqual(len(record["assignments"]), 1)
        self.assertEqual(record["assignments"][0]["assignment_id"], process["assignment_id"])
        self.assertTrue(registry.with_suffix(".json.lock").is_file())
        for identity in (process["identity"], supervisor["identity"]):
            try:
                self.kill_verified(identity)
            except ProcessIdentityError:
                pass

    def test_background_launch_rejects_bad_registry_without_starting_supervisor(self):
        output = self.root / "bad-registry-attempt"
        output.mkdir()
        job = output / "STATE.md"
        job.write_text("worker state\n")
        registry = self.root / "bad-registry.json"
        registry.write_text('{"schema":"unknown","assignments":[]}\n')
        completed = subprocess.run(
            [
                sys.executable, str(LAUNCHER), "--role", "worker",
                "--worktree", str(self.root), "--job-file", str(job),
                "--output-dir", str(output), "--watchdog-registry", str(registry),
                "--background",
            ],
            capture_output=True, text=True, timeout=5,
        )
        self.assertEqual(completed.returncode, 75, completed.stderr)
        self.assertIn("watchdog registry has an unknown schema", completed.stderr)
        self.assertFalse((output / "supervisor.json").exists())

    def test_two_auditors_terminalize_dead_attempt_once_without_touching_evidence(self):
        output, process, supervisor = self.launch_attempt()
        dirty = self.root / "dirty-worktree-marker"
        dirty.write_bytes(b"tracked/index/untracked sentinel\n")
        before = hashlib.sha256(dirty.read_bytes()).hexdigest()
        event_before = (output / "events.jsonl").read_bytes()
        self.kill_verified(process["identity"])
        self.kill_verified(supervisor["identity"])
        for identity in (process["identity"], supervisor["identity"]):
            deadline = time.monotonic() + 3
            while time.monotonic() < deadline and Path(
                f"/proc/{identity['pid']}"
            ).exists():
                time.sleep(.01)
        # The supervisor may win its normal child-exit finalization race. Restore
        # the stale-running incident fixture before racing independent audits.
        process_path = output / "process.json"
        stale_process = json.loads(process_path.read_text())
        stale_process["status"] = "running"
        for field in ("child_exit_code", "exit_code", "completed_utc"):
            stale_process.pop(field, None)
        process_path.write_text(json.dumps(stale_process) + "\n")
        assignment_path = output / "assignment.json"
        stale_assignment = json.loads(assignment_path.read_text())
        stale_assignment["status"] = "running"
        assignment_path.write_text(json.dumps(stale_assignment) + "\n")

        commands = [
            [sys.executable, str(AUDITOR), "--output-dir", str(output)]
            for _ in range(2)
        ]
        auditors = [
            subprocess.Popen(
                command,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                text=True,
            )
            for command in commands
        ]
        results = [auditor.communicate(timeout=5) for auditor in auditors]
        self.assertEqual([auditor.returncode for auditor in auditors], [0, 0], results)
        events = [json.loads(result[0])["event"] for result in results]
        self.assertEqual(sorted(events), ["already-terminal", "interrupted"])

        terminal = json.loads((output / "process.json").read_text())
        self.assertEqual(terminal["status"], "interrupted")
        self.assertEqual(
            json.loads((output / "supervisor.json").read_text())["status"],
            "interrupted",
        )
        receipt = json.loads((output / "interruption.json").read_text())
        self.assertEqual(receipt["old_process_record"]["status"], "running")
        self.assertEqual(receipt["worker"]["state"], "missing")
        self.assertEqual(receipt["supervisor"]["state"], "missing")
        self.assertEqual(receipt["descendants"], [])
        self.assertEqual(receipt["resources"], [])
        self.assertEqual(receipt["resolution"], [])
        quarantine = json.loads((output / "quarantine.json").read_text())
        self.assertEqual(quarantine["status"], "non-acceptance")
        self.assertIn(
            str(output / "events.jsonl"),
            [item["path"] for item in quarantine["artifacts"]],
        )
        self.assertEqual((output / "events.jsonl").read_bytes(), event_before)
        self.assertEqual(hashlib.sha256(dirty.read_bytes()).hexdigest(), before)
        lease = output / ".recovery.lock"
        inode = lease.stat().st_ino
        rerun = subprocess.run(commands[0], capture_output=True, text=True, timeout=5)
        self.assertEqual(rerun.returncode, 0, rerun.stderr)
        self.assertEqual(json.loads(rerun.stdout)["event"], "already-terminal")
        self.assertEqual(lease.stat().st_ino, inode)

    def test_live_worker_is_healthy_and_pid_reuse_decoy_is_not_signalled(self):
        output, process, supervisor = self.launch_attempt()
        try:
            healthy = subprocess.run(
                [sys.executable, str(AUDITOR), "--output-dir", str(output)],
                capture_output=True,
                text=True,
                timeout=5,
            )
            self.assertEqual(json.loads(healthy.stdout)["event"], "healthy")
            forged = json.loads((output / "process.json").read_text())
            forged["identity"]["start_time_ticks"] += 1
            (output / "process.json").write_text(json.dumps(forged) + "\n")
            partial = subprocess.run(
                [sys.executable, str(AUDITOR), "--output-dir", str(output)],
                capture_output=True,
                text=True,
                timeout=5,
            )
            self.assertEqual(json.loads(partial.stdout)["event"], "partial-tree")
            self.assertTrue(Path(f"/proc/{process['identity']['pid']}").exists())
        finally:
            for identity in (process["identity"], supervisor["identity"]):
                try:
                    observed = read_process_identity(identity["pid"])
                    if compare_process_identity(identity, observed)["match"]:
                        os.kill(identity["pid"], signal.SIGKILL)
                except Exception:
                    pass

    def test_live_registered_descendant_and_contended_kernel_slot_block_interruption(self):
        output, process, supervisor = self.launch_attempt()
        descendant = subprocess.Popen([sys.executable, "-c", "import time; time.sleep(60)"])
        lock_dir = self.root / "local-locks"
        lock_dir.mkdir()
        lock_path = lock_dir / "test-1.lock"
        lock_holder = subprocess.Popen(
            [
                sys.executable,
                str(SCRIPT_ROOT / "with_resource_slots.py"),
                "--lock-dir",
                str(lock_dir),
                "--resource",
                "test",
                "--capacity",
                "1",
                "--",
                sys.executable,
                "-c",
                "import time; time.sleep(60)",
            ],
            stderr=subprocess.PIPE,
            text=True,
        )
        try:
            deadline = time.monotonic() + 2
            while time.monotonic() < deadline and not lock_path.exists():
                time.sleep(.01)
            self.assertTrue(lock_path.exists())
            lock_inode = lock_path.stat().st_ino
            self.register_descendant_fixture(output, process, descendant.pid)
            self.kill_verified(process["identity"])
            self.kill_verified(supervisor["identity"])
            partial = subprocess.run(
                [sys.executable, str(AUDITOR), "--output-dir", str(output)],
                capture_output=True,
                text=True,
                timeout=5,
            )
            result = json.loads(partial.stdout)
            self.assertEqual(result["event"], "partial-tree")
            self.assertEqual(result["reason"], "registered-descendant-still-live")
            self.assertEqual(json.loads((output / "process.json").read_text())["status"], "running")
            self.assertIsNone(descendant.poll())
            self.assertIsNone(lock_holder.poll())
            self.assertEqual(lock_path.stat().st_ino, lock_inode)
            descendant.terminate()
            descendant.wait(timeout=5)
            register_assignment_ownership(
                output,
                assignment_id=process["assignment_id"],
                attempt_id=process["attempt_id"],
                generation=process["generation"],
                resource={"resource": "game", "path": "/canonical/game-1.lock", "device": 1, "inode": 2},
            )
            with mock.patch(
                "external_worker_runtime.observe_resource_status",
                return_value=[
                    {
                        "resource": "game",
                        "state": "observed",
                        "slots": [
                            {
                                "path": "/canonical/game-1.lock",
                                "device": 1,
                                "inode": 2,
                                "availability": "contended",
                                "metadata_classification": "last-known",
                            }
                        ],
                    }
                ],
            ):
                blocked = audit_attempt(output, resource_lock_dir=self.root)
            self.assertEqual(blocked["event"], "blocked")
            self.assertEqual(blocked["reason"], "canonical-resource-owner-unverified")
            self.assertEqual(json.loads((output / "process.json").read_text())["status"], "blocked")
            self.assertEqual(json.loads((output / "assignment.json").read_text())["status"], "blocked")
            self.assertIsNone(lock_holder.poll())
            self.assertEqual(lock_path.stat().st_ino, lock_inode)
        finally:
            if descendant.poll() is None:
                descendant.terminate()
            lock_holder.terminate()
            try:
                descendant.wait(timeout=5)
            except subprocess.TimeoutExpired:
                descendant.kill()
            try:
                lock_holder.wait(timeout=5)
            except subprocess.TimeoutExpired:
                lock_holder.kill()
            if lock_holder.stderr is not None:
                lock_holder.stderr.close()

    def test_assignment_generation_mismatch_is_superseded_without_transition(self):
        output, process, supervisor = self.launch_attempt()
        try:
            assignment_path = output / "assignment.json"
            assignment = json.loads(assignment_path.read_text())
            assignment["generation"] += 1
            assignment_path.write_text(json.dumps(assignment) + "\n")
            result = subprocess.run(
                [sys.executable, str(AUDITOR), "--output-dir", str(output)],
                capture_output=True,
                text=True,
                timeout=5,
            )
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertEqual(json.loads(result.stdout)["event"], "superseded")
            self.assertEqual(json.loads((output / "process.json").read_text())["status"], "running")
        finally:
            for identity in (process["identity"], supervisor["identity"]):
                try:
                    observed = read_process_identity(identity["pid"])
                    if compare_process_identity(identity, observed)["match"]:
                        os.kill(identity["pid"], signal.SIGKILL)
                except Exception:
                    pass

    def test_recovery_resolves_owned_descendant_and_relaunches_once_without_overwrite(self):
        output, process, supervisor = self.launch_attempt(role="worker")
        descendant = subprocess.Popen([sys.executable, "-c", "import time; time.sleep(60)"])
        try:
            self.register_descendant_fixture(output, process, descendant.pid)
            old_process = (output / "process.json").read_bytes()
            self.kill_verified(process["identity"])
            self.kill_verified(supervisor["identity"])
            for identity in (process["identity"], supervisor["identity"]):
                self.wait_not_live(identity)
            result = audit_attempt(
                output, recover=relaunch_assignment,
                resolve_grace_seconds=.1, resolve_kill_seconds=.5,
            )
            self.assertEqual(result["event"], "relaunch-started")
            self.assertEqual(result["generation"], 2)
            self.assertEqual(result["predecessor_attempt_id"], process["attempt_id"])
            self.assertIsNotNone(descendant.poll())
            assignment = self.wait_json(output / "assignment.json", lambda value: value.get("status") == "running")
            self.assertEqual(assignment["generation"], 2)
            self.assertEqual(assignment["registrations"]["attempt_id"], assignment["current_attempt_id"])
            replacement = Path(assignment["current_attempt_dir"])
            replacement_process = self.wait_json(replacement / "process.json", lambda value: value.get("status") == "running")
            self.assertEqual(replacement_process["job_file"], str((output / "job.json").resolve()))
            self.assertEqual((output / "interruption.json").is_file(), True)
            interruption = json.loads((output / "interruption.json").read_text())
            self.assertEqual(len(interruption["descendants"]), 1)
            self.assertTrue(
                interruption["resolution"][0]["result"].startswith("resolved")
            )
            self.assertNotEqual((output / "process.json").read_bytes(), old_process)
            self.assertEqual(json.loads((output / "process.json").read_text())["status"], "interrupted")
        finally:
            if descendant.poll() is None:
                descendant.kill()
            assignment_path = output / "assignment.json"
            if assignment_path.exists():
                assignment = json.loads(assignment_path.read_text())
                attempt_dir = Path(str(assignment.get("current_attempt_dir", output)))
                for name in ("process.json", "supervisor.json"):
                    path = attempt_dir / name
                    if path.exists():
                        identity = json.loads(path.read_text()).get("identity")
                        if identity:
                            try:
                                observed = read_process_identity(identity["pid"])
                                if compare_process_identity(identity, observed)["match"]:
                                    os.kill(identity["pid"], signal.SIGKILL)
                            except Exception:
                                pass

    def test_durable_stop_wins_and_suppresses_relaunch(self):
        output, process, supervisor = self.launch_attempt(role="worker")
        intent = request_stop(output, reason="test cancellation", requested_by="unit-test")
        self.assertEqual(intent["attempt_id"], process["attempt_id"])
        result = audit_attempt(
            output, recover=relaunch_assignment,
            resolve_grace_seconds=.1, resolve_kill_seconds=.5,
        )
        self.assertEqual(result["event"], "stopped")
        assignment = json.loads((output / "assignment.json").read_text())
        self.assertEqual(assignment["status"], "stopped")
        self.assertEqual(assignment["generation"], 1)
        self.assertFalse((output / "attempts").exists())
        self.assertTrue((output / "stop.json").is_file())
        suppressed = audit_attempt(output, recover=relaunch_assignment)
        self.assertEqual(suppressed["event"], "already-terminal")
        self.assertEqual(json.loads((output / "assignment.json").read_text())["generation"], 1)

    def test_durable_stop_wins_after_worker_signal_exit_is_finalized(self):
        output, process, _ = self.launch_attempt(role="worker")
        intent = request_stop(
            output, reason="finalization race", requested_by="unit-test"
        )
        self.kill_verified(process["identity"])
        failed = self.wait_json(
            output / "process.json", lambda value: value.get("status") == "failed"
        )
        self.assertLess(failed["child_exit_code"], 0)
        result = audit_attempt(
            output,
            recover=relaunch_assignment,
            resolve_grace_seconds=.1,
            resolve_kill_seconds=.5,
        )
        self.assertEqual(result["event"], "stopped")
        assignment = json.loads((output / "assignment.json").read_text())
        self.assertEqual(assignment["status"], "stopped")
        self.assertEqual(assignment["stop_intent"], intent)
        self.assertFalse((output / "attempts").exists())

    def test_explicit_start_supersedes_stop_and_relaunches_once(self):
        output, process, supervisor = self.launch_attempt(role="worker")
        request_stop(output, reason="test cancellation", requested_by="unit-test")
        stopped = audit_attempt(
            output, recover=relaunch_assignment,
            resolve_grace_seconds=.1, resolve_kill_seconds=.5,
        )
        self.assertEqual(stopped["event"], "stopped")
        result = authorize_stopped_start(
            output,
            reason="authorized test resume",
            requested_by="unit-test",
            recover=relaunch_assignment,
        )
        self.assertEqual(result["event"], "relaunch-started")
        self.assertEqual(result["generation"], 2)
        assignment = self.wait_json(
            output / "assignment.json", lambda value: value.get("status") == "running"
        )
        self.assertIsNone(assignment["stop_intent"])
        self.assertEqual(assignment["superseded_stop_intent"]["generation"], 1)
        self.assertEqual(assignment["role"], "worker")
        self.assertEqual(assignment["worktree"], str(self.root.resolve()))
        self.assertEqual(assignment["job_file"], str((output / "job.json").resolve()))
        authorization = json.loads((output / "start.json").read_text())
        self.assertEqual(authorization["predecessor_generation"], 1)
        self.assertEqual(authorization["superseded_stop_intent"]["reason"], "test cancellation")
        with self.assertRaisesRegex(ProcessIdentityError, "requires stopped status"):
            authorize_stopped_start(
                output,
                reason="duplicate resume",
                requested_by="unit-test",
                recover=relaunch_assignment,
            )
        self.assertEqual(len(list((output / "attempts").iterdir())), 1)
        replacement = Path(assignment["current_attempt_dir"])
        for name in ("process.json", "supervisor.json"):
            identity = json.loads((replacement / name).read_text()).get("identity")
            if identity:
                try:
                    os.kill(identity["pid"], signal.SIGKILL)
                except ProcessLookupError:
                    pass

    def test_start_cli_refuses_non_stopped_assignment_without_clearing_stop(self):
        output, process, supervisor = self.launch_attempt(role="worker")
        intent = request_stop(output, reason="pending cancellation", requested_by="unit-test")
        completed = subprocess.run(
            [
                sys.executable, str(STARTER), "--output-dir", str(output),
                "--reason", "too early", "--requested-by", "unit-test",
            ],
            capture_output=True, text=True, timeout=5,
        )
        self.assertEqual(completed.returncode, 75)
        self.assertEqual(json.loads(completed.stdout)["event"], "start-error")
        assignment = json.loads((output / "assignment.json").read_text())
        self.assertEqual(assignment["stop_intent"], intent)
        self.assertEqual(assignment["status"], "stop-requested")
        result = audit_attempt(
            output, resolve_grace_seconds=.1, resolve_kill_seconds=.5
        )
        self.assertEqual(result["event"], "stopped")

    def test_two_explicit_start_callers_create_one_generation(self):
        output, process, supervisor = self.launch_attempt(role="worker")
        request_stop(output, reason="race setup", requested_by="unit-test")
        stopped = audit_attempt(
            output, resolve_grace_seconds=.1, resolve_kill_seconds=.5
        )
        self.assertEqual(stopped["event"], "stopped")
        environment = dict(os.environ)
        environment["PATH"] = f"{self.root / 'bin'}:{environment['PATH']}"
        command = [
            sys.executable, str(STARTER), "--output-dir", str(output),
            "--reason", "race authorization", "--requested-by", "unit-test",
        ]
        starters = [
            subprocess.Popen(
                command, env=environment, stdout=subprocess.PIPE,
                stderr=subprocess.PIPE, text=True,
            )
            for _ in range(2)
        ]
        completed = [starter.communicate(timeout=8) for starter in starters]
        events = [json.loads(stdout)["event"] for stdout, _ in completed]
        self.assertEqual(events.count("relaunch-started"), 1, completed)
        self.assertEqual(events.count("start-error"), 1, completed)
        assignment = self.wait_json(
            output / "assignment.json", lambda value: value.get("status") == "running"
        )
        self.assertEqual(assignment["generation"], 2)
        self.assertEqual(len(list((output / "attempts").iterdir())), 1)
        replacement = Path(assignment["current_attempt_dir"])
        for name in ("process.json", "supervisor.json"):
            identity = json.loads((replacement / name).read_text()).get("identity")
            if identity:
                try:
                    os.kill(identity["pid"], signal.SIGKILL)
                except ProcessLookupError:
                    pass

    def test_rejected_start_envelopes_preserve_durable_stop_without_signals(self):
        mutations = ("missing-state", "symlink-state", "wrong-branch", "wrong-role")
        for mutation in mutations:
            with self.subTest(mutation=mutation):
                output, _, _ = self.launch_attempt(role="worker", name=mutation)
                assignment_path, _ = self.stop_attempt(output)
                assignment = json.loads(assignment_path.read_text())
                job = Path(assignment["job_file"])
                if mutation == "missing-state":
                    job.unlink()
                elif mutation == "symlink-state":
                    target = job.with_name("preserved-state.md")
                    job.rename(target)
                    job.symlink_to(target)
                elif mutation == "wrong-branch":
                    assignment["branch"] = "not-the-authorized-branch"
                    assignment_path.write_text(json.dumps(assignment) + "\n")
                else:
                    assignment["role"] = "commenter"
                    assignment_path.write_text(json.dumps(assignment) + "\n")
                stopped_bytes = assignment_path.read_bytes()
                with mock.patch("external_worker_runtime.os.kill") as signal_process:
                    with self.assertRaises(ProcessIdentityError):
                        authorize_stopped_start(
                            output,
                            reason="fault injection",
                            requested_by="unit-test",
                            recover=relaunch_assignment,
                        )
                signal_process.assert_not_called()
                self.assertEqual(assignment_path.read_bytes(), stopped_bytes)
                current = json.loads(assignment_path.read_text())
                self.assertEqual(current["status"], "stopped")
                self.assertIsNotNone(current["stop_intent"])
                self.assertFalse((output / "attempts").exists())

    def test_proc_inspection_error_rejects_start_without_clearing_stop(self):
        output, process, _ = self.launch_attempt(role="worker", name="proc-error")
        assignment_path, stopped_bytes = self.stop_attempt(output)
        proc_root = self.root / "inaccessible-proc"
        (proc_root / str(process["identity"]["pid"])).mkdir(parents=True)
        with mock.patch("external_worker_runtime.os.kill") as signal_process:
            with self.assertRaisesRegex(ProcessIdentityError, "inspection is inconclusive"):
                authorize_stopped_start(
                    output,
                    reason="fault injection",
                    requested_by="unit-test",
                    recover=relaunch_assignment,
                    proc_root=proc_root,
                )
        signal_process.assert_not_called()
        self.assertEqual(assignment_path.read_bytes(), stopped_bytes)
        self.assertIsNotNone(json.loads(assignment_path.read_text())["stop_intent"])
        self.assertFalse((output / "start.json").exists())

    def test_start_receipt_write_failure_preserves_stop_and_skips_relaunch(self):
        output, _, _ = self.launch_attempt(role="worker", name="receipt-failure")
        assignment_path, stopped_bytes = self.stop_attempt(output)
        real_write = __import__("external_worker_runtime").atomic_write_json

        def fail_start_receipt(path, value):
            if Path(path).name == "start.json":
                raise OSError("injected start receipt failure")
            return real_write(path, value)

        recover = mock.Mock()
        with mock.patch(
            "external_worker_runtime.atomic_write_json", side_effect=fail_start_receipt
        ), mock.patch("external_worker_runtime.os.kill") as signal_process:
            with self.assertRaisesRegex(OSError, "injected start receipt failure"):
                authorize_stopped_start(
                    output,
                    reason="fault injection",
                    requested_by="unit-test",
                    recover=recover,
                )
        recover.assert_not_called()
        signal_process.assert_not_called()
        self.assertEqual(assignment_path.read_bytes(), stopped_bytes)
        self.assertFalse((output / "start.json").exists())

    def test_assignment_reservation_write_failure_rolls_back_empty_generation(self):
        output, _, _ = self.launch_attempt(role="worker", name="assignment-failure")
        assignment_path, stopped_bytes = self.stop_attempt(output)
        failed_write = mock.Mock(
            side_effect=OSError("injected assignment reservation failure")
        )
        with mock.patch.dict(
            relaunch_assignment.__globals__, {"atomic_write_json": failed_write}
        ), mock.patch("external_worker_runtime.os.kill") as signal_process:
            with self.assertRaisesRegex(
                OSError, "injected assignment reservation failure"
            ):
                authorize_stopped_start(
                    output,
                    reason="fault injection",
                    requested_by="unit-test",
                    recover=relaunch_assignment,
                )
        signal_process.assert_not_called()
        self.assertEqual(assignment_path.read_bytes(), stopped_bytes)
        self.assertIsNotNone(json.loads(assignment_path.read_text())["stop_intent"])
        attempts = output / "attempts"
        self.assertTrue(not attempts.exists() or not any(attempts.iterdir()))

    def test_stop_during_relaunch_resolves_supervisor_before_process_publication(self):
        output, process, supervisor = self.launch_attempt(role="worker")
        self.kill_verified(process["identity"])
        self.kill_verified(supervisor["identity"])
        for identity in (process["identity"], supervisor["identity"]):
            self.wait_not_live(identity)
        relaunch_supervisor = subprocess.Popen(
            [sys.executable, "-c", "import time; time.sleep(60)"], start_new_session=True
        )
        attempt_id = "00000000-0000-4000-8000-000000000002"
        attempt_dir = output / "attempts" / f"generation-000002-{attempt_id}"
        attempt_dir.mkdir(parents=True)
        identity = read_process_identity(relaunch_supervisor.pid)
        (attempt_dir / "supervisor.json").write_text(
            json.dumps(
                {
                    "schema": "libertydawn.external-worker-attempt/v1",
                    "assignment_id": process["assignment_id"],
                    "attempt_id": attempt_id,
                    "generation": 2,
                    "identity": identity,
                    "status": "launched",
                }
            )
            + "\n"
        )
        assignment_path = output / "assignment.json"
        assignment = json.loads(assignment_path.read_text())
        assignment.update(
            {
                "current_attempt_id": attempt_id,
                "generation": 2,
                "next_generation": 3,
                "current_attempt_dir": str(attempt_dir),
                "status": "recovering",
                "registrations": {"attempt_id": attempt_id, "descendants": [], "resources": []},
            }
        )
        assignment_path.write_text(json.dumps(assignment) + "\n")
        request_stop(output, reason="race cancellation", requested_by="unit-test")
        result = audit_attempt(output, resolve_grace_seconds=.1, resolve_kill_seconds=.5)
        self.assertEqual(result["event"], "stopped")
        self.assertIsNotNone(relaunch_supervisor.poll())
        self.assertFalse((attempt_dir / "process.json").exists())
        self.assertEqual(json.loads(assignment_path.read_text())["status"], "stopped")

    def test_two_recovery_auditors_create_only_one_generation(self):
        output, process, supervisor = self.launch_attempt(role="worker")
        self.kill_verified(process["identity"])
        self.kill_verified(supervisor["identity"])
        for identity in (process["identity"], supervisor["identity"]):
            self.wait_not_live(identity)
        environment = dict(os.environ)
        environment["PATH"] = f"{self.root / 'bin'}:{environment['PATH']}"
        commands = [
            [sys.executable, str(AUDITOR), "--output-dir", str(output), "--recover"]
            for _ in range(2)
        ]
        auditors = [subprocess.Popen(command, env=environment, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True) for command in commands]
        results = [auditor.communicate(timeout=8) for auditor in auditors]
        self.assertEqual([auditor.returncode for auditor in auditors], [0, 0], results)
        events = [json.loads(stdout)["event"] for stdout, _ in results]
        self.assertIn("relaunch-started", events)
        self.assertEqual(len(list((output / "attempts").iterdir())), 1)
        assignment = self.wait_json(output / "assignment.json", lambda value: value.get("status") == "running")
        self.assertEqual(assignment["generation"], 2)
        replacement = Path(assignment["current_attempt_dir"])
        for name in ("process.json", "supervisor.json"):
            identity = json.loads((replacement / name).read_text()).get("identity")
            if identity:
                try:
                    os.kill(identity["pid"], signal.SIGKILL)
                except ProcessLookupError:
                    pass

    def test_worker_signal_failure_recovers_after_live_supervisor_finalizes(self):
        output, process, supervisor = self.launch_attempt(role="worker")
        self.kill_verified(process["identity"])
        failed = self.wait_json(
            output / "process.json", lambda value: value.get("status") == "failed"
        )
        self.assertLess(failed["child_exit_code"], 0)
        self.wait_not_live(supervisor["identity"])
        result = audit_attempt(
            output,
            recover=relaunch_assignment,
            resolve_grace_seconds=.1,
            resolve_kill_seconds=.5,
        )
        self.assertEqual(result["event"], "relaunch-started")
        interruption = json.loads((output / "interruption.json").read_text())
        self.assertEqual(
            interruption["reason"],
            "worker-exited-by-signal-and-supervisor-identity-not-live",
        )
        assignment = self.wait_json(
            output / "assignment.json", lambda value: value.get("status") == "running"
        )
        replacement = Path(assignment["current_attempt_dir"])
        for name in ("process.json", "supervisor.json"):
            identity = json.loads((replacement / name).read_text()).get("identity")
            if identity:
                try:
                    os.kill(identity["pid"], signal.SIGKILL)
                except ProcessLookupError:
                    pass

    def test_resource_wrapper_registers_guardian_child_and_exact_kernel_slot(self):
        output, process, supervisor = self.launch_attempt(role="worker")
        process_path = output / "process.json"
        original_process = process_path.read_bytes()
        registration_process = json.loads(original_process)
        # This test invokes the wrapper itself, so make the test process the
        # recorded worker only for the ancestry-registration transaction.
        registration_process["identity"] = read_process_identity(os.getpid())
        process_path.write_text(json.dumps(registration_process) + "\n")
        environment = dict(os.environ)
        environment.update(
            {
                "LIBERTY_DAWN_ASSIGNMENT_ID": process["assignment_id"],
                "LIBERTY_DAWN_ATTEMPT_ID": process["attempt_id"],
                "LIBERTY_DAWN_ATTEMPT_GENERATION": "1",
                "LIBERTY_DAWN_ASSIGNMENT_ROOT": str(output),
            }
        )
        try:
            completed = subprocess.run(
                [
                    sys.executable, str(SCRIPT_ROOT / "with_resource_slots.py"),
                    "--lock-dir", str(CANONICAL_LOCKS), "--resource", "game",
                    "--capacity", "2", "--slots", "1", "--timeout", "60", "--",
                    sys.executable, "-c", "import time; time.sleep(.15)",
                ],
                env=environment, capture_output=True, text=True, timeout=70,
            )
        finally:
            process_path.write_bytes(original_process)
        self.assertEqual(completed.returncode, 0, completed.stderr)
        assignment = json.loads((output / "assignment.json").read_text())
        registrations = assignment["registrations"]
        self.assertGreaterEqual(len(registrations["descendants"]), 2)
        self.assertEqual(len(registrations["resources"]), 1)
        resource = registrations["resources"][0]
        self.assertEqual(resource["resource"], "game")
        facts = Path(resource["path"]).stat()
        self.assertEqual((resource["device"], resource["inode"]), (facts.st_dev, facts.st_ino))
        request_stop(output, reason="test cleanup", requested_by="unit-test")
        stopped = audit_attempt(output, resource_lock_dir=CANONICAL_LOCKS, resolve_grace_seconds=.1, resolve_kill_seconds=.5)
        self.assertEqual(stopped["event"], "stopped")

    def test_descendant_registration_requires_stable_bounded_assignment_ancestry(self):
        output, process, supervisor = self.launch_attempt(role="worker")
        process_path = output / "process.json"
        original_process = process_path.read_bytes()
        child_pid_path = self.root / "registered-child.pid"
        registrar = subprocess.Popen(
            [
                sys.executable,
                "-c",
                (
                    "import pathlib,subprocess,sys,time; "
                    "child=subprocess.Popen([sys.executable,'-c','import time; time.sleep(60)']); "
                    f"pathlib.Path({str(child_pid_path)!r}).write_text(str(child.pid)); "
                    "time.sleep(60)"
                ),
            ]
        )
        unrelated = subprocess.Popen([sys.executable, "-c", "import time; time.sleep(60)"])
        registered_identity = None
        try:
            child_pid = int(self.wait_text(child_pid_path))
            registered_identity = read_process_identity(child_pid)
            registration_process = json.loads(original_process)
            registration_process["identity"] = read_process_identity(os.getpid())
            process_path.write_text(json.dumps(registration_process) + "\n")
            registrations = register_assignment_ownership(
                output,
                assignment_id=process["assignment_id"],
                attempt_id=process["attempt_id"],
                generation=1,
                descendant_pid=child_pid,
                registrar_pid=registrar.pid,
            )
            proof = registrations["descendants"][0]["registration_proof"]
            self.assertEqual(proof["target_identity"]["pid"], child_pid)
            self.assertEqual(proof["registrar_identity"]["pid"], registrar.pid)
            self.assertEqual(proof["worker_identity"]["pid"], os.getpid())
            self.assertLessEqual(len(proof["target_to_registrar"]), proof["max_depth"])
            before_rejection = (output / "assignment.json").read_bytes()
            with self.assertRaisesRegex(ProcessIdentityError, "not below stable ancestor"):
                register_assignment_ownership(
                    output,
                    assignment_id=process["assignment_id"],
                    attempt_id=process["attempt_id"],
                    generation=1,
                    descendant_pid=unrelated.pid,
                    registrar_pid=registrar.pid,
                )
            self.assertEqual((output / "assignment.json").read_bytes(), before_rejection)
        finally:
            process_path.write_bytes(original_process)
            if registered_identity is not None:
                try:
                    os.kill(registered_identity["pid"], signal.SIGKILL)
                except ProcessLookupError:
                    pass
            for child in (registrar, unrelated):
                if child.poll() is None:
                    child.kill()
                child.wait(timeout=5)
            for identity in (process["identity"], supervisor["identity"]):
                try:
                    observed = read_process_identity(identity["pid"])
                    if compare_process_identity(identity, observed)["match"]:
                        os.kill(identity["pid"], signal.SIGKILL)
                except (ProcessIdentityError, ProcessLookupError):
                    pass

    def test_watchdog_recovers_dead_assignment_and_does_not_repeat_idle_state(self):
        output, process, supervisor = self.launch_attempt(role="worker")
        registry = self.root / "registry.json"
        state = self.root / "watchdog-state.json"
        self.write_registry(registry, [(output, process)])
        self.kill_verified(process["identity"])
        self.kill_verified(supervisor["identity"])
        for identity in (process["identity"], supervisor["identity"]):
            self.wait_not_live(identity)
        environment = dict(os.environ)
        environment["PATH"] = f"{self.root / 'bin'}:{environment['PATH']}"
        command = [
            sys.executable, str(WATCHDOG), "--registry", str(registry),
            "--state", str(state), "--once", "--launch-stale", ".2",
            "--resolve-grace", ".1", "--resolve-kill", ".5",
        ]
        first = subprocess.run(command, env=environment, capture_output=True, text=True, timeout=8)
        self.assertEqual(first.returncode, 0, first.stderr)
        self.assertEqual(json.loads(first.stdout)["event"], "relaunch-started")
        assignment = self.wait_json(output / "assignment.json", lambda value: value.get("status") == "running")
        replacement = Path(assignment["current_attempt_dir"])
        second = subprocess.run(command, env=environment, capture_output=True, text=True, timeout=8)
        self.assertEqual(second.returncode, 0, second.stderr)
        self.assertEqual(json.loads(second.stdout)["event"], "healthy")
        third = subprocess.run(command, env=environment, capture_output=True, text=True, timeout=8)
        self.assertEqual(third.returncode, 0, third.stderr)
        self.assertEqual(third.stdout, "")
        for name in ("process.json", "supervisor.json"):
            identity = json.loads((replacement / name).read_text()).get("identity")
            if identity:
                try:
                    os.kill(identity["pid"], signal.SIGKILL)
                except ProcessLookupError:
                    pass

    def test_watchdog_rejects_poll_interval_above_production_bound(self):
        completed = subprocess.run(
            [
                sys.executable, str(WATCHDOG), "--registry", str(self.root / "missing.json"),
                "--poll-seconds", "60.001", "--once",
            ],
            capture_output=True, text=True, timeout=5,
        )
        self.assertEqual(completed.returncode, 2)
        self.assertIn("--poll-seconds must be in (0, 60.0]", completed.stderr)

    def test_watchdog_audits_other_assignment_when_one_tree_is_partial(self):
        partial_output, partial_process, partial_supervisor = self.launch_attempt(
            role="worker", name="partial"
        )
        dead_output, dead_process, dead_supervisor = self.launch_attempt(
            role="worker", name="dead"
        )
        registry = self.root / "registry.json"
        self.write_registry(
            registry,
            [(partial_output, partial_process), (dead_output, dead_process)],
        )
        self.kill_verified(partial_supervisor["identity"])
        self.kill_verified(dead_process["identity"])
        self.kill_verified(dead_supervisor["identity"])
        for identity in (
            partial_supervisor["identity"], dead_process["identity"], dead_supervisor["identity"]
        ):
            self.wait_not_live(identity)
        environment = dict(os.environ)
        environment["PATH"] = f"{self.root / 'bin'}:{environment['PATH']}"
        completed = subprocess.run(
            [sys.executable, str(WATCHDOG), "--registry", str(registry), "--once", "--workers", "2"],
            env=environment, capture_output=True, text=True, timeout=8,
        )
        self.assertEqual(completed.returncode, 0, completed.stderr)
        events = [json.loads(line)["event"] for line in completed.stdout.splitlines()]
        self.assertEqual(sorted(events), ["partial-tree", "relaunch-started"])
        self.assertTrue(Path(f"/proc/{partial_process['identity']['pid']}").exists())
        self.kill_verified(partial_process["identity"])
        dead_assignment = self.wait_json(dead_output / "assignment.json", lambda value: value.get("status") == "running")
        dead_replacement = Path(dead_assignment["current_attempt_dir"])
        for name in ("process.json", "supervisor.json"):
            identity = json.loads((dead_replacement / name).read_text()).get("identity")
            if identity:
                try:
                    os.kill(identity["pid"], signal.SIGKILL)
                except ProcessLookupError:
                    pass

    def test_unknown_reparented_lock_owner_is_durably_blocked_while_peer_recovers(self):
        blocked_output, blocked_process, blocked_supervisor = self.launch_attempt(
            role="worker", name="unknown-owner"
        )
        safe_output, safe_process, safe_supervisor = self.launch_attempt(
            role="worker", name="safe-peer"
        )
        lock_path = self.root / "unknown-owner.lock"
        pid_path = self.root / "unknown-owner.pid"
        holder_parent = subprocess.Popen(
            [
                sys.executable,
                "-c",
                (
                    "import fcntl,os,pathlib,time; "
                    f"stream=open({str(lock_path)!r},'w'); "
                    "child=os.fork(); "
                    "os._exit(0) if child else None; "
                    "os.setsid(); fcntl.flock(stream,fcntl.LOCK_EX); "
                    f"pathlib.Path({str(pid_path)!r}).write_text(str(os.getpid())); "
                    "time.sleep(60)"
                ),
            ]
        )
        holder_identity = None
        replacements = []
        try:
            holder_parent.wait(timeout=5)
            holder_pid = int(self.wait_text(pid_path))
            holder_identity = read_process_identity(holder_pid)
            self.assertNotEqual(holder_identity["parent_pid"], holder_parent.pid)
            facts = lock_path.stat()
            with lock_path.open("w") as probe:
                with self.assertRaises(BlockingIOError):
                    fcntl.flock(probe, fcntl.LOCK_EX | fcntl.LOCK_NB)
            register_assignment_ownership(
                blocked_output,
                assignment_id=blocked_process["assignment_id"],
                attempt_id=blocked_process["attempt_id"],
                generation=1,
                resource={
                    "resource": "game",
                    "path": str(lock_path),
                    "device": facts.st_dev,
                    "inode": facts.st_ino,
                },
            )
            for identity in (
                blocked_supervisor["identity"], safe_supervisor["identity"],
                blocked_process["identity"], safe_process["identity"],
            ):
                self.kill_verified(identity)
                self.wait_not_live(identity)
            registry = self.root / "unknown-owner-registry.json"
            state = self.root / "unknown-owner-watchdog-state.json"
            self.write_registry(
                registry,
                [(blocked_output, blocked_process), (safe_output, safe_process)],
            )
            observed_resource = [
                {
                    "resource": "game",
                    "state": "observed",
                    "slots": [
                        {
                            "path": str(lock_path),
                            "device": facts.st_dev,
                            "inode": facts.st_ino,
                            "availability": "contended",
                            "metadata_classification": "last-known",
                        }
                    ],
                }
            ]
            environment = {"PATH": f"{self.root / 'bin'}:{os.environ['PATH']}"}
            with mock.patch.dict(os.environ, environment), mock.patch(
                "external_worker_runtime.observe_resource_status",
                return_value=observed_resource,
            ), mock.patch(
                "external_worker_runtime.os.kill", wraps=os.kill
            ) as signal_mock:
                changed = audit_registry_once(
                    registry,
                    state_path=state,
                    resource_lock_dir=self.root,
                    workers=2,
                    lease_timeout_seconds=.5,
                    launch_stale_seconds=.2,
                    resolve_grace_seconds=.1,
                    resolve_kill_seconds=.5,
                )
            self.assertEqual(
                sorted(item["event"] for item in changed),
                ["blocked", "relaunch-started"],
            )
            self.assertFalse(
                any(call.args and call.args[0] == holder_pid for call in signal_mock.call_args_list)
            )
            blocked_assignment = json.loads((blocked_output / "assignment.json").read_text())
            self.assertEqual(blocked_assignment["status"], "blocked")
            self.assertEqual(
                blocked_assignment["blocked_reason"],
                "canonical-resource-owner-unverified",
            )
            self.assertEqual(
                json.loads((blocked_output / "process.json").read_text())["status"],
                "blocked",
            )
            self.assertEqual(
                json.loads((blocked_output / "supervisor.json").read_text())["status"],
                "blocked",
            )
            self.assertTrue((blocked_output / "blocked.json").is_file())
            self.assertEqual(
                json.loads((blocked_output / "quarantine.json").read_text())["status"],
                "non-acceptance",
            )
            self.assertEqual((lock_path.stat().st_dev, lock_path.stat().st_ino), (facts.st_dev, facts.st_ino))
            self.assertTrue(
                compare_process_identity(holder_identity, read_process_identity(holder_pid))["match"]
            )
            safe_assignment = self.wait_json(
                safe_output / "assignment.json", lambda value: value.get("status") == "running"
            )
            replacements.append(Path(safe_assignment["current_attempt_dir"]))
            blocked_bytes = (blocked_output / "blocked.json").read_bytes()
            with mock.patch(
                "external_worker_runtime.observe_resource_status",
                return_value=observed_resource,
            ):
                settled = audit_registry_once(
                    registry,
                    state_path=state,
                    resource_lock_dir=self.root,
                    workers=2,
                    lease_timeout_seconds=.5,
                    launch_stale_seconds=.2,
                    resolve_grace_seconds=.1,
                    resolve_kill_seconds=.5,
                )
                unchanged = audit_registry_once(
                    registry,
                    state_path=state,
                    resource_lock_dir=self.root,
                    workers=2,
                    lease_timeout_seconds=.5,
                    launch_stale_seconds=.2,
                    resolve_grace_seconds=.1,
                    resolve_kill_seconds=.5,
                )
            self.assertEqual([item["event"] for item in settled], ["healthy"])
            self.assertEqual(unchanged, [])
            self.assertEqual((blocked_output / "blocked.json").read_bytes(), blocked_bytes)

            os.kill(holder_pid, signal.SIGTERM)
            self.wait_not_live(holder_identity)
            available_resource = [
                {
                    **observed_resource[0],
                    "slots": [{**observed_resource[0]["slots"][0], "availability": "available"}],
                }
            ]
            with mock.patch.dict(os.environ, environment), mock.patch(
                "external_worker_runtime.observe_resource_status",
                return_value=available_resource,
            ):
                resumed = audit_attempt(
                    blocked_output,
                    resource_lock_dir=self.root,
                    recover=relaunch_assignment,
                    resolve_grace_seconds=.1,
                    resolve_kill_seconds=.5,
                )
            self.assertEqual(resumed["event"], "relaunch-started")
            blocked_assignment = self.wait_json(
                blocked_output / "assignment.json", lambda value: value.get("status") == "running"
            )
            replacements.append(Path(blocked_assignment["current_attempt_dir"]))
            self.assertNotIn("blocked_reason", blocked_assignment)
            self.assertNotIn("blocked_record", blocked_assignment)
            predecessor = blocked_assignment["predecessor_lineage"]
            self.assertEqual(predecessor["attempt_id"], blocked_process["attempt_id"])
            self.assertEqual(predecessor["status"], "interrupted")
            self.assertEqual(
                predecessor["blocked_reason"],
                "canonical-resource-owner-unverified",
            )
            self.assertEqual(predecessor["blocked_record"], str(blocked_output / "blocked.json"))
            self.assertEqual((lock_path.stat().st_dev, lock_path.stat().st_ino), (facts.st_dev, facts.st_ino))
        finally:
            if holder_identity is not None:
                try:
                    current = read_process_identity(holder_identity["pid"])
                    if compare_process_identity(holder_identity, current)["match"]:
                        os.kill(holder_identity["pid"], signal.SIGKILL)
                except Exception:
                    pass
            if holder_parent.poll() is None:
                holder_parent.kill()
            for attempt_dir in replacements:
                for name in ("process.json", "supervisor.json"):
                    path = attempt_dir / name
                    if not path.exists():
                        continue
                    identity = json.loads(path.read_text()).get("identity")
                    if identity:
                        try:
                            current = read_process_identity(identity["pid"])
                            if compare_process_identity(identity, current)["match"]:
                                os.kill(identity["pid"], signal.SIGKILL)
                        except Exception:
                            pass

    def test_stale_recovery_without_process_becomes_actionable_blocked(self):
        output, process, supervisor = self.launch_attempt(role="worker")
        self.kill_verified(process["identity"])
        self.wait_not_live(process["identity"])
        try:
            self.kill_verified(supervisor["identity"])
            self.wait_not_live(supervisor["identity"])
        except (ProcessIdentityError, ProcessLookupError, AssertionError):
            pass
        stale_supervisor = subprocess.Popen(
            [sys.executable, "-c", "import time; time.sleep(60)"],
            start_new_session=True,
        )
        supervisor_record = json.loads((output / "supervisor.json").read_text())
        supervisor_record["identity"] = read_process_identity(stale_supervisor.pid)
        supervisor_record["pid"] = stale_supervisor.pid
        supervisor_record["status"] = "launched"
        (output / "supervisor.json").write_text(json.dumps(supervisor_record) + "\n")
        (output / "process.json").unlink()
        assignment_path = output / "assignment.json"
        assignment = json.loads(assignment_path.read_text())
        assignment.update(
            {
                "status": "recovering",
                "updated_utc": (datetime.now(timezone.utc) - timedelta(seconds=10)).isoformat(),
            }
        )
        assignment_path.write_text(json.dumps(assignment) + "\n")
        result = audit_attempt(
            output,
            launch_stale_seconds=.01,
            resolve_grace_seconds=.1,
            resolve_kill_seconds=.5,
        )
        self.assertEqual(result["event"], "blocked")
        self.assertEqual(result["reason"], "recovering-timeout")
        self.wait_not_live(supervisor_record["identity"])
        stale_supervisor.wait(timeout=2)
        blocked = json.loads(assignment_path.read_text())
        self.assertEqual(blocked["status"], "blocked")
        self.assertEqual(blocked["blocked_reason"], "recovering-timeout")


if __name__ == "__main__":
    unittest.main()
