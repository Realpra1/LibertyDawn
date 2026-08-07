# CNC-87 task report

## Current result

Implementation and adversarial testing are complete in three product-change
cycles. The result establishes an enforced external analysis-role path, real installed-Codex
parser validation, one protected capacity-one large-build entry policy, and
complete assigned-process-tree supervision. Four real CNC games and their fresh
Commenter paths pass.

## Design choices and assumptions

- `launch_role.py` remains the sole owner of role model/reasoning, sandbox,
  session directory, strict analysis envelope, prompt, output, and supervision.
  Its no-agent validation submits every production option to the installed parser
  with only the free-form prompt replaced by parser help, then proves the same
  parser rejects legacy `-a never`.
- `with_resource_slots.py --large-build-entry <worker|reviewer|integrator>` owns
  resource `large-build`, capacity one, and canonical
  `large-build-1.lock`. Generic `--resource large-build` is rejected. The legacy
  direct-flock `large-build.lock` is a mixed-namespace error and is never unlinked
  automatically.
- A forked guardian inherits the kernel lock and acts as a Linux child subreaper.
  This lets an abrupt wrapper death leave a live guardian holding the reservation.
  After the foreground command exits, short descendants receive a one-second
  completion grace; persistent assigned descendants receive targeted SIGTERM and
  two seconds before SIGKILL. Release follows complete reaping. This is necessary
  for reusable .NET/MSBuild nodes observed in a real build.
- Game reservations retain their generic indexed capacity-two namespace and are
  independent of the large-build policy.

## Evidence to date

- Installed parser: `codex-cli 0.146.0`; protected argv exits 0 in no-agent mode,
  legacy `-a never` exits 2 with `unexpected argument '-a'`, and no agent event or
  last-message artifact is created.
- Base split-lock control: Integrator direct `large-build.lock` entered in 14.8 ms
  while Worker-style `large-build-1.lock` was live; maximum concurrency was 2 and
  both filenames existed.
- Base process-tree control: the wrapper returned in 0.0712 s, a second holder
  completed in 0.0530 s, and the detached descendant was still unfinished.
- Changed focused suite: 16 tests pass in 6.01 s with ResourceWarnings treated as
  errors. Both Worker/Integrator orders serialize; stale metadata reacquires;
  child exit 7 propagates; abrupt wrapper death does not release a live tree;
  mixed names fail without cleanup; two game slots remain independent while a
  third queues; short detached work completes and persistent work is terminated
  before release.
- Real guarded build: incremental `make -j2` passed with zero warnings/errors.
  Foreground exit was followed by targeted reaping of persistent MSBuild PIDs
  736650, 736651, and 736655, then command outcome 0 and release.
- Scenario 1 lock ordering: Worker tree exit monotonic 237101.793; Integrator
  acquire 237101.804. A game reservation acquired independently while Worker held.
- Scenario 1 game: Empire Earth seed 87001, ordinary SkyNet versus Brutalis,
  headless MAX, tick 10000, exit 0, 40.662 s, 245.904 valid ticks/s, no fatal or
  desync signal. An earlier `--no-xvfb` tick-0 platform failure was an invalid
  harness attempt and is not counted.
- Real Commenter receipt and narrative:
  `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-02/analysis/worker-1-cnc87/cycle-01-game/commenter/`.
  `process.json` records `gpt-5.6-terra`/`medium`, workspace-write, exit 0;
  `NARRATIVE.md` factually confirms map, seed, bots, MAX, tick, clean outcome, and
  evidence limitations.
- Scenario 2 reversed order/failure: Integrator child and reported outcome were
  exactly 7 at monotonic 237500.219; queued Worker acquired at 237500.233 and
  exited 0. Stale canonical owner text did not act as occupancy. Empire Earth seed
  87002 reached tick 15000, exit 0, in 72.379 s. The fresh Commenter receipt under
  `analysis/worker-1-cnc87/scenario-02-failure-stale/commenter/` is Terra medium,
  exit 0, and factually confirms the run.
- Scenario 3 cancellation/mixed namespace: a fixture containing both
  `large-build.lock` and `large-build-1.lock` failed with exit 78, both paths and
  remediation named, and neither inode removed. Supported SIGTERM returned 143;
  detached PID 751035 was targeted, reaped, and absent before release at
  237768.708; Integrator entered at 237768.771. Empire Earth seed 87003 ran to
  natural game over at tick 20000, exit 0, in 63.999 s. Its fresh Commenter receipt
  under `analysis/worker-1-cnc87/scenario-03-cancel-natural/commenter/` is exact.
- Final literal acceptance on cycle 3: Worker foreground exit was followed by
  detached-child completion at 238291.196 and release at 238291.198; Integrator
  acquired at 238291.223. The independent game recorded only its actually held
  `game-1.lock`, then seed 87001 reached tick 10000 and exited 0 in 73.933 s. The
  final Commenter `process.json` explicitly records Terra medium, workspace-write,
  exact session/output directories, unsupervised foreground execution, and exit 0.
- Final local gates: 20-test Python portfolio passed in 4.91 s, then affected
  cycle-3 suite passed 16/16 in 6.01 s. Final-head guarded `make test` passed all
  CNC MiniYAML/maps and reaped MSBuild PIDs 767032/767034/767035 before release.
  Guarded `make check` passed the Debug warning-as-error build and both interface
  checks, reaping PIDs 767800/767801/767805 before release. Both builds reported
  zero warnings and zero errors.

## Performance and determinism

The focused suite remains under seven seconds and lock waiting polls at 50 ms with
bounded timeouts. Diagnostics emit one JSON line per state transition rather than
per poll. No game or AI code changed. All four valid games produced configured
map/seed/bot/MAX/tick or natural-end evidence; strategic outcome is not an
acceptance metric. Timing varied with shared-host load (135.25 to 312.486 valid
ticks/s) without startup, fatal, desync, or termination regression.

## Deferred work and remaining risks

- Draft PR #86 targets the recorded common base:
  `https://github.com/Realpra1/LibertyDawn/pull/86`. Complete the final independent
  Sol-high review, required Linux/Windows GitHub checks, and publication receipt.
- Evaluate portability explicitly: complete process-tree enforcement uses Linux
  `prctl` and `/proc`, matching the coordinated Linux host contract.
