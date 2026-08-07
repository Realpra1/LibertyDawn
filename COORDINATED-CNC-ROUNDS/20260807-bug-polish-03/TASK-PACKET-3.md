# Task packet
- Task ID: CNC-47
- Title: Repeatable performance baseline
- Status at selection: pending
- Required base/prerequisites: Use the coordinated round's recorded common base, `468ee64f5a0f9a9e19e260e5c5943e6e878f4705` (`agent/cnc-20260807-bug-polish-02-release`). No explicit prerequisite is stated in the task sheet.
- Related active tasks or PRs: CNC-45 and CNC-46 are claimed by slots 1 and 2 for this round. CNC-40, CNC-41, CNC-42, CNC-44, and CNC-87 are excluded in-progress prior-round work. CNC-48 integration and CNC-49 lag reduction are pending downstream consumers of this baseline.
- Cross-worker concern: Keep the baseline independent of CNC-45/CNC-46 behavior changes and record the exact checked-out revision/configuration for every result so later integration can distinguish baseline variance from task effects.

## Authoritative task text

**Repeatable performance baseline.** Create a repeatable late-game test on Normal and Fastest with at least five active AIs and, where feasible, 300+ mobile units per AI. Record real/game-time ratio, actor counts, simulation timing, and profiler evidence. Exercise scaling and adversarial cases. Only make bounded behavior-preserving improvements here; never lower the 300-unit floor to manufacture gains.

## Relevant linked notes

- CNC-48 — Integration test: use the baseline when testing cumulative behavior and lag, and record lag sources in `DEFERRED_WORK.md`.
- CNC-49 — Lag reduction: measure before/after at fixed unit counts using the performance baseline, while preserving deterministic replays and the 300-unit floor.

## Relevant deferred constraints

- Profile long late-game matches on slower hardware. The adaptive 300-unit-per-AI floor intentionally favors strength and can still be expensive when several AIs are active.
- Keep a repeatable Normal/Fastest performance baseline with at least five active AIs and, where feasible, 300 or more mobile units per AI. Record real-time/game-time ratio, actor counts and profiler hotspots before accepting optimization claims.

## Selection rationale

CNC-47 is the first eligible pending task in sheet order after coordinator exclusions CNC-87, CNC-40, CNC-41, CNC-42, and CNC-44, and the same-round claims CNC-45 and CNC-46. It has no stated prerequisite, user-question gate, or pinned-final restriction. CNC-26C remains pinned final and ineligible.
