# CNC-33B: Unstable Harvester Decloak and Deploy

- Status: complete; draft PR #71 required checks green
- Cycles used: 30 of 30
- Branch: `agent/cnc33b-red-tiberium-deploy`
- Base: `origin/agent/cnc30-1-bounded-exploration` at `78ae3946ec`
- PR: https://github.com/Realpra1/LibertyDawn/pull/71
- Green implementation head: `67d8c0fb63` (Linux 2m13s; Windows 3m10s)

## Literal acceptance

Load real red Tiberium into a Harvester or Stealth Harvester. For the first 2,250 ticks it may use its normal cloak behavior and a deploy order must do nothing. At tick 2,250 it must begin the existing warning blink and become visibly uncloaked continuously for the entire final 750 ticks. With normal options it must retain the existing automatic detonation at 3,000 ticks. With `No red Tiberium explosions` enabled it must remain alive after tick 3,000, but a deploy order at or after that boundary must cause the real red-cargo detonation at its current location; the order must remain unavailable before both the two-minute cargo age and full 30-second warning. Unloading or losing all red cargo resets both timers, removes the warning decloak, and restores normal cloak rules.

At a valid enemy target, the ordinary red-Tiberium bomb module must explicitly deploy a ready unstable harvester instead of waiting forever when automatic red explosions are disabled. Both VIKI and an Iron Reaper that reaches covert technology must produce/use eligible stealth harvesters and exercise this mission path.

Forbidden outcomes are early deploy or explosion; automatic detonation being lost under default options; no-red silently killing the harvester at 3,000 ticks; warning harvesters recloaking outside detector range; permanent decloak after cargo loss; deploy affecting green/blue/empty harvesters, ordinary stealth units, or MCV/cargo deploy orders; bypassing `No red` for non-deploy deaths; mutation when `No mutants ever` is enabled; AI orders before arrival/readiness, repeated deploy spam, lost reservations, or save/load resetting/duplicating timers and missions.

## Plan and design

- The existing `Blink` trait only owns a private visual delay and cannot expose its active phase as a rules condition. Add one narrow unstable-harvester timer/deploy trait rather than broadening `Blink` or changing global cloak semantics.
- Track continuous real unstable-cargo age deterministically. Grant a configured warning condition for the final 750 ticks; rules use that condition both to enable the existing blink and disable only the harvester's cloak trait. Revoke/reset immediately when unstable cargo disappears.
- Preserve automatic 3,000-tick death when red explosions are enabled. When the semantic red impact is suppressed, keep the armed harvester alive for explicit deploy. A valid deploy marks only that death as an intentional red-impact suppression bypass, then kills the actor through its existing configured `Explodes` traits so normal/non-mutating weapon selection remains centralized.
- Extend the bomb mission state with readiness/one-shot deploy logging and issue deploy only when the unit is in target blast range and the actor trait accepts it. Persist the actor timer and existing mission state. Keep release debug switches false.

## Contention inventory and tests

The same harvester is ordered/reserved by both Harvester bot modules, `FindAndDeliverResources`, the red-bomb module, crate exploration exclusions, transport/squad/specialist reservations, repair, player `Harvest`/`Deliver`/`Move`/`Stop`, and detector/cloak state. Deploy dispatch shares the command-bar `IIssueDeployOrder` aggregation with MCV transforms, cargo unload, aircraft return, and other deploy traits, so the new trait must be actor-local and reject ineligible states without stealing those orders. Explosion contention includes all `Explodes` traits, semantic no-red/no-blue suppression, no-mutants weapon substitution/creation suppression, external combat deaths, ownership changes, cargo unload, actor disposal, and save/load/replay.

Focused tests cover ticks 2,249/2,250/2,999/3,000; unstable/empty state; default automatic versus no-red hold; explicit bypass scope; timer reset; and deployment eligibility. Full-engine tests cover HARV/SHARV loading, warning visibility, exact early/ready orders, default/no-red/no-mutants/no-blue combinations, unloading, detectors, ownership, VIKI and covert Iron Reaper missions, target arrival, competing harvester orders/reservations, mid-timer/mid-warning save/load, replay, asymmetric/blocked approach paths, and natural MAX matches. Acceptance is followed by three distinct clean adversarial games and a final literal regression.

## Implementation

- Added a deterministic, synced `UnstableHarvesterDetonation` trait. It owns continuous red-cargo age, grants warning and automatic-damage conditions, exposes an actor-local mature deploy order, and resets immediately after unloading.
- The final 750 ticks now drive both the existing blink and an explicit cloak exclusion. Default automatic death still uses the original `ChangesHealth` path at 3,000 ticks; `No red Tiberium explosions` disables only that automatic damage condition.
- A valid mature deploy kills through the actor's existing `Explodes` traits. The suppression bypass is limited to that source actor, that explicit death, and `RedTiberiumExplosion`; normal deaths and blue suppression remain gated, while `No mutants ever` still selects the non-mutating weapon.
- The bomb module waits at a valid target until the timer is mature, issues one queued deploy order, retries only at its existing bounded interval if necessary, and records the appended mission state without changing earlier serialized enum values.
- Release rules retain `DebugLogging: false`. A narrow scripting property was added only to make exact player-order boundaries observable in engine fixtures.

## Evidence

- Pure policy coverage proves warning boundaries at 2,249/2,250, deploy boundaries at 2,999/3,000, unstable-state gating, and automatic suppression behavior. Final validation passed 406/406 unit tests, strict Debug and Release builds with zero warnings, interface checks, Lua checks, exhaustive CNC YAML/map validation, and `git diff --check`.
- Focused real-engine cycles proved early deploy rejection, warning decloak, default death at exact cargo age 3,000, survival under no-red, explicit target destruction, unload timer reset and cloak recovery, no-red/no-blue and no-red/no-mutants combinations, zero created mutants, VIKI ordering, and covert Iron Reaper ordering.
- A tick-2,500 warning save loaded without resetting the actor timer or duplicating the mission; the loaded VIKI issued one mature deploy and destroyed its target without desync.
- The natural long-distance VIKI-versus-Brutalis MAX match reached its normal tick-60,000 conclusion. It produced 48 organic bomb launches and four mature explicit deployments; defended warning harvesters that died before maturity did not issue deploy orders.
- Final cycles 28-30 ran three isolated games concurrently on the final code at a combined 524.52 valid ticks/second. Default VIKI detonated at age 3,000 with no explicit order; no-red VIKI passed early rejection, unload/reset contention, one mature order, and target destruction; covert Iron Reaper passed technology activation, one mature order, and target destruction. All reached tick 3,500 with no desync, fatal Lua/rules error, or unhandled exception. Raw evidence is ignored under `.build/cnc33b/evidence/`, especially `cycle19-natural-empire/` and `cycles28-30-final-current-code/`.

## Remaining risk

The isolated final deploy fixtures use a Stealth Harvester because that is the ordinary bomb-module unit. Ordinary HARV uses the same timer/deploy trait and its weapon-level semantic bypass is source-actor aware, but its manual deployment was established by shared policy/rules validation rather than a separate final-cycle observable. The 30-cycle ceiling was reached after three clean final-code adversarials; no known functional failure remains.

## Post-completion correction

CNC-34's first ordinary match exposed a trait-access exception when a resource explosion retained an already disposed infantry source. The actor-aware weapon gate now skips bypass-trait lookup for disposed sources, which cannot be an active explicit harvester deployment. The reproducing ordinary match and strict validation pass on the corrected cumulative head.
