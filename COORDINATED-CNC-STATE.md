# Coordinated CNC State

- Round ID: `20260807-bug-polish-03`
- Phase: `five Sol-high isolated workers resumed after process-tree interruption`
- Common base branch: `agent/cnc-20260807-bug-polish-02-release`
- Common base SHA: `468ee64f5a0f9a9e19e260e5c5943e6e878f4705`
- Coordinator model: `gpt-5.6-terra` / `medium` (user identifies this current
  coordinator session as Luna medium); delegated worker tiers remain pinned
  independently
- Game slots: `2` for ordinary/full MAX simulations; `3` only for short,
  tightly bounded custom fixtures
- Large-build slots: `1`
- Lock directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-03/locks`
- Persistent policy scratchpad: `/root/github/LibertyDawn/.agents/references/LIBERTY-DAWN-POLICY-SCRATCHPAD.md`
- Cross-round policy lock directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/shared-locks`
- Prior release: [product PR #90](https://github.com/Realpra1/LibertyDawn/pull/90)
  at final task-status head `468ee64f5a0f`; merged by the user into `bleed` at
  2026-08-07 18:20:52 UTC after 2× Linux/2× Windows CI passed
- Release candidate: `none`
- Release PR: `none`

## Workers

| Worker | Task | Branch | Worktree | State | Process/result | PR | Review | Integrated status |
|---|---|---|---|---|---|---|---|---|
| 1 | CNC-45 Economy troop production/use | `agent/round-20260807-cnc45-economy-troop-use` | `.worktrees/coordinated-cnc/20260807-bug-polish-03/workers/worker-1-cnc45` | assignment `92edf1f42a`; no prerequisite; preserve CNC-43/CNC-36 ownership surfaces | `roles/worker-1-resume-1/process.json` running PID `1032810` | | | |
| 2 | CNC-46 Defense clusters | `agent/round-20260807-cnc46-defense-clusters` | `.worktrees/coordinated-cnc/20260807-bug-polish-03/workers/worker-2-cnc46` | assignment `1cea87332d`; preserve CNC-52 enclosure ownership and keep CNC-91 sparse towers subordinate | `roles/worker-2-resume-1/process.json` running PID `1032812` | | | |
| 3 | CNC-47 Repeatable performance baseline | `agent/round-20260807-cnc47-performance-baseline` | `.worktrees/coordinated-cnc/20260807-bug-polish-03/workers/worker-3-cnc47` | assignment `8614808bc8`; pure measurement/tooling; outputs feed CNC-48/CNC-49 | `roles/worker-3-resume-1/process.json` running PID `1032866` | | | |
| 4 | CNC-50 Late-game engineer stall recovery | `agent/round-20260807-cnc50-engineer-stall-recovery` | `.worktrees/coordinated-cnc/20260807-bug-polish-03/workers/worker-4-cnc50` | assignment `49c24d7d29`; preserve CNC-39/CNC-39A; CNC-59 out of scope; named manual evidence absent but non-blocking | `roles/worker-4-resume-1/process.json` running PID `1032864` | | | |
| 5 | CNC-52 Starting-Fact wall hole prevention/repair | `agent/round-20260807-cnc52-first-fact-wall-holes` | `.worktrees/coordinated-cnc/20260807-bug-polish-03/workers/worker-5-cnc52` | assignment `d32362502b`; first-Fact maintenance before tick 7,500; CNC-46 owns general wall self-blocking/selling | `roles/worker-5-resume-1/process.json` running PID `1032916` | | | |

## Release rounds

| RC | Head | Included heads | Repair heads | Build/checks | Integrated tests | Result |
|---|---|---|---|---|---|---|

## Resume note

Record only routing, process identity, branch heads, phase, blockers, and concise
results here. Keep task specifications and detailed evidence in worker state and
reports. Round 02's durable details remain in
`COORDINATED-CNC-ROUNDS/20260807-bug-polish-02/` and its final coordinator history.

User acceptance clarification: require correct save/load, replay/no-desync
behavior, and sensible restored AI state; do not require a loaded game to reproduce
an uninterrupted game's exact actor decisions or ticks unless a task-specific
persisted invariant expressly needs it.

Terminal directive (supersedes the earlier pause directive): complete, review,
integrate, and adversarially test this five-task round; open its final
cumulative product release PR into `bleed`; then continue with another fresh
five-task coordinator cycle instead of pausing.

Recovery note (2026-08-07 16:38 UTC): the shared `policy-scratchpad` lock left by
completed speccer PID `1006013` was verified dead with no active policy-role
process, then moved into the ignored shared-lock `stale/` quarantine. No
canonical scratchpad content was changed; policy consultations may proceed.

Task routing note (2026-08-07 16:42 UTC): a fresh Task Maker recorded `CNC-94`
as a high-priority coordination-infrastructure bug/polish task after dead
game/build locks were also observed. It covers automatic, safe stale-lock
reclamation without weakening live-owner exclusivity; task-sheet commit
`c6d133cc32` placed it immediately before pinned final `CNC-26C`.

Interruption recovery (2026-08-07 18:09 UTC): the machine had not rebooted, but
all five original worker/supervisor process pairs and their game/build children
had been terminated around 16:48–16:49 UTC; tmux also had no server. All five
assigned worktrees retained their cycle-one edits. Three dead game locks were
verified ownerless and quarantined, then five fresh Sol-high workers were
launched from the same durable state files under `roles/worker-*-resume-1/`.
No task was reselected and no worktree content was discarded.

Task routing note (2026-08-07 18:11 UTC): a fresh Task Maker recorded `CNC-95`
as a separate high-priority coordination-infrastructure recovery task at commit
`9bf0ec9050`, immediately before pinned final `CNC-26C`. It covers dead-worker
detection, truthful process status, safe exact-assignment relaunch, preserved
worktree changes, partial-test handling, and avoiding duplicate cycle counts.

Model audit (2026-08-07 18:20 UTC): active-round external envelopes match the
role table. Commenters and ordinary Policy Reviewers use Terra medium;
spec-policy consultation uses Sol high; workers use Sol high. Task Readers and
Task Makers were fresh Terra-medium native roles; Speccers were Sol xhigh. No
cycle reviewer has launched yet; it must use Terra medium. Final PR Reviewers
and the Integrator remain Sol high by design. The coordinator is now the
user-selected Luna-medium/Terra-medium session. Dead pre-interruption worker
envelopes are historical records, not additional running Sol sessions.

Task routing note (2026-08-07): a fresh Terra-medium Task Maker recorded two
separate pending user bug reports at task-sheet commit `281bc8d23c`: `CNC-96`
for periodic freezes on very old hardware (linked to CNC-47 history), and
`CNC-97` for suspected failure to recognize new aircraft husks as Engineer-
restorable targets (linked to CNC-44 history). Neither was deduplicated. CNC-96
can be sharpened later with CPU/GPU/RAM/OS, map/match conditions, freeze
frequency, and freeze duration; absence of those details does not alter the
active Round 03 batch.

CNC-96 evidence updates: task-sheet commits `2b7e3b3299` and `a128a2f1ad`
record that Economy guard squads appear functionally sound but are not blamed
for the freeze. Diagnosis remains component-agnostic and must compare stronger
hardware plus CPU, memory/GC, simulation load, and possible playtest debug/report
disk-I/O bursts under matched scenarios before assigning a cause.

CNC-96 scheduling hypothesis update: task-sheet commit `3506efaada` records
that nominally spaced one- or two-second AI jobs may remain phase-aligned and
overload the same recurring simulation tick. If per-tick attribution confirms
this, preserve average decision cadence while assigning simple stable offsets
so different squads, modules, and players plan on different ticks; avoid random
jitter, globally slower AI, balance changes, or unnecessary scheduler theory.

CNC-96 guard-planning hypothesis update: task-sheet commit `a103b7c2a8`
records a conditional total-work reduction only if profiling implicates guard
planning. Avoid one expensive independent planner per Harvester; prefer shared
local Tiberium-field guard groups, bounded low-frequency enemy checks, and
ordinary move/guard/attack orders. This does not presume Economy squads are the
freeze cause, and requires behavioral control comparison before adoption.

Lock-scope correction (2026-08-07): inspection of the actual
`with_resource_slots.py` helper showed that slot files use kernel `fcntl.flock`;
JSON/PID text is diagnostic only, and file persistence after process exit does
not hold a slot. CNC-94 was corrected by Task Maker commit `599a222fdf` to cover
misleading metadata and inconsistent round/shared lock namespaces, with tests
based on actual flock ownership and safe process death rather than JSON cleanup.

Task routing note: fresh Task Maker commit `fc16244bd1` corrected `CNC-85` as
pending. The task now distinguishes eligible husk restoration into a live unit
from impossible cross-tech production capture, and limits its stealth-tank
requirement to air-squad hunt/target priority `3000`; build priority and balance
are explicitly out of scope. Active Round 03 selection was unchanged.

Task routing note: fresh Task Maker commit `9a9f4b1f43` split `CNC-86A` from
`CNC-86`. `CNC-86A` is pending and owns the blue-tiberium-loaded harvester
shot-burst behavior; `CNC-86` remains pending with only its lobby-option and
detonation contract. No active Round 03 task was changed, and no completed
merged task was eligible for history migration.

Task routing note: fresh Task Maker commit `7be701ec66` clarified pending
`CNC-86` lobby semantics. `Unstable tiberium` is positive when selected and
controls spontaneous instability; `tiberium explosions` is positive when
selected and controls loose tiberium reacting to shots. Killed or deliberately
detonated tiberium-loaded harvesters always explode regardless of either option.
Active Round 03 selection was unchanged.
