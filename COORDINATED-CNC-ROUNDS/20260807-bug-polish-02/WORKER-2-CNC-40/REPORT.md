# CNC-40 Task Report — Adaptive specialists

## Proposed status

**First iteration - testing.** The scoped specialist-outcome accounting is
implemented and passes its focused correctness regression, broad tests, and CNC
rules validation. It is not proposed as `Complete - testing`: a high-value
Engineer success produced prolonged target-starved Engineer saturation under the
unchanged adaptive bounds, and the required staged save/load and complete clean
adversarial set remain unproven. Numeric policy is frozen for CNC-40, so the
observed production-policy issue needs separately authorized follow-up.

## Behavior and design

- A completed direct building capture records one positive adaptive sample and
  the building's non-negative economic value for the actual completing Engineer's
  player and actor type, after the authoritative owner transfer succeeds.
- A restorable husk records one sample and only the usable replacement actor's
  economic value. Credit is deferred until `TransformOnCapture.AfterTransform`
  confirms that the expected replacement exists, is usable, and is owned by the
  captor. The zero-value shell and failed/configured-only transforms do not count.
- Delayed C4 keeps the planter's stable player, actor type, and actor ID. During
  the exact resolving building kill, `Demolishable` supplies the direct sell
  value to the existing generic kill ledger. This replaces the ordinary valued
  cost for that one adaptive mutation; it does not add a second completion hook.
- `CompletedSpecialistOutcome.TryRecord` centralizes exact one-sample/value
  mutation, non-negative clamping, bounded diagnostics, and the missing
  `PlayerStatistics` warning/no-credit path.
- `UpdatesPlayerStatistics` caches each actor's rare
  `IAdaptiveKillValue` providers at creation, avoiding a trait/LINQ discovery and
  allocation on every ordinary kill. When no provider supplies specialist
  evidence, the pre-existing generic kill field mutations remain unchanged.
- `e6` is added only to SkyNet's `AdaptiveTypes`. Authored `e6` weight 8, `rmbo`
  weight 2, costs, prerequisites, selection policy, confidence, decay, bounds,
  timing, targeting, reservations, transport, and C4 safety are unchanged.

The implementation assumes that the established specialist economic value is
the direct target sell value for a building and the configured replacement
actor's sell/value cost for a restored husk. Zero and negative economic values
still supply one completed-outcome sample with zero credited value. Stable
identity is deliberately stored instead of retaining a disposed specialist actor
reference.

## Implementation history

Five product-change cycles were used:

1. `b07fbdc6d6` — credit completed specialist outcomes adaptively.
2. `6cb1f8df94` — preserve ordinary kill behavior, actual frame-end capture owner,
   and handled missing-statistics capture behavior.
3. `55ceeec863` — cache specialist kill-value providers.
4. `3693e990e5` — preserve delayed C4 planter attribution.
5. `02496c3892` — route delayed C4 through the handled recording boundary.

The cumulative diff against `419bee2531d4802bf922c3597b42c6eeb75ab250`
was checked with `git diff --check`. Before publication, named CNC-39/39A commits
`53874e4328`, `0e9efa901a`, `0c6accf17a`, and `f3fbbb4da4` were confirmed as
ancestors of the unchanged base. PR #84's base/head boundary still resolves to
the recorded base, so no capture/demolition/statistics/config rebase was needed.

## Automated checks

- Release build: pass, 0 warnings and 0 errors.
- Full `OpenRA.Test`: pass, 457/457, including the 37 focused
  `AdaptiveWeightingTest`/`CaptureTargetingTest` cases.
- `./utility.sh cnc --check-yaml`: pass.
- Interface/static and cumulative diff checks: pass.
- Focused tests cover one sample/value mutation, non-negative clamping, and
  direct-versus-replacement economic value selection. Completion callback order,
  attribution, cancellation, and exact-once behavior are additionally exercised
  by full-engine games, but not yet by staged save/load automation.

No Red Alert, Dune 2000, or Tiberian Sun product work was performed.

## Full-engine evidence and old control

Twelve material full-engine games were counted: one base probe, one invalid
changed harness run that still produced outcome evidence, four valid changed
runs, four matched base controls, and a changed/control natural-conclusion pair
invalid only because both ended before the configured duration. Two tick-0 map
bootstrap failures had no game evidence and were excluded. All ordinary-AI games
used SkyNet plus an active opponent with normal modules; matched controls used
the exact base SHA `419bee2531d4802bf922c3597b42c6eeb75ab250` and matching
map bytes, seeds, starts, factions, and options.

- Base probe: `base-probe/run`. Existing C4 gave exactly one generic `rmbo`
  sample, but used valued cost 1500 rather than the building's custom sell value
  400. Capture/restoration supplied no Engineer evidence.
- Cycle 1, seed 40601: `cycle-01/paired/{changed,control}`, map SHA-256 prefix
  `48955c11`, tick 3500. Changed direct capture was `e6` 0->1 and 0->400;
  restored `htnk` was `e6` 1->2 and 400->2100 only after replacement creation;
  C4 was exactly one `rmbo` sample/value 400. Control completed the visible
  captures without Engineer adaptive evidence. Throughput was 290.994 changed
  versus 291.058 control ticks/s.
- Cycle 2, seed 40602: `cycle-02/paired/{changed,control}`, map SHA-256 prefix
  `064b4d08`, tick 9000. One 400-value Engineer outcome rolled to score 200.50,
  weight 167.6, confidence 0.10 without a duplicate. The subsequent queue was
  lost, so this was bounded accounting evidence rather than production-policy
  acceptance. Throughput was 427.863 versus 408.453 ticks/s.
- Cycle 3, seed 40603: `cycle-03/paired/{changed,control}`, map SHA-256 prefix
  `4e196aa5`, tick 12000. Runtime harvester husks invalidated the planned
  target-starvation setup but exercised target-rich response: changed recorded
  five exact Engineer outcomes worth 8400, selected 10 Engineers plus 19 other
  explicitly selected infantry, and retained three legitimate Engineer losses;
  control selected 3 Engineers plus 40 others. Throughput was 499.128 versus
  544.683 ticks/s in this divergent higher-actor/log-volume run.
- Cycle 4, seed 40604: `cycle-04/paired/{changed,control}`, map SHA-256 prefix
  `e83c8302`. Both games reached natural game over before the requested tick
  15000 and therefore are material but not duration-matched acceptance. Changed
  recorded one early 4000-value capture, then bought 16 Engineers before a late
  target appeared and 32 overall; at tick 12000 it reported 25 built/2 lost and
  13 surplus Engineers rejected around the reserved late pair. The late pair
  sabotaged and captured normally, adding exactly one 400-value sample. It still
  produced 73 conventional infantry and continued economy/defense/tech, so total
  crowd-out was not shown, but prolonged target-starved saturation was.
- Cycle 5 final literal pair, seed 40601:
  `cycle-05/final/{changed,control}`, the same `48955c11` map, tick 3500. Changed
  again recorded direct capture 0->1/0->400, C4 exactly once 0->1/0->400, and
  usable `htnk` restoration 1->2/400->2100. A later valid C4 added 4000 exactly
  once, an unplanted target removal added nothing, and a legitimate later
  Commando loss remained. Control completed the same visible paths without
  Engineer evidence. Neither run emitted a warning, fatal, or desync. Throughput
  was 317.670 versus 349.209 ticks/s.

The cycle-3 and cycle-5 changed runs were respectively 8.36% and 9.03% slower
than their controls, but the identical cycle-1 map was effectively equal and the
runs diverged in actions, actor counts, and diagnostic volume. The current
evidence does not establish a repeatable sustained >5% regression. No desync or
nondeterministic attribution was observed. Startup action timing can differ
between otherwise matched runs because the existing `CaptureManagerBotModule`
initializes with unseeded `World.LocalRandom`; map/replay seeds and content were
matched and this was not introduced by CNC-40.

## Independent evidence review

Each material batch has a factual Commenter narrative and routine Policy Review
under the task's analysis directory. Cycles 1-3 found exact accounting and a
conditional response but insufficient policy acceptance. Cycle 4's routine
review found Engineer saturation with medium-high confidence. The one permitted
Sol-xhigh escalation after ten counted games, at
`policy-escalation/POLICY-REVIEW.md`, recommends `First iteration - testing` and
blocks `Complete - testing`: exact accounting is valid, while a numeric remedy is
outside this task's frozen authority. Cycle 5's Commenter passes the final pair as
a focused correctness regression, and its Policy Review says that result does not
remove the prior saturation risk.

The mandatory Terra cycle-5 cumulative review is at
`cycle-review-05/CYCLE-REVIEW.md`. It found the implementation coherent and one
advisory concern: staged save/load exact-once behavior is unproven while husk
restoration attribution is pending and after credit. The concern is adopted as a
handoff risk; no product change is justified without that missing test evidence.

## Diagnostics

Retained diagnostics are bounded per completed or inconsistent outcome. A normal
record contains world tick, outcome kind, specialist actor ID/type/player,
target/replacement identity and value source, credited sample/value, and ledger
before/after. Missing statistics ownership or a promised replacement mismatch
warns and supplies no false credit. No per-tick probe, unbounded scan, temporary
debug hook, generated log, replay, save, or build output is included in Git.

## Remaining risks and deferred work

- Obtain explicit authority for a follow-up adaptive-production policy task to
  investigate target-starved Engineer saturation after an exceptional success.
  CNC-40 must not tune confidence, decay, weights, floor/ceiling, intervals, or
  other numeric policy.
- Add a staged save/load test after husk capture but before replacement creation,
  and after each completed C4/capture/restoration credit, verifying preserved
  state and no lost/double sample. This is required before promotion to
  `Complete - testing`.
- Complete the clean cancellation/ownership race, paired/shared-target transport
  contention, failed-transform, blocked/island transport, repeated-failure, and
  strongest final-stress differential set. The current first-iteration handoff
  does not claim these acceptance gates.
- A forced in-game Commando owner-change after planting was not obtained. The
  implementation stores the planter's stable player/type/ID, and ordinary delayed
  disposal was exercised, but the ownership-race case remains a test gap.
- Repeat stable-workload throughput comparisons if promotion is considered; the
  observed slower pairs are not repeatable but should not be ignored.
- CNC-90 may later reissue specialist orders or change post-mission ownership; it
  must preserve this exact-once completion accounting.

## Artifact roots

- Match evidence and role outputs:
  `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-02/analysis/worker-2-cnc40`
- Cycle-5 factual narrative: `cycle-05/commenter/NARRATIVE.md`
- Cycle-5 policy review: `cycle-05/policy/POLICY-REVIEW.md`
- Cycle-5 code review: `cycle-review-05/CYCLE-REVIEW.md`
- Policy escalation: `policy-escalation/POLICY-REVIEW.md`

## Publication

- Task branch: `agent/round-20260807-cnc40-adaptive-specialists`
- Intended base: `agent/cnc-20260806-bug-polish-01-release`
- Pull request and required checks: pending publication.
- Final Sol-high task-PR review: pending publication/checks.
