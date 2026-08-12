#!/usr/bin/env bash
set -euo pipefail

repo="/root/github/LibertyDawn"
lock="/tmp/libertydawn-cnc-coordinator-watchdog.lock"
log_dir="$repo/.worktrees/coordinated-cnc/watchdog"
mkdir -p "$log_dir"
exec 9>"$lock"
flock -n 9 || exit 0

stamp="$(date -u +%Y%m%dT%H%M%SZ)"
timeout 240 /root/.local/bin/codex exec --ephemeral --json \
  -C "$repo" -s danger-full-access -c 'approval_policy="never"' \
  -m gpt-5.6-luna -c 'model_reasoning_effort="medium"' \
  -o "$log_dir/$stamp.json" \
  'Read .agents/skills/coordinate-cnc-development/SKILL.md and COORDINATED-CNC-STATE.md. Audit every active task stream and external process record, then resume the active round: launch the next authorized role/test for any completed or stalled stream, including eligible Luna cycles 6-15 for minor fixes/testing after primary cycles, never duplicate a healthy process, and update only coordinator state. Do not merely report status and do not create tasks. If all work is healthy, perform the audit and exit.' \
  || true
