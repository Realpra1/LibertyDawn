#!/usr/bin/env python3

import json
import os
import select
import shutil
import signal
import subprocess
import sys
import tempfile
import time
import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
HELPER = (
    REPO_ROOT
    / ".agents/skills/coordinate-cnc-development/scripts/with_resource_slots.py"
)


class ResourceSlotsTest(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="cnc87-slots-test-")
        self.root = Path(self.temporary.name)
        self.repository = self.root / "repository"
        self.helper = (
            self.repository
            / ".agents/skills/coordinate-cnc-development/scripts/with_resource_slots.py"
        )
        self.helper.parent.mkdir(parents=True)
        shutil.copy2(HELPER, self.helper)
        subprocess.run(
            ["git", "init", "-q", str(self.repository)], check=True, timeout=3
        )
        self.lock_dir = self.repository / ".agents" / "locks"
        self.sequence = 0
        self.processes = []
        self.stderr_buffers = {}
        self.stderr_lines = {}

    def tearDown(self):
        for process in reversed(self.processes):
            if process.poll() is None:
                process.terminate()
            try:
                process.communicate(timeout=3)
            except subprocess.TimeoutExpired:
                if process.poll() is None:
                    process.kill()
                process.communicate(timeout=3)
        self.temporary.cleanup()

    def track(self, process):
        self.processes.append(process)
        return process

    def large_build(self, entry_role, command, timeout=3):
        return [
            sys.executable,
            str(self.helper),
            "--lock-dir",
            str(self.lock_dir),
            "--large-build-entry",
            entry_role,
            "--timeout",
            str(timeout),
            "--",
            *command,
        ]

    def generic(self, resource, capacity, command, timeout=3):
        return [
            sys.executable,
            str(self.helper),
            "--lock-dir",
            str(self.lock_dir),
            "--resource",
            resource,
            "--capacity",
            str(capacity),
            "--slots",
            "1",
            "--timeout",
            str(timeout),
            "--",
            *command,
        ]

    def status(self, resource, capacity, lock_dir=None):
        return [
            sys.executable,
            str(self.helper),
            "--lock-dir",
            str(lock_dir or self.lock_dir),
            "--resource",
            resource,
            "--capacity",
            str(capacity),
            "--status",
        ]

    def marker_command(self, entered, release=None, exited=None, child_delay=None):
        code = [
            "import pathlib,subprocess,sys,time",
            f"pathlib.Path({str(entered)!r}).write_text(str(time.monotonic()))",
        ]
        if child_delay is not None:
            child_code = (
                "import pathlib,time; time.sleep("
                + repr(child_delay)
                + "); pathlib.Path("
                + repr(str(exited))
                + ").write_text(str(time.monotonic()))"
            )
            code.append(
                "subprocess.Popen([sys.executable,'-c',"
                + repr(child_code)
                + "], start_new_session=True, stdin=subprocess.DEVNULL, "
                "stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)"
            )
        elif release is not None:
            code.append(
                f"p=pathlib.Path({str(release)!r}); deadline=time.monotonic()+10"
            )
            code.append(
                "\nwhile not p.exists() and time.monotonic() < deadline: time.sleep(.01)"
            )
            code.append("\nassert p.exists(), 'release marker timeout'")
            if exited is not None:
                code.append(
                    f"pathlib.Path({str(exited)!r}).write_text(str(time.monotonic()))"
                )
        return [sys.executable, "-c", ";".join(code)]

    def wait_path(self, path, timeout=3):
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            if path.exists():
                return
            time.sleep(.01)
        self.fail(f"timed out waiting for {path}")

    def wait_event(self, process, event, timeout=3):
        deadline = time.monotonic() + timeout
        descriptor = process.stderr.fileno()
        buffer = self.stderr_buffers.setdefault(process.pid, bytearray())
        lines = self.stderr_lines.setdefault(process.pid, [])
        while time.monotonic() < deadline:
            while b"\n" in buffer:
                raw_line, _, remainder = buffer.partition(b"\n")
                buffer[:] = remainder
                line = raw_line.decode("utf-8", errors="replace") + "\n"
                lines.append(line)
                try:
                    value = json.loads(line.removeprefix("resource-slot "))
                except json.JSONDecodeError:
                    continue
                if value.get("event") == event:
                    return value, list(lines)
            ready, _, _ = select.select([descriptor], [], [], .1)
            if not ready:
                continue
            chunk = os.read(descriptor, 65536)
            if not chunk:
                break
            buffer.extend(chunk)
        self.fail(f"timed out waiting for event {event!r}; lines={lines!r}")

    def assert_cross_path_serializes(self, first_role, second_role):
        self.sequence += 1
        prefix = f"order-{self.sequence}"
        first_enter = self.root / f"{prefix}-{first_role}-enter"
        first_exit = self.root / f"{prefix}-{first_role}-exit"
        release = self.root / f"{prefix}-{first_role}-release"
        second_enter = self.root / f"{prefix}-{second_role}-enter"
        first = self.track(subprocess.Popen(
            self.large_build(
                first_role,
                self.marker_command(first_enter, release, first_exit),
            ),
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
        ))
        self.wait_path(first_enter)
        second = self.track(subprocess.Popen(
            self.large_build(
                second_role,
                self.marker_command(second_enter),
            ),
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
        ))
        queued, _ = self.wait_event(second, "queued")
        self.assertEqual(queued["entry_role"], second_role)
        self.assertFalse(second_enter.exists())
        release.write_text("release\n", encoding="utf-8")
        first_output = first.communicate(timeout=3)
        second_output = second.communicate(timeout=3)
        self.assertEqual(first.returncode, 0, first_output)
        self.assertEqual(second.returncode, 0, second_output)
        self.assertGreaterEqual(
            float(second_enter.read_text()), float(first_exit.read_text())
        )

    def test_worker_and_integrator_paths_serialize_in_both_orders(self):
        self.assert_cross_path_serializes("worker", "integrator")
        self.assert_cross_path_serializes("integrator", "worker")
        self.assertEqual(
            sorted(path.name for path in self.lock_dir.iterdir()),
            ["large-build-1.lock"],
        )

    def test_detached_descendant_keeps_reservation_until_tree_exit(self):
        entered = self.root / "parent-enter"
        descendant_exit = self.root / "descendant-exit"
        started = time.monotonic()
        completed = subprocess.run(
            self.large_build(
                "worker",
                self.marker_command(
                    entered, exited=descendant_exit, child_delay=.35
                ),
            ),
            capture_output=True,
            text=True,
            timeout=3,
        )
        elapsed = time.monotonic() - started
        self.assertEqual(completed.returncode, 0, completed.stderr)
        self.assertTrue(descendant_exit.exists(), completed.stderr)
        self.assertGreaterEqual(elapsed, .30)
        self.assertIn('"event": "tree-resolved"', completed.stderr)

    def test_persistent_descendant_is_terminated_before_release(self):
        entered = self.root / "persistent-parent-enter"
        pid_file = self.root / "persistent-child-pid"
        child_code = (
            "import os,pathlib,time; pathlib.Path("
            + repr(str(pid_file))
            + ").write_text(str(os.getpid())); time.sleep(60)"
        )
        parent_code = (
            "import pathlib,subprocess,sys,time; pathlib.Path("
            + repr(str(entered))
            + ").write_text(str(time.monotonic())); "
            "subprocess.Popen([sys.executable,'-c',"
            + repr(child_code)
            + "], start_new_session=True, stdin=subprocess.DEVNULL, "
            "stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)"
        )
        completed = subprocess.run(
            self.large_build("worker", [sys.executable, "-c", parent_code]),
            capture_output=True,
            text=True,
            timeout=5,
        )
        self.assertEqual(completed.returncode, 0, completed.stderr)
        child_pid = int(pid_file.read_text())
        with self.assertRaises(ProcessLookupError):
            os.kill(child_pid, 0)
        self.assertIn(f'"terminated_pids": [{child_pid}]', completed.stderr)

    def test_failed_and_stale_holders_release_without_false_occupancy(self):
        failed = subprocess.run(
            self.large_build("integrator", [sys.executable, "-c", "raise SystemExit(7)"]),
            capture_output=True,
            text=True,
            timeout=3,
        )
        self.assertEqual(failed.returncode, 7, failed.stderr)
        canonical = self.lock_dir / "large-build-1.lock"
        canonical.write_text('{"pid": 999999, "status": "stale"}\n', encoding="utf-8")
        recovered = subprocess.run(
            self.large_build("worker", [sys.executable, "-c", "print('recovered')"]),
            capture_output=True,
            text=True,
            timeout=3,
        )
        self.assertEqual(recovered.returncode, 0, recovered.stderr)
        self.assertEqual(recovered.stdout.strip(), "recovered")

    def test_abrupt_wrapper_death_does_not_release_live_command_tree(self):
        first_enter = self.root / "abandoned-enter"
        first_exit = self.root / "abandoned-exit"
        first = self.track(subprocess.Popen(
            self.large_build(
                "worker",
                self.marker_command(
                    first_enter, exited=first_exit, child_delay=.40
                ),
            ),
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        ))
        self.wait_path(first_enter)
        os.kill(first.pid, signal.SIGKILL)
        first.wait(timeout=1)
        second_enter = self.root / "after-abandon-enter"
        second = self.track(subprocess.Popen(
            self.large_build("integrator", self.marker_command(second_enter)),
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
        ))
        self.wait_event(second, "queued")
        self.assertFalse(second_enter.exists())
        self.wait_path(first_exit)
        output = second.communicate(timeout=3)
        self.assertEqual(second.returncode, 0, output)
        self.assertGreaterEqual(
            float(second_enter.read_text()), float(first_exit.read_text())
        )

    def test_supported_cancellation_resolves_tree_and_preserves_signal_outcome(self):
        entered = self.root / "cancel-enter"
        command = [
            sys.executable,
            "-c",
            "import pathlib,time; pathlib.Path("
            + repr(str(entered))
            + ").write_text(str(time.monotonic())); time.sleep(60)",
        ]
        holder = self.track(subprocess.Popen(
            self.large_build("integrator", command),
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
        ))
        self.wait_path(entered)
        holder.terminate()
        output = holder.communicate(timeout=5)
        self.assertEqual(holder.returncode, 143, output)
        self.assertIn('"event": "cancellation-requested"', output[1])
        self.assertIn('"event": "tree-resolved"', output[1])
        next_holder = subprocess.run(
            self.large_build("worker", [sys.executable, "-c", "print('next')"]),
            capture_output=True,
            text=True,
            timeout=3,
        )
        self.assertEqual(next_holder.returncode, 0, next_holder.stderr)
        self.assertEqual(next_holder.stdout.strip(), "next")

    def test_timeout_and_invalid_policy_do_not_claim_success(self):
        entered = self.root / "timeout-holder-enter"
        release = self.root / "timeout-holder-release"
        holder = self.track(subprocess.Popen(
            self.large_build("worker", self.marker_command(entered, release)),
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
        ))
        self.wait_path(entered)
        timed_out = subprocess.run(
            self.large_build(
                "integrator", [sys.executable, "-c", "print('unsafe')"], timeout=.1
            ),
            capture_output=True,
            text=True,
            timeout=2,
        )
        self.assertEqual(timed_out.returncode, 75, timed_out.stderr)
        self.assertIn('"event": "timed-out"', timed_out.stderr)
        self.assertNotIn('"event": "acquired"', timed_out.stderr)
        self.assertNotIn("unsafe", timed_out.stdout)
        release.write_text("release\n", encoding="utf-8")
        holder.communicate(timeout=3)
        self.assertEqual(holder.returncode, 0)

        invalid = subprocess.run(
            [
                sys.executable,
                str(self.helper),
                "--lock-dir",
                str(self.lock_dir),
                "--resource",
                "large-build",
                "--capacity",
                "1",
                "--",
                sys.executable,
                "-c",
                "print('bypass')",
            ],
            capture_output=True,
            text=True,
        )
        self.assertEqual(invalid.returncode, 2)
        self.assertIn("large-build policy is protected", invalid.stderr)
        self.assertNotIn("bypass", invalid.stdout)

    def test_inaccessible_lock_path_is_actionable(self):
        not_a_directory = self.root / "not-a-directory"
        not_a_directory.write_text("file\n", encoding="utf-8")
        completed = subprocess.run(
            [
                sys.executable,
                str(self.helper),
                "--lock-dir",
                str(not_a_directory),
                "--large-build-entry",
                "worker",
                "--",
                sys.executable,
                "-c",
                "print('unsafe')",
            ],
            capture_output=True,
            text=True,
        )
        self.assertEqual(completed.returncode, 78)
        self.assertIn("Resource reservation rejected", completed.stderr)
        self.assertNotIn("unsafe", completed.stdout)

    def test_mixed_namespace_is_rejected_without_cleanup(self):
        self.lock_dir.mkdir()
        legacy = self.lock_dir / "large-build.lock"
        canonical = self.lock_dir / "large-build-1.lock"
        legacy.write_text("legacy\n", encoding="utf-8")
        canonical.write_text("canonical\n", encoding="utf-8")
        completed = subprocess.run(
            self.large_build("worker", [sys.executable, "-c", "print('unsafe')"]),
            capture_output=True,
            text=True,
            timeout=3,
        )
        self.assertNotEqual(completed.returncode, 0)
        self.assertIn(str(legacy), completed.stderr)
        self.assertIn(str(canonical), completed.stderr)
        self.assertTrue(legacy.exists())
        self.assertTrue(canonical.exists())
        self.assertNotIn("unsafe", completed.stdout)

    def test_game_slots_remain_independent_and_capacity_two(self):
        build_enter = self.root / "build-enter"
        build_release = self.root / "build-release"
        build = self.track(subprocess.Popen(
            self.large_build(
                "worker", self.marker_command(build_enter, build_release)
            ),
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
        ))
        self.wait_path(build_enter)
        games = []
        releases = []
        for index in range(2):
            entered = self.root / f"game-{index}-enter"
            release = self.root / f"game-{index}-release"
            process = self.track(subprocess.Popen(
                self.generic("game", 2, self.marker_command(entered, release)),
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                text=True,
            ))
            self.wait_path(entered)
            games.append(process)
            releases.append(release)
        game_owners = [
            json.loads((self.lock_dir / f"game-{index}.lock").read_text())
            for index in (1, 2)
        ]
        self.assertEqual(
            {tuple(owner["lock_paths"]) for owner in game_owners},
            {
                (str(self.lock_dir / "game-1.lock"),),
                (str(self.lock_dir / "game-2.lock"),),
            },
        )
        third_enter = self.root / "game-2-enter"
        third = self.track(subprocess.Popen(
            self.generic("game", 2, self.marker_command(third_enter)),
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
        ))
        self.wait_event(third, "queued")
        self.assertFalse(third_enter.exists())
        releases[0].write_text("release\n", encoding="utf-8")
        self.assertEqual(games[0].communicate(timeout=3)[0], "")
        self.assertEqual(games[0].returncode, 0)
        self.assertEqual(third.communicate(timeout=3)[0], "")
        self.assertEqual(third.returncode, 0)
        releases[1].write_text("release\n", encoding="utf-8")
        self.assertEqual(games[1].communicate(timeout=3)[0], "")
        self.assertEqual(games[1].returncode, 0)
        build_release.write_text("release\n", encoding="utf-8")
        self.assertEqual(build.communicate(timeout=3)[0], "")
        self.assertEqual(build.returncode, 0)

    def test_registered_resources_reject_split_namespace_and_wrong_capacity(self):
        for resource, capacity in (("game", 2), ("policy-scratchpad", 1)):
            split = subprocess.run(
                self.status(resource, capacity, self.root / "round-local-locks"),
                capture_output=True,
                text=True,
                timeout=3,
            )
            self.assertEqual(78, split.returncode, split.stderr)
            self.assertIn("rejected conflicting namespace", split.stderr)
            self.assertFalse((self.root / "round-local-locks").exists())

            wrong = subprocess.run(
                self.status(resource, capacity + 1),
                capture_output=True,
                text=True,
                timeout=3,
            )
            self.assertEqual(78, wrong.returncode, wrong.stderr)
            self.assertIn("rejected caller capacity", wrong.stderr)

    def test_status_uses_flock_and_labels_retained_json_last_known(self):
        entered = self.root / "status-holder-enter"
        release = self.root / "status-holder-release"
        holder = self.track(
            subprocess.Popen(
                self.generic(
                    "policy-scratchpad",
                    1,
                    self.marker_command(entered, release),
                ),
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                text=True,
            )
        )
        self.wait_path(entered)
        path = self.lock_dir / "policy-scratchpad-1.lock"
        before = path.stat()
        live = subprocess.run(
            self.status("policy-scratchpad", 1),
            capture_output=True,
            text=True,
            timeout=3,
        )
        self.assertEqual(0, live.returncode, live.stderr)
        self.assertIn('"availability": "contended"', live.stderr)
        self.assertIn('"metadata_classification": "last-known"', live.stderr)
        self.assertEqual((before.st_dev, before.st_ino), (path.stat().st_dev, path.stat().st_ino))

        release.write_text("release\n", encoding="utf-8")
        holder.communicate(timeout=3)
        retained = path.read_text(encoding="utf-8")
        available = subprocess.run(
            self.status("policy-scratchpad", 1),
            capture_output=True,
            text=True,
            timeout=3,
        )
        self.assertEqual(0, available.returncode, available.stderr)
        self.assertIn('"availability": "available"', available.stderr)
        self.assertIn('"metadata_classification": "last-known"', available.stderr)
        self.assertIn(retained.strip().replace('"', '\\"')[:20], available.stderr)
        self.assertEqual((before.st_dev, before.st_ino), (path.stat().st_dev, path.stat().st_ino))

    def test_production_caller_documents_use_one_global_namespace(self):
        relative_paths = (
            ".agents/skills/coordinate-cnc-development/SKILL.md",
            ".agents/skills/spec-cnc-task/assets/WORKER-STATE.template.md",
            ".agents/skills/integrate-cnc-release/SKILL.md",
            ".agents/skills/review-cnc-pr/SKILL.md",
        )
        documents = {
            path: (REPO_ROOT / path).read_text(encoding="utf-8")
            for path in relative_paths
        }
        combined = "\n".join(documents.values())
        self.assertNotIn("absolute-round-lock-dir", combined)
        self.assertNotIn("round's absolute lock directory", combined)
        self.assertIn("<repository-root>/.agents/locks", combined)
        self.assertIn("{{REPOSITORY_ROOT}}/.agents/locks", combined)
        self.assertIn("--large-build-entry worker", combined)
        self.assertIn("--large-build-entry reviewer", combined)
        self.assertIn("--large-build-entry integrator", combined)
        self.assertIn("--resource policy-scratchpad --capacity 1", combined)
        self.assertIn("--resource game --capacity 2", combined)


if __name__ == "__main__":
    unittest.main()
