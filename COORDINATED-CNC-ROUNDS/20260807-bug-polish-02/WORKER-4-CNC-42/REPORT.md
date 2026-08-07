# CNC-42 — Economy field defense

## Current status

Integrated RC1 is complete on `agent/round-20260807-cnc42-rc1-repair` from release
head `ffb841b48750cc54b1862fb93101d3dce3a87a3f`. Proposed handoff is
`Complete - testing`: tested product head
`b6e7eecf15a6993a2349b1595ffb2c350582d976` persists exact economy-SAM build
ownership across a mid-build save/load, and the reset clean-three, combined
CNC-41 G4/G7, corrected matched stressed-final differential, focused, and
repository-wide gates pass.

## Dependency inspection

- 2026-08-07 before product changes: CNC-41 local/remote branch
  `agent/round-20260807-cnc41-economy-tiberium-fields` at `ab7997c89b`.
- The scoped diff contains only CNC-41's worker contract. There is no product or
  configuration change and no GitHub PR to consume yet.
- The cycle-5 recheck again found local/remote head
  `ab7997c89b8a2d545b894aef2a08e615e957032e` and no open CNC-41 PR.
- Immediately before cycle-10 BaseBuilder work, local, remote-tracking, and live
  remote CNC-41 remained at
  `ab7997c89b8a2d545b894aef2a08e615e957032e`; the scoped diff still contained
  only its worker contract and no open CNC-41 PR existed.
- At publication, CNC-41 had advanced to PR #88, head
  `418786381f64b1cae4ff9a8d1d943c78d5666646`, with product commit
  `aa4e97972d8a0cb7f4780babcdffa4fa363c2299`. Its permitted scoped diff adds an
  internal persisted Tiberium-field manager and touches the same
  `BaseBuilderBotModule.cs`, `BaseBuilderQueueManager.cs`, and `mods/cnc/rules/ai.yaml`
  surfaces as CNC-42. It exposes no shared live field identity/entrance/traffic
  API for CNC-42 to consume. The branches remain independent; the integrator must
  combine their additive config and queue branches deliberately and rerun G4/G5/G7
  on the reviewed combined candidate.
- Assumption: CNC-42 will own only post-unload field facts and field-defense
  policy, with narrow traffic/placement helpers that can be integrated with a
  later reviewed CNC-41 API.

## Cycle evidence

G0 run 1 (seed 424200) loaded the ordinary Brutalis versus
Skynet control at headless MAX and advanced through tick 600. The harvester moved
from the live field into the refinery delivery area, but raid creation used the
nonexistent CNC actor `apache` instead of `heli`, causing a fatal Lua exception.
This is counted as one invalid full-engine test; G0 characterization and the first
paired post-change game remain pending. Artifacts:
`analysis/worker-4-cnc42/games/g0-control/run-v2/g0-base-control`.

G0 run 2 used the corrected `heli` raid and advanced cleanly through configured
tick 3000. The launcher status failed only because `AfterDelay` logged the raid
and outcome at 601/2951 versus exact 600/2950 assertions. The player-visible
control characterization is decisive: the original riflemen left in opposite
directions from the first samples, the tank/MSAM later left the field, the tank
died, and no SAM existed at any sample. The harvester repeatedly traversed the
live field/refinery path but had no stable 1/2/1 local owner. Artifacts:
`analysis/worker-4-cnc42/games/g0-control/run-v3/g0-base-control`.

### Cycle 1

Implemented an explicit unload-completed event, saveable pending/committed field
facts, and the first reservation/assignment/safe-route field owner. Focused policy
tests passed 6/6, Release compiled without warnings, and CNC MiniYAML validation
passed. G1 paired seed 424201 reached tick 3000 on both changed and detached base.
Changed first committed after the delivery window and eventually retained the
Medium Tank/MSAM and one tracked rifle near the field while control dispersed
them. G1 did not pass: the map refinery created an unintended second harvester,
active base protection correctly delayed tank/AA assignment, and en-route
infantry received forbidden repeated reform orders every 25 ticks. Artifacts:
`analysis/worker-4-cnc42/games/g1-paired/{changed-run,control-run}`.

### Cycle 2

Added progress-sensitive route ownership, separated active local combat from
stalled movement, bounded route rejection/retry, and explicit pending/unload/
commit diagnostics. Focused policy tests again passed 6/6; `make test` and
`make check` passed with zero compile warnings. The corrected G1 changed/control
pair used map SHA-256 `2232d428...`, seed 424201, one scripted initial harvester,
identical ordinary bots/options, and reached tick 1800 cleanly in both arms.

Changed recorded actual pending harvest cells, then unload start at tick 706,
completed empty unload at tick 787, and bot commit at tick 801. It reached exact
one-field 1 mtnk/2 e1/1 msam composition at tick 1001. Per-defender reform orders
were separated by at least 100 ticks and reflected destination changes; the
cycle-1 25-tick order churn was absent. Changed moved the original tank/MSAM to
field cells 23,15/22,15 while control retained them at base cells 32,13/33,13.
The timed raid preceded screen formation, so this is feature-execution evidence,
not combat or economic acceptance. The fresh Commenter found no evidence blocker
and no match winner; routine Policy Review concluded `insufficient evidence`
with high confidence and required matched raids after the screen forms.
Artifacts: `analysis/worker-4-cnc42/games/g1-cycle2/{changed-run,control-run}`,
`analysis/worker-4-cnc42/g1-cycle2-comment/NARRATIVE.md`, and
`analysis/worker-4-cnc42/g1-cycle2-policy/POLICY-REVIEW.md`.

The prelaunch `product_diff_sha256` was a checksum of the tracked diff only and
therefore omitted then-untracked new source files. The corrected combined tracked
diff plus untracked-product checksum after the run is
`4a426b332ac3bbe3b9344bfe54a4c97292e4b58bd6c8c5ead79ec8e3528c6f79`;
the exercised changed Common/CNC assembly hashes are `a5f03384...` and
`2d4db105...`. This accounting defect does not change the map/seed or exercised
binaries, and subsequent manifests must use a complete product identity.

### Cycle 3

Added owner-keyed external production requests so field defense can deduplicate
and cancel only its own demand, save-compatible owner serialization in
UnitBuilder, nested persisted field/assignment/destination state, stale-field
cleanup, and refinery/low-power request gates. Focused policy tests passed 11/11
and Release `make test` passed. G2 used two separated original harvesters and
refineries, only one pre-placed mtnk/e1, and fixed bike/two-e3/heli raids after
both initial fields committed. The first launch pair stopped at tick 0 on a Lua
loop terminator error and is excluded from game counts; corrected run 2 reached
tick 5000 cleanly in both arms.

G2 failed the intended product test. The owned-production provider was filtered
while condition-disabled during early trait creation and cached as absent, so no
owned request occurred; changed had no MSAM and only one tank. Changed still
formed partial local screens, delayed the left harvester loss from control tick
4204 to 4488, and killed 2/8 bikes versus control 0/8, but both arms lost both
original harvesters. Normal production also expanded committed fields from two
to six, exposing real scale pressure while invalidating the exact two-field
composition assertion. Both fresh reviewers rated the result insufficient with
high confidence: partial local assignment has credible value, but the corrected
mixed force must exist before contact and preserve at least one harvester through
a later unload. Artifacts:
`analysis/worker-4-cnc42/games/g2-separated/{changed-run2,control-run2}`,
`analysis/worker-4-cnc42/g2-separated-comment/NARRATIVE.md`, and
`analysis/worker-4-cnc42/g2-separated-policy/POLICY-REVIEW.md`.

### Cycle 4

Fixed provider timing by caching all owner-aware production providers and
selecting the enabled provider at each demand scan; cancellation checks every
provider. Added per-field composition diagnostics so later normal expansion
cannot make a global composition marker masquerade as the measured original
cohort. Focused tests passed 11/11 and Release `make test` passed. The same G2
pair reached tick 5000 cleanly and provider requests executed.

The left original field reached 1/2/1 before its raid; the right remained 1/2/0
until the left harvester died and its MSAM was reassigned. Changed killed a
helicopter and bike versus control's zero scripted kills, delayed the left loss
113 ticks, and preserved both refineries at full health, but its right harvester
died 59 ticks before control's. It therefore failed decisive economy survival.
The manifest's right 1/2/1 marker occurred only after contact/left loss and is not
accepted as a behavioral pass. MSAM 369 also exposed renewed forbidden 25-tick
idle-retry churn from ticks 3651-4126. The fresh Policy Reviewer returned `mixed`
with high confidence: local mixed defense has tactical value, but bounded parallel
demand and reliable positioning must provide both screens before contact.
Artifacts: `analysis/worker-4-cnc42/games/g2-separated/{changed-run3,control-run3}`,
`analysis/worker-4-cnc42/g2-cycle4-comment/NARRATIVE.md`, and
`analysis/worker-4-cnc42/g2-cycle4-policy/POLICY-REVIEW.md`.

### Cycle 5

Raised only the expressly owned per-role field-defense request concurrency from
one to two and withheld idle route retries until the existing 250-tick stall
timeout. Focused policy tests passed 11/11. The complete exercised product
identity was `f5c28594283b767a3308252c1cec7d6ea9bd147ec942c575ce42a5faef92cb03`;
the changed Common/CNC assembly hashes were `960c42ba...` and `ed772a61...`, and
the AI rules hash was `f44acb1a...`.

The changed seed-424302 scenario passed through tick 5000. Both original fields
visibly held 1 mtnk/2 e1/1 msam at the tick-3401 pre-contact assertion. Changed
killed the right helicopter and one bike, kept the right harvester alive through
tick 5000 at 29,000 health, and eliminated the prior 25-tick idle-retry loop;
representative idle retries were approximately 250 ticks apart. The left
harvester still died at tick 4052. The concurrently launched current control
ended naturally near tick 2001 before the raids and is invalid for the raid
comparison, but counts as an advanced invalid full-engine test. The prior cycle-4
control is an identical detached-base/map/seed/options comparator and is reused
without recounting: it lost the left harvester at tick 4025 and the right at tick
4945, with zero scripted kills. Changed therefore prevented one old-control
harvester loss for this first materially successful seed, while refinery health
remained a mixed left/right tradeoff.

The fresh Commenter confirmed the comparator distinction and tick-level result.
Routine Policy Review returned `mostly sensible` with medium confidence: retain
the bounded mixed screen, but require multi-seed confirmation, assess refinery
damage per field, and explicitly exercise leash/re-form, hazard/traffic,
reservation/release, and static-SAM boundaries. Artifacts:
`analysis/worker-4-cnc42/games/g2-separated/changed-run4`,
`analysis/worker-4-cnc42/games/g2-separated/control-run4`,
`analysis/worker-4-cnc42/games/g2-separated/control-run3`,
`analysis/worker-4-cnc42/g2-cycle5-comment/NARRATIVE.md`, and
`analysis/worker-4-cnc42/g2-cycle5-policy/POLICY-REVIEW.md`.

The mandatory Terra cycle-5 code review returned one advisory concern, adopted:
the safe path search rejected hazard/traffic cells, but four-cell-spaced ordinary
movement waypoints could independently re-path through the forbidden cells. The
cycle-6 repair must preserve each validated segment and add route-level toxic
geometry evidence. Review:
`analysis/worker-4-cnc42/cycle-review-05/CYCLE-REVIEW.md`.

### Cycle 6

Replaced spaced ordinary waypoints with a synchronized, bounded exact-path order
and activity. The resolver accepts only adjacent serialized cells, advances one
validated segment at a time without alternative pathfinding, rechecks resource
safety before each segment, and is configured only on the three field-defense
role actors. Added a focused serialization/adjacency/bound regression and
rate-limited actual resource/traffic occupancy diagnostics. Focused tests passed
12/12; Release `make test` passed with zero build warnings. The cycle-6 product
identity was `3058d17777af003155ee15f20be6390747410383d1d20d3c0e176f82762f3ad1`.

G4 added five harvesters, existing green fields, a four-cell-wide alternating
blue/red barrier, two busy refineries, and paired mixed raids with bike kites.
The initial no-Xvfb pair failed before tick advancement and is excluded. The
corrected changed/control pair both reached configured tick 6000. Exact orders
executed and changed formed visible original-field screens by tick 4001 (left
2/2/2, right 1/3/1 versus control 0/0/0 and 0/1/0). At tick 5551 both arms kept
both original harvesters/refineries, but changed preserved 34,434 right-harvester
health versus control 11,000.

The safety result failed decisively: changed logged multiple Minigunners on
Tiberium starting at tick 1801, plus tank/Mobile-SAM resource occupancy. Exact
station routing is no longer the only cause; unrestricted autonomous pursuit and
resource growth around near-field cells can move/overtake reserved defenders.
The intended full-storage aborted unload was also not exercised because the
tick-0 capacity had not initialized (`0/0`); it must be retimed rather than
claimed. Artifacts:
`analysis/worker-4-cnc42/games/g4-toxic-route/{changed-run2,control-run2}` and
`analysis/worker-4-cnc42/g4-cycle6-comment/NARRATIVE.md`.

The fresh Commenter confirmed the direct hazard failures and the storage-harness
limitation. Routine Policy Review returned `mixed` with high confidence: the
screen is strategically better than control in the exercised raid, but the hard
safety violation outweighs that benefit. It requires continuous enforcement
across hold, pursuit, re-form, and recovery, plus a delayed genuine-full storage
case. Review:
`analysis/worker-4-cnc42/g4-cycle6-policy/POLICY-REVIEW.md`.

### Cycle 7

Reserved defenders now have inherited attack activities cleared, switch to
`Defend`, and use exact non-attack station routes; their prior stances are retained
for release/save state. Candidate and route cells require a one-cell resource
margin, and malformed exact paths with repeated cells are rejected. Focused tests
passed 12/12 after the repeated-cell regression caught and drove that validator
fix. Release `make test` passed with zero warnings. The product identity was
`94d85985b9a317c1c08b5a83d6c27f093d48a8e75f068bfde636b51037c2b385`.

The corrected G4 pair used positive `8300/8300` storage at tick 2 and both arms
reached tick 6000. The changed screens were visibly strong at tick 4001 (left
1/4/2 and right 2/3/2 versus control 0/0/0 on both original fields), killed both
bikes by tick 4443, and kept both original harvesters/refineries pristine. Control
also kept those assets pristine and killed 3/8 scripted raiders versus changed
2/8, so this batch supplies no decisive economy-outcome improvement.

Safety still failed: the changed log recorded five e1 and three tank Tiberium
occupancies from ticks 1451-5051, all outside refinery traffic. The one-cell
annulus excludes diagonal neighbors even though resource spread can choose them;
per-field `used` sets also permit simultaneous fields to retain overlapping
destinations and displace actors. Storage saturation was real at tick 2 but not
continuously maintained against AI spending, allowing commits at ticks 676, 726,
851, and 901; the aborted/full-storage unload remains unexercised. The fresh
Commenter found no evidence-integrity blocker. Routine Policy Review returned
`Fail — exercised policy violation` with high confidence and required compliant
final routes/cells or safe release before outcome tuning. Artifacts:
`analysis/worker-4-cnc42/games/g4-toxic-route/{changed-run3,control-run3}`,
`analysis/worker-4-cnc42/g4-cycle7-comment/NARRATIVE.md`, and
`analysis/worker-4-cnc42/g4-cycle7-policy/POLICY-REVIEW.md`.

### Cycle 8

Expanded resource safety to a two-cell annulus (covering diagonal one-step
spread) and made all simultaneous fields share one deterministic set of occupied
formation cells. The G4 harness continuously reset storage to full through tick
901 and explicitly cancelled the left dock at tick 751. Focused tests passed
12/12 and Release `make test` passed with zero warnings. The cycle-8 product
identity was `0dff5fe7006e2576d9dbcba7c71b90dd63ea62e7802ce7d28ac667e0e1748a1e`.

Changed reached tick 6000 and the state-transition boundary passed for the first
time: no field commit preceded the full-storage cancellation or release, completed
unloads occurred at tick 982, and first commits followed at tick 1001. Forbidden
occupancy fell from eight events to two e1-only late events, with no tank, MSAM,
or refinery-traffic violation. E1 349 had destination 71,14 at tick 4551 but
appeared on Tiberium at 76,18 at tick 4751; e1 360 had destination 70,15 at tick
5551 but appeared on Tiberium at 77,18 at tick 5701. Both were released. Their
multi-cell displacement from exact destinations points to ordinary `Mobile.Nudge`
movement, which is outside the exact field route and has no resource predicate.

Changed killed 5/8 raiders and preserved both original harvesters/refineries,
but the detached control ended naturally around tick 3801 before the raids. It is
valid only for the shared opening and cannot support outcome/performance claims.
The fresh Commenter identified that comparator blocker while accepting the changed
safety failure. Routine Policy Review returned `Fail` with high confidence: later
release is containment, not compliance; displacement must itself remain safe.
Artifacts: `analysis/worker-4-cnc42/games/g4-toxic-route/{changed-run4,control-run4}`,
`analysis/worker-4-cnc42/g4-cycle8-comment/NARRATIVE.md`, and
`analysis/worker-4-cnc42/g4-cycle8-policy/POLICY-REVIEW.md`.

The cycle-8 product identity recorded above was accidentally calculated from the
sibling analysis repository context rather than this worker repository. The map,
seed, launch manifests, and exercised Common/CNC assembly hashes still pin the
actual run, but `0dff5fe...` is not a valid complete source-product checksum and
must not be used as one. Cycle 9 restores complete worker-repository accounting.

### Cycle 9

Added a small owner-activated nudge-cell validation interface. While the field
module owns a defender, its synchronized exact-route trait rejects ordinary
collision-nudge destinations using the same resource-margin predicate; releasing
the actor disables this restriction and restores normal movement. Focused policy
and exact-route tests passed 12/12, and Release `make test` passed with zero
warnings. The complete exercised source-product identity was
`5426c6290f5d670de2a7d7b2669835c3ee71ef12287302ada1753fe56c9bf61e`;
the changed Common/CNC assembly hashes were
`6ffec99caf23769b12e4adc602d97ae6cdac4cbce7d67a46716197282afe04c7`
and `78f1ca0dab9de17386cb0dc6efd3fc2d5fd7c7589984347b35e24ce2ee099aae`.

The G4 pair retained the continuously full storage, tick-751 cancellation, and
tick-901 release, but advanced raids to ticks 3001/3101 so the detached control
would observe them. Changed recorded no commit before release, first commits at
tick 1001, and no resource or refinery-traffic occupancy through natural game end.
At tick 3001 both original fields held 2/4/2 screens. At the tick-3751 outcome,
changed preserved both harvesters at 35,000/33,000 health and both refineries at
full health; the control's left harvester was already at 16,250 and died at tick
3915. Changed ultimately logged all 8/8 raid callbacks at tick 4436, while control
reached 7/8 and retained one attacker through tick 6000. The simultaneous changed
callbacks are recorded without inferring their cause.

The changed game ended naturally at launcher-observed tick 5000, so its manifest
failed only the configured tick-6000 minimum. It is behaviorally clean through
natural end but is not late-duration proof. Observed throughput was effectively
identical at about 249.65 changed versus 249.70 control ticks/second. The fresh
Commenter confirmed the stronger economy-protection result and duration caveat.
Routine Policy Review returned `Pass with duration-evidence limitation` with high
confidence. This is the first distinct clean adversarial scenario after the latest
safety fix; three clean scenarios, static-SAM evidence, and a full-duration changed
arm remain outstanding. Artifacts:
`analysis/worker-4-cnc42/games/g4-toxic-route/{changed-run5,control-run5}`,
`analysis/worker-4-cnc42/g4-cycle9-comment/NARRATIVE.md`, and
`analysis/worker-4-cnc42/g4-cycle9-policy/POLICY-REVIEW.md`.

### Cycle 10

The required pre-BaseBuilder CNC-41 recheck again found only its contract commit
and no PR. Added a narrow BaseBuilder-owned economy-SAM planner for Brutalis and
Iron Reaper after Economy II: existing active coverage satisfies nearby anchors;
otherwise normal SAM construction prioritizes live refineries, resonators, then
materially used silos, admits only powered construction under a configured
four-site cap, and selects a legal footprint outside every refinery footprint and
approach corridor. Focused field/SAM policy tests passed 18/18. Release `make test`
and CNC MiniYAML/map validation passed with zero compiler warnings. The complete
sorted product-file manifest identity was
`32575b30ddd72bcdc11877ba6d21545508c75003621ae25d316d402ea5fde4d3`;
changed Common/CNC assemblies were
`51ca78d330a9c1f13d710faef857883b1a9804f33360f121976a25da0d9f950b`
and `cf720fe7711acb7bf8bf219be932c63f85a14aadb7bdb2c0ab3ee776a5fce9ff`.

G5 pre-placed a left SAM whose effective coverage overlapped a refinery and
resonator, held bot power unavailable through tick 1001, then restored ample
power with the right refinery uncovered. The first pair advanced through tick 801
and proved each arm still had exactly one SAM, then both hit the same unsupported
Lua `table.concat` formatter. Those two advanced harness-invalid games count but
are not product evidence. A bounded string loop corrected only the formatter.

The judged corrected pair both reached tick 6500. Changed reserved/placed SAMs at
67,16 for the uncovered priority-0 refinery and 76,16 for a priority-1 resonator,
then withheld further placement because all anchors were covered. It did issue a
second reservation while one SAM remained pending, so the intended one-in-flight
bound is not yet demonstrated. Control placed one generic SAM at 70,30, about 13
cells from the right refinery and beyond the base SAM's 10-cell range. Changed
killed 7/8 raiders, preserved both harvesters (34,960/35,000 and 35,000/35,000 at
tick 6001), and kept both refineries pristine. Control lost the right harvester,
left the other at 30,000/35,000, damaged the left refinery to 73,485/100,000, and
killed 6/8. Both waited correctly for power. Throughput differed by less than
0.1% (about 216.49 versus 216.59 ticks/second).

The changed result nevertheless failed: assigned e1 364 occupied Tiberium at
76,17 on tick 3726 and was later released for route invalidation at tick 3951.
The cell is adjacent to the new 76,16 SAM, but neither raw evidence nor the fresh
Commenter establishes causality. Routine Policy Review returned `FAIL` with high
confidence: the decisive economy improvement supports the strategy, but cannot
override the hard zero-infantry-resource rule. Artifacts:
`analysis/worker-4-cnc42/games/g5-static-sam/{changed-run1,control-run1,changed-run2,control-run2}`,
`analysis/worker-4-cnc42/g5-cycle10-comment/NARRATIVE.md`, and
`analysis/worker-4-cnc42/g5-cycle10-policy/POLICY-REVIEW.md`.

The mandatory cycle-10 Terra code review returned one advisory and it is adopted.
The cumulative diff's exact-route, persistence, determinism, bounded-scan,
ownership, and BaseBuilder responsibilities had no stronger exposed defect, but
the live e1 failure proves owned safety does not yet veto every ordinary movement
source. Cycle 11 will extend the same predicate to Mobile cell transitions and
project active resource-modifier zones into planned safe cells, then force the G5
opportunity again. Review:
`analysis/worker-4-cnc42/cycle-review-10/CYCLE-REVIEW.md`.

### Cycle 11

Generalized the field-owned safety seam from nudge-only filtering to every
`Mobile.CanEnterCell` transition, and made exact routes share that strict
predicate while an actor is owned. The field module also projected every active
resource modifier's range plus one configured cell out of destinations and routes.
Economy SAM demand was tightened to one in-flight request. Focused policy tests
passed 22/22; Release `make test` and CNC MiniYAML validation passed with zero
warnings. The complete product identity was
`9d41b7256716de1c71f4e2afe6fd43d981204d9fc1a050fe7a2945ea11ef76a1`;
changed Common/CNC assemblies were
`d30a0facac8b45cb8e41e97aea39185f62a4a81e67731105807d45c05904944d`
and `b5086e4ced01449a1f4d5ceb04ef778b9dd042de96842eb81030ba2b965bdbb6`.

The unchanged G5 seed-424307 pair produced a strong but invalid changed result.
Every economy-SAM reservation reported `pending=0`; the bot placed distinct sites
at 67,16 and 76,18. No assigned e1 occupied Tiberium. At tick 6001 changed had
both harvesters alive at 22,719 and 34,999 health, both refineries pristine, and
4/8 scripted raiders destroyed. The clean control lost the right harvester, left
the survivor at 7,000 health and the right refinery at 3,777/100,000, and killed
2/8. Thus the mixed defense and serialized static coverage remained materially
better than old behavior.

Changed did not pass runtime acceptance. MSAM 395 was claimed while already on
Tiberium at ticks 4276 and 4526, then immediately released for no safe route. The
projected exclusion also caused frequent safe-release behavior. After the map's
tick-6001 duration snapshot, an actor whose cell already equaled its chosen
destination produced a one-cell path at tick 6026; `MoveAlongPath.CreateOrder`
requires at least two cells and threw `ArgumentException`. The process exited 1
without the configured exit marker or benchmark, while control cleanly reached
tick 6500.

The fresh Commenter rejected the run while confirming its large player-facing
economy advantage. Routine Policy Review likewise rejected runtime acceptance
with high confidence 0.94, retained the strategic policy, and distinguished the
literal hard rules: resource exclusion is absolute for infantry, whereas tanks
and Mobile SAMs should prefer resource-free cells/routes without sacrificing
useful coverage; refinery-lane exclusion remains absolute for all defenders.
Cycle 12 adopts that class-specific interpretation, rejects initially unsafe
claims, and treats a one-cell route as already arrived rather than issuing an
invalid exact order. Artifacts:
`analysis/worker-4-cnc42/games/g5-static-sam/{changed-run3,control-run3}`,
`analysis/worker-4-cnc42/g5-cycle11-comment/NARRATIVE.md`, and
`analysis/worker-4-cnc42/g5-cycle11-policy/POLICY-REVIEW.md`.

### Cycle 12

Adopted the cycle-11 policy distinction without weakening literal safety.
Same-cell subcell offsets now count as arrived, and a defensive caller also
withholds any exact path shorter than two cells; malformed external exact orders
remain rejected. Infantry retains current and projected Tiberium exclusion at
every Mobile transition. Tanks and Mobile SAMs first seek the same resource-free
destinations and exact paths, then may use a resource-crossing fallback while all
roles remain absolutely excluded from refinery traffic. Candidates already in a
hard-forbidden cell are not claimed, and diagnostics now separate hard occupancy
from vehicle preference misses.

Focused policy tests passed 23/23, including the same-cell/subcell regression.
Release `make test` and Debug `make check` passed with zero warnings/errors. The
complete product identity was
`420012335c071ea079af1b4bfe4c68e9ce3175ac5bb15e5bb7ac6fa0405f8478`;
changed Common/CNC assemblies were
`b2a6595d2177f6153b31de2b6dbd42a512d5003ab64860cda7a9dbd11d894aef`
and `712d53ed3f6dcfd6d736fea83393433c5150d82c94b511c8ab55f53bceee1095`.

The fresh G5 seed-424307 pair both passed through tick 6500 with configured exits,
benchmarks, and no crash/desync. Changed had zero infantry-resource and zero
all-role traffic violations, no overlapping SAM reservation, field-side static
sites at 67,16 and 76,16, and repeated successful post-raid unloads. It kept both
original harvesters (35,000 and 5,675 health at tick 6001), both refineries at
full health, and reached 7/8 raid kills by tick 6418. Control lost the right
harvester at tick 3368 and the left at tick 6160, left the right refinery at
66,228/100,000 on tick 6001, and reached 3/8 kills by exit. Changed showed useful
screens on both sides (1/4/1 each at tick 3001 and 2/4/2 plus 2/5/2 at tick 6001),
versus no measured field screens in control. Two transient MSAM Tiberium contacts
at ticks 4126/4151 were non-traffic preference misses; the actor later released
and replacement coverage formed. Changed completed at about 341.53 ticks/second
versus control 282.24 in this single pair.

The fresh Commenter judged a clear player-facing improvement with clean mandatory
safety and runtime validity. Routine Policy Review returned `PASS — clean and
materially successful for the stated adversarial/acceptance scenario`, with high
confidence for this scenario and moderate-high confidence generally. It accepts
the two contained vehicle preference misses, while identifying long-duration
per-context demand convergence and stale release/save-load as the chief remaining
risk because five simultaneous committed harvester contexts produced larger
overlapping local counts than the two measured originals alone imply. This is the
literal scripted acceptance and clean adversarial 1 after the latest relevant
fix. Artifacts:
`analysis/worker-4-cnc42/games/g5-static-sam/{changed-run4,control-run4}`,
`analysis/worker-4-cnc42/g5-cycle12-comment/NARRATIVE.md`, and
`analysis/worker-4-cnc42/g5-cycle12-policy/POLICY-REVIEW.md`.

### Post-cycle-12 G4 adversarial

Without a product change, the cycle-12 build next ran the materially different
G4 toxic/full-storage/refinery-contention/kiting scenario. Changed cleanly reached
tick 6000 with no hard infantry-resource or all-role traffic violation, proved the
tick-751 aborted unload did not commit before storage release and later success,
and ran about 299.47 ticks/second. The concurrently launched control advanced
through the tick-3751 outcome and tick-3901 sample, then ended naturally after all
eight raiders disappeared simultaneously; the harness reports it duration-invalid.
The prior valid same-map/seed/base control through tick 6000 remains the sound
full-duration comparator and is reused without recounting.

G4 exposed a real policy regression. Changed logged 24 vehicle preference misses
across green, blue, and red Tiberium (nine Medium Tank and fifteen Mobile SAM),
spanning the barrier rather than representing one contained contact. At tick 3751
changed kept both harvesters at 35,000/29,000 versus prior control's 16,250/35,000,
delaying its first loss to tick 4509 versus control 3915. By late evidence each arm
had nevertheless lost one original harvester, and changed converted its much
larger screen into only 3/8 kills at outcome and 4/8 late versus prior control's
5/8 and 7/8. This is not the decisive task improvement required.

The fresh Commenter classified the valid comparison as strong operational safety
and delayed economy loss but mixed/poor raid efficiency. Routine Policy Review
returned `bounded policy failure — not a clean adversarial pass`, high confidence
0.87, and recommended resource-clear fail-closed vehicle reform plus ingress-aware
role slots. Cycle 13 adopts the directly isolated fail-closed part: owned tanks
and MSAMs must again reject current resource cells and routes, but only infantry
projects active modifier ranges. It defers new raid-direction inference because
the earlier strict-current-resource G4 result already performed better and can
test this cause with a smaller change. The clean-three requirement resets.
Artifacts:
`analysis/worker-4-cnc42/games/g4-toxic-route/{changed-run6,control-run6,control-run5}`,
`analysis/worker-4-cnc42/g4-post-cycle12-comment/NARRATIVE.md`, and
`analysis/worker-4-cnc42/g4-post-cycle12-policy/POLICY-REVIEW.md`.

### Cycle 13

Removed the permissive vehicle resource fallback while avoiding cycle 11's
overbroad projected-zone starvation. Every owned role now rejects current
Tiberium and the configured two-cell margin at claim, destination, exact route,
nudge, and every ordinary Mobile transition. Only infantry additionally rejects
the projected range of active resource modifiers. Focused policy/exact-route
tests passed 24/24; Release `make test`, Debug warnings-as-errors `make check`,
and the final Release `make test` all passed with zero warnings/errors. The
complete product identity is
`7124c95d0196599aa8036954642fe2c6249ec49d11388637a4b1806bd55233a3`;
changed Common/CNC assemblies are
`438b8f32c265971f68f905151bb78418337e94fa26b7083d3cc26fea2f532b8f`
and `11b083e534d47dfb947e5bcb0ed219d0c0e94e42864661117973fb8ff3df03da`.

The changed G4 seed-424306 arm passed tick 6000 at about 299.39 ticks/second.
It logged zero hard or preferred resource/traffic occupancy, retained the
tick-751 cancelled unload without a commit before tick-901 storage release, and
formed five defended fields by tick 1326. At tick 3751 both original harvesters
were alive at 34,000 and 34,525 health with pristine refineries; neither was
lost through the full run. The newly launched current control again ended
naturally after the tick-3751 outcome and is duration-invalid despite materially
advancing, so the prior valid identical base/map/seed control is reused without
recounting. That valid control had five of eight raiders killed at the outcome,
but its left harvester was already at 16,250 health and died at tick 3915; it
reached seven of eight kills only at tick 5423. Changed converted fewer kills
(2/8 at outcome, 3/8 late), but decisively prevented the old-control economy loss
while satisfying the fail-closed safety boundary.

The fresh corrected Commenter confirmed that survival contrast and excluded the
duration-invalid current arm. Routine Policy Review approved the result with
high confidence 0.86 and identified aggregate local unit count/opportunity cost
under constrained income and changing active fields as the strongest remaining
risk. This is clean adversarial one after the latest relevant product fix.
Artifacts:
`analysis/worker-4-cnc42/games/g4-toxic-route/{changed-run7,control-run7,control-run5}`,
`analysis/worker-4-cnc42/g4-cycle13-comment/NARRATIVE.md`, and
`analysis/worker-4-cnc42/g4-cycle13-policy/POLICY-REVIEW.md`.

### Post-cycle-13 literal/static confirmation

With no further product change, a fresh G5 seed-424307 pair both passed tick 6500.
Changed logged zero hard or preferred resource/traffic occupancy and no overlapping
economy-SAM request. Its normal BaseBuilder placed four distinct sites at 24,20,
67,16, 76,18, and 26,5; the control's three sites were 24,20, 52,28, and 62,25.
At tick 6001 changed retained both original harvesters at 35,000 and 23,000 health,
both refineries were pristine, and six of eight raiders were dead. Control had
already lost the right harvester at tick 4029 and its left refinery, with the
right refinery at 73,550 health, although it also reached six of eight kills.
Changed completed at about 324.42 ticks/second versus control's 341.56, roughly a
5% cost and within the required 10% limit.

The fresh Commenter confirmed useful, nonduplicated static coverage and the
economy-survival difference. Routine Policy Review accepted the result with
moderate confidence 0.74, while asking for bounded demand under multi-field scale
and topology. Artifacts:
`analysis/worker-4-cnc42/games/g5-static-sam/{changed-run5,control-run5}`,
`analysis/worker-4-cnc42/g5-post-cycle13-comment/NARRATIVE.md`, and
`analysis/worker-4-cnc42/g5-post-cycle13-policy/POLICY-REVIEW.md`.

### Post-cycle-13 G6 invalidation/contention adversarial

G6 retained the toxic/full-storage foundation and ordinary bot modules, added an
MLRS to activate Economy-artillery competition, began from six committed field
contexts, destroyed the reserved left-side Mobile SAM at tick 2601, destroyed an
extra committed harvester at tick 2751, and applied paired raids at ticks
3301/3401. The first changed arm passed tick 6500; its paired control advanced
materially but ended naturally before the configured duration, so both count but
the control is duration-invalid. A map-only duration sentinel then produced a
corrected pair in which both arms passed tick 6500.

In the corrected changed arm, the invalid Mobile SAM reservation released at tick
2701 and replacement coverage existed at tick 2751. The destroyed harvester's
field released at tick 2951, reducing active fields from six to five and dropping
stale demand. There was no resource/traffic occupancy signal, no crash/desync, and
no ownership churn. At the tick-4201 outcome both original harvesters survived at
12,500 and 35,000 health, both refineries were pristine, and changed had killed
3/8 raiders. Control had lost the right harvester around tick 3980, damaged the
right refinery to 80,496, and killed 0/8 by the same outcome; each arm reached 5/8
late. Changed ran at about 360.49 ticks/second versus control's 324.56, about 11%
faster rather than slower.

The fresh policy review supports the prompt release/replacement and bounded
per-working-field behavior at moderate confidence, with field exhaustion,
unreachable topology, and wider proportionality still requiring pressure. This is
clean adversarial two after cycle 13. Artifacts:
`analysis/worker-4-cnc42/games/g6-invalidation/{changed-run1,control-run1,changed-run2,control-run2}`,
`analysis/worker-4-cnc42/g6-comment/NARRATIVE.md`, and
`analysis/worker-4-cnc42/g6-policy/POLICY-REVIEW.md`.

### G7 topology and cycle 14 persistence failure

G7 first exercised stock CNC Archipelago with ordinary bots, then used a focused
Archipelago-derived map with reachable economy contexts, an unreachable-domain
Mobile SAM fixed at 91,96, storage-full cancellation, active resource projection,
static SAM coverage, and paired ground/air raids. The valid focused cycle-13 pair
passed tick 6500 at 240.352 changed versus 240.341 control ticks/second. Changed
kept both measured harvesters alive at 35,000 and 32,697 health, retained both
refineries, killed three of six raiders, and never assigned the unreachable actor;
control lost its far harvester. A pre-commit negative save loaded cleanly. A fresh
old-control post-commit save also loaded to tick 5200, isolating the changed
product when its post-commit save desynchronized during replay.

Cycle 14 replaced the bot module's direct world-affecting safety toggle with a
synchronized actor order. Its safety-cell payload is deduplicated, sorted by
`CPos.Bits`, bounded to 2048 configured cells, and fail-closed at assignment.
Focused serialization/policy tests passed 25/25. The complete cycle-14 product
identity was `5fdeb3513fe6118dbbf61044aa9ef9bbf652ae87b0f6b79f24eff63e2675962e`;
Common/CNC assemblies were `948a04c677fa5718f51dbda837a35eb08b040d395a1af0802974b26fdf8968dd`
and `11b083e534d47dfb947e5bcb0ed219d0c0e94e42864661117973fb8ff3df03da`.

The fresh cycle-14 run passed tick 6500 and wrote current save SHA-256
`039aeb443f16bd2d8460897d38535246a635f009a868c30f50b6d42f2e9b6f64`
at tick 3200. The exact reload reproduced the live sample trace byte-for-byte
through tick 3201, proving the previous replay desync was fixed. It then failed
before the loaded world's first tick with `IndexOutOfRangeException` in
`FieldLoader.ParseCPos` while resolving `EconomyFieldDefenseBotModule` line 856.
Diagnosis: `FieldSaver.FormatValue(CPos[])` flattens every comma-delimited cell,
but the generic array loader splits the same commas before parsing each `CPos`.
This is a current product persistence failure; the save becomes stale after the
next code change and will not be reused as proof.

The fresh run itself retained storage-cancellation, safety, topology, release,
and reassignment invariants but lost its main harvester at tick 2137 and kept the
far harvester at 34,000 through the tick-4201 outcome, with 4/6 raiders killed.
The Commenter treats the current reload as an evidence-integrity blocker and the
old-control load only as isolation. Routine Policy Review returns `insufficient
evidence`, high confidence: persistence must be repaired before the main loss or
two-field allocation is judged. Artifacts:
`analysis/worker-4-cnc42/games/g7-archipelago/{focused-changed-run4,focused-control-run4,load-negative-run1,focused-control-save-run1,load-control-run1,focused-cycle14-run1,load-cycle14-run1}`,
`analysis/worker-4-cnc42/g7-cycle14-comment/NARRATIVE.md`, and
`analysis/worker-4-cnc42/g7-cycle14-policy/POLICY-REVIEW.md`.

### Cycle 15 parse-safe save, mismatched continuation

Cycle 15 changed only saved destination representation: each `CPos` is persisted
as its integer `Bits` value and explicitly reconstructed after the generic integer
array loader. A focused test exercises the exact `FieldSaver`/`FieldLoader`
round trip; the policy/save fixture passed 26/26. The cycle-15 product identity is
`c7c0aa84386a1cdafd429ace1553c368e9413f9628021cf4744ee8d44ad70e33`;
Common/CNC assemblies are `501722532c6fee0c4cb63d4acc3fb0d8e7cd89dd6854d27efe93103c366f1746`
and `11b083e534d47dfb947e5bcb0ed219d0c0e94e42864661117973fb8ff3df03da`.

The fresh run passed tick 6500 and wrote save SHA-256
`3f640befc36c94b74c817de493351ce7ba4c961c5bcd9ed4f1784686091423e7`
at tick 3200 with two committed fields and 2/4/2 assignments. Its exact load
replayed identically through tick 3201, restored two fields at tick 3202, avoided
all exception/desync/resource/traffic signals, kept the unreachable MSAM at 91,96,
ran both raids, and exited cleanly at tick 5200. This proves the parse repair.

The harness still failed its expected field-release marker for a substantive
reason. In the uninterrupted run harvester 230 died and its field released at
tick 4076; at the reload outcome both measured harvesters remained alive at
25,000 and 35,000, no field released, and the module later grew to three fields.
Assignment and destination identity survives, but `lastOrderTicks`,
`routeProgress`, and `routeRejectedUntil` are behavior-affecting state cleared by
restore. The first post-load scan can therefore generate a different order stream.
Cycle 15 is a persistence-continuity failure and provides no clean-adversarial
credit. Artifacts:
`analysis/worker-4-cnc42/games/g7-archipelago/{focused-cycle15-run1,load-cycle15-run1}`.

The fresh Commenter confirms that the missing release is a substantive
persistence-equivalence failure, not a launcher false negative. Routine Policy
Review returns `mixed`, high confidence: preserve the bounded combined-arms policy,
but restore the exact field/harvester lifecycle and add actor-level restoration
and combat telemetry before judging the reload's apparent improvement. Artifacts:
`analysis/worker-4-cnc42/g7-cycle15-comment/NARRATIVE.md` and
`analysis/worker-4-cnc42/g7-cycle15-policy/POLICY-REVIEW.md`.

The mandatory cycle-15 reviewer raised one advisory that `strictAvoidCells` might
be lost while its `[Sync]` safety boolean survives a load. This is rejected after
source verification. OpenRA game saves replay the synchronized
`SetMoveAlongPathSafety` actor order before resolving arbitrary trait data; its
order resolver deterministically reconstructs both the boolean and canonical
avoid-cell set. `[Sync]` contributes to hash reporting rather than serializing the
field, and the cycle-15 replay reproduced live pre-boundary movement byte-for-byte.
Cycle 16 will still retain a post-load, pre-first-scan forbidden-cell assertion as
defense-in-depth evidence. Review artifact:
`analysis/worker-4-cnc42/cycle-review-15/CYCLE-REVIEW.md`.

### Cycle 16 bounded route-state restoration

Cycle 16 persists actor-keyed last-order cooldowns, route progress
(`BestDistanceSquared`, last-progress tick, and en-route state), and unexpired
route-rejection deadlines as nested primitive scalars. Restore accepts route state
only for live owned assigned actors and rejects retry deadlines outside the
configured future bound. Debug evidence identifies every restored actor without
adding per-tick logging. The nested save round-trip fixture passed 27/27; Release
`make test` and Debug warnings-as-errors `make check` passed with zero warnings or
errors. The complete staged binary-diff identity is
`c8cad8cb3ca7cf327af12a38c0d0368ec672836c85067ae94c62ca057605caf5`;
the exercised Release Common/CNC assembly hashes are
`40401d5691cfd91e889567ecffa584d68de6d898a5932ffd221ee8e640624340`
and `fc7cdcf6a56f9e6528bd9fff6b94c7719d00243180652cbfa054bc0a02a4f9b3`.

The fresh seed-424310 G7 run passed tick 6500 and wrote exact save SHA-256
`ca4e45e5df0144ade825be7c88bfcf67b59384b85438517027277781d0c6d8cc`
at tick 3200. Its exact load restored seven defender route records at tick 3201,
did not issue the cycle-15 tick-3202 idle retry, kept the unreachable MSAM fixed,
logged no resource/traffic/desync/runtime failure, and exited normally at tick
5200. Both executions grew to four fields and retained the original harvesters at
exactly 34,888/35,000 and 34,902/35,000 through the tick-4201 outcome, with no
field-230 release.

The load launcher reports failed only because an exact `raid=3/6` fresh-outcome
regex observed `2/6`; all other required and forbidden assertions passed. That is
a real one-kill combat-continuation variance and remains visible rather than being
rewritten as a green launcher result. The save boundary coincided with one field
station transition: load reprocessed that transition one tick later as
`new-destination`, but no restored actor received the forbidden immediate
`idle-retry`. The predeclared lifecycle, ownership, field-growth, health, topology,
and safety oracle passes. The factual Commenter nevertheless classifies exact-load
equivalence as limited/failed because the main E3 survives and the loaded screen
later declines to zero tanks and zero Mobile SAMs. Routine Policy Review returns
`mixed`, medium confidence, and withholds acceptance until each release names its
concrete owner/invalidity, active-field vacancy and eligible replacement, alongside
an actor-level raid damage/target timeline. Cycle 16 therefore receives no clean
persistence credit. Cycle 17 is bounded diagnostic work, not a balance or tactical
policy change: it will expose release ownership/role vacancy/replacement eligibility
and the causal raid timeline, then repeat the same fresh/exact-load comparison.
Review artifacts:
`analysis/worker-4-cnc42/g7-cycle16-comment/NARRATIVE.md` and
`analysis/worker-4-cnc42/g7-cycle16-policy/POLICY-REVIEW.md`. Game artifacts:
`analysis/worker-4-cnc42/games/g7-archipelago/{focused-cycle16-run1,load-cycle16-run1}`.

### Cycle 17 release and combat attribution

Cycle 17 changes diagnostics only. Each prune now names the exact invalidity or
reservation owner, active field and vacated role, remaining/target count, and the
first bounded eligible replacement. The task-local map adds stable-label raider
and defender damage/destruction events plus change-only raider position/health
samples. It does not change unit stats, tactical policy, assignment targets, or
raid commands. Focused tests passed 27/27; Debug `make check` and Release CNC
`make test` passed with zero warnings/errors. Product identity is
`5fd9fb5ec2a7fb6bbc328b0f29ee8b8f5529b74584f7732dbbfa3d5503809050`;
Release Common/CNC hashes are `6136233b...bb75d` / `e00b3f26...6199`.

The first launch reached tick 6500 but resolved the inherited cycle-16 ignored
map copy, so it is counted as an invalid setup and excluded from the useful pair.
The corrected fresh run used exact map SHA-256 `892fa9de...b933`, passed tick
6500, and wrote save SHA-256 `44a2d9cd...6b15`. Its exact load passed tick 5200.
Both arms killed the main bike at tick 3321 and E3 at tick 3531 through identical
damage chains; the static SAM killed the helicopter at tick 3350 fresh versus
3355 load. Both therefore reproduced the required `3/6` checkpoint, retained both
tracked harvesters and processors, kept the unreachable MSAM at 91,96, and logged
no resource-occupancy, traffic, desync, or runtime failure. Main harvester health
matched at 23,031; far health was 8,600 fresh versus 9,000 load.

The earlier generic release concern is not an unexplained other-owner theft in
this pair. Exercised reasons were missing actors, refinery traffic, and resource
invalidation, all with vacancy and replacement evidence. Fresh released MSAM 235
at tick 3401 because it occupied resource and no replacement was eligible; load
retained it and ran the corresponding scan one tick later. This remaining scan/
release cadence variance was reviewed factually at
`analysis/worker-4-cnc42/g7-cycle17-comment/NARRATIVE.md`. Routine Policy Review
returns a conditional pass with one bounded follow-up: make defender 235's first
post-save release/re-form decision agree without changing tactics or balance
(`analysis/worker-4-cnc42/g7-cycle17-policy/POLICY-REVIEW.md`). Raw save evidence
isolates the cause: the tick-3200 save stores a relative scan countdown of one,
which uninterrupted play consumes at tick 3201, but load resolution happens
after that tick's bot processing and therefore moves all later scans to 3202,
3227, and 3402. Actor 235's assignment and last-order tick are present. Cycle 18
will persist the absolute scan phase and resume at 3226/3401 after the missed
boundary, with legacy countdown fallback and no tactical tuning. Artifacts:
`analysis/worker-4-cnc42/games/g7-archipelago/{focused-cycle17-run1,focused-cycle17-run2,load-cycle17-run1}`.

### Cycle 18 absolute scan-phase persistence

Cycle 18 persists the absolute next field-defense scan tick alongside the legacy
relative countdown. New-format loads preserve the original cadence; legacy loads
reconstruct the saved frame's absolute boundary, skip a boundary already consumed
before trait-data resolution, and resume at the next original interval. Six new
phase cases brought the focused policy/persistence fixture to 33/33. Debug and
Release builds, interface checks, and CNC MiniYAML passed with zero warnings or
errors. Final pre-cleanup Common/CNC hashes were `9f04ce63...adf` and
`922097ee...6cd`.

The first fresh attempt reached tick 6500 but is invalid because a task-map
`Player` override changed bot-trait ordering. The corrected exact-rules-shape
fresh run reached tick 6500, wrote a new-format save containing absolute
`NextScanTick: 3201`, retained both harvesters and pristine processors, reached
`raid=3/6`, and logged no safety/traffic/runtime/desync failure. Its naturally
different assignment order did not reproduce the save-specific actor-235 event,
so only that deliberately overstrict marker failed.

Loading cycle 17's exact legacy save SHA `44a2d9cd...6b15` under the final fallback
passed tick 5200. It restored `saved=1 next=3201 current=3201 ticks=25`, scanned at
3226 and the original cadence thereafter, released Mobile SAM 235 at tick 3401
for current resource with anti-air vacancy 0/1 and no eligible replacement, and
did not re-form it at tick 3402. The load retained `raid=3/6`, zero harvester
losses, and pristine processors without safety, traffic, runtime, or desync
failure. The fresh Commenter verified the distinct fresh/legacy roles; routine
Policy Review returned `PASS, with bounded follow-up concern`, medium-high
confidence. Actor ID 235 is rejected as a fresh-run oracle because identities and
natural assignment ordering are save-specific; the new-format fresh save plus
exact legacy event jointly close the bounded phase defect without tactical or
balance tuning. Artifacts:
`analysis/worker-4-cnc42/games/g7-archipelago/{focused-cycle18-run1,focused-cycle18-run2,load-cycle18-run1}`,
`analysis/worker-4-cnc42/g7-cycle18-comment/NARRATIVE.md`, and
`analysis/worker-4-cnc42/g7-cycle18-policy/POLICY-REVIEW.md`.

### Cycle 19 publication diagnostics cleanup

All task-owned mobile field-defense and Brutalis/Iron Reaper economy-SAM debug
switches are false in published CNC rules. The untracked G7 archive was moved out
of `mods/cnc/maps` into ignored analysis storage with SHA preserved, so task test
content is not shipped. The exact publication scoped product-diff SHA was
`0c487627dbe05a4d5933317ff306eed26fbe279c4a1511e0cdc72a79a5cd3139`;
Release Common/CNC hashes were `9f04ce63...adf` / `922097ee...6cd`, and final
`ai.yaml` SHA was `b5bfb588...873d`.

Focused tests passed 33/33. `make check test` passed Debug/Release builds,
interface checks, and CNC MiniYAML with zero warnings/errors; a post-move
`make test` rerun passed without discovering the task map. The fresh
debug-disabled G7 publication run passed tick 6500 at 270.5 ticks/second and wrote
save SHA-256 `0ba298b2dd31ac8e918d75f0baf277c98a55d4cfdc4b39f597235b7411b8efce`.
At tick 4201 it retained both harvesters at 34,120/35,000 and 35,000/35,000,
the processor was pristine, the unreachable Mobile SAM remained at 91,96, and
the raid outcome was `3/6` with zero harvester loss. Neither task debug prefix nor
any runtime/desync failure appeared. The no-content configuration stop and wrong
content-root mod-content UI launch advanced no game ticks and are excluded.
Fresh factual review confirmed the run; routine Policy Review found the result
policy-compatible at high confidence with no bounded concern. Artifacts:
`analysis/worker-4-cnc42/games/g7-archipelago/focused-cycle19-run2`,
`analysis/worker-4-cnc42/g7-cycle19-comment/NARRATIVE.md`, and
`analysis/worker-4-cnc42/g7-cycle19-policy/POLICY-REVIEW.md`.

### Cycle 20 final-review response: ordinary SAM ownership

The final Sol-high PR review blocked publication on one concrete defect: Economy
II identified SAM control only by actor type, removed every SAM from the ordinary
authored `BuildingFractions` path, and sent every completed SAM through
economy-only placement. Once all economy anchors were covered, normal general
base air-defense production could therefore be suppressed or cancelled.

Cycle 20, the one permitted response and the isolated cap, retains economy build
ownership on the exact runtime production-queue object plus actor type from
selection through placement. Only that matching queued build uses economy
placement; ordinary SAMs keep their unchanged authored fraction and normal
general-defense placement. A focused test covers exact queue/type identity,
queued-lifetime retention, and release after completion/cancellation. Focused
tests passed 34/34. `make check test` passed Debug and Release compilation,
interface checks, and CNC MiniYAML with zero warnings/errors.

The fresh full-engine regression deliberately began with all economy anchors
covered by the powered SAM at 47,17: active Refinery, Resonator, and materially
used Silo squared distances were 50, 37, and 37 against effective coverage
radius-squared 64. It used ordinary Brutalis/Skynet, all normal modules, the real
unchanged one-percent authored SAM fraction, and no BaseBuilder trait override;
100 ordinary pre-placed walls made the fraction eligible. One SAM remained through
tick 3701. Sites at 42,35 and 47,35 appeared by tick 3801, and 40,35 appeared by
tick 4801, all outside the economy-anchor placement annuli. At tick 5001 four SAMs
and all three economy anchors were alive. The engine exited 0 at tick 5200 with
replay/benchmarks and no economy reservation, Lua/runtime, or desync signal.

The launcher status remained failed only because two predeclared expressions were
overstrict: BotDebug did not emit an optional authored-fraction sentence, and the
outcome required exactly two sites rather than the observed four. The preceding
attempt is counted as an invalid advanced setup: it created a second site by tick
3401 and then crashed when its observer used unsupported Lua `ActorID`. A tick-0
wrong-content-root attempt is excluded. The factual Commenter confirms the direct
additional-site observation but limits causality for lack of a matched control and
queue-decision trace. Routine Policy Review returns `insufficient evidence`, high
confidence, for strategic benefit—not a finding that the ownership repair is
unsound—and recommends bounded ownership observability plus an identical control.
Those recommendations, the post-fix clean-three, and final stressed literal run
cannot begin a cycle 21 and remain handoff work. The mandatory cycle-20 Terra
review adds one valid implementation blocker: exact queue/type ownership is not
persisted, so loading after economy reservation but before placement can demote
that completed build to ordinary placement. This advisory is adopted; persisting
or deterministically reconstructing ownership plus an exact mid-build load
regression would require forbidden cycle 21. Artifacts:
`analysis/worker-4-cnc42/final-review/FINAL-REVIEW.md`,
`analysis/worker-4-cnc42/games/g5-cycle20-covered-general`,
`analysis/worker-4-cnc42/g5-cycle20-comment/NARRATIVE.md`, and
`analysis/worker-4-cnc42/g5-cycle20-policy/POLICY-REVIEW.md`, and
`analysis/worker-4-cnc42/cycle-review-20/CYCLE-REVIEW.md`.

## Final isolated assessment

- Behavior: post-unload committed field contexts drive bounded 1/2/1 mixed
  defender demand, stable reservations, owned production requests, fail-closed
  Tiberium/refinery-safe movement, local re-form/release, and economy-aware SAM
  placement through normal BaseBuilder ownership. No unit, weapon, building,
  resource, probability, or unrelated composition balance value changed.
- Determinism/persistence: actor IDs, field membership, destinations, stances,
  synchronized safety snapshots, route cooldown/progress/rejections, and absolute
  scan phase are persisted with bounded validation. The final legacy regression
  reproduced the formerly missed tick-3401 release without desync.
- Comparative outcome: decisive valid comparisons include G2 right-harvester
  survival versus control, G4 prevention of the control harvester loss under toxic
  geometry, G5 preservation of harvesters/refineries with useful SAM placement,
  G6 prompt invalidation/replacement with a surviving economy, and G7 preservation
  of reachable economy while withholding the unreachable actor. Recorded MAX
  cost ranged from effectively equal/faster to about 5% slower in the accepted
  matched G5 pair, within the required 10% bound.
- Evidence total: 66 counted full-engine tests through 20 product-change cycles.
  Invalid advanced setups are labeled; tick-0/map-unavailable attempts and reused
  controls are excluded. Required Terra cycle reviews at 5/10/15/20 were completed;
  their two safety advisories were adopted and the cycle-15 serialization premise
  was rejected with source and replay evidence. The cycle-20 save/load ownership
  advisory is adopted as deferred because no cycle 21 is permitted. Sol-xhigh
  policy escalation was unused.
- Status rationale: propose `First iteration - testing`, not completion. The
  final-review response is a relevant cycle-20 behavior correction, so the strict
  clean-three counter resets again; no cycle remains for three fresh adversaries
  or the strongest specified two-separated-field final regression. The cycle-20
  full-engine case proves ordinary SAM construction remains available but lacks a
  matched control and explicit queue-decision trace. Combined testing must also
  persist exact economy-SAM queue/type ownership across a save made between
  reservation and placement, resolve CNC-41 PR #88's overlapping
  BaseBuilder/queue/config branches, and rerun G4/G5/G7.

## Diagnostics and retained seams

Task debug switches are published false. Rate-limited diagnostics remain behind
those explicit rules flags for future evidence builds. No task map, raw log,
replay, save, benchmark, or build output is tracked. Retained product seams are
the post-unload field-station trait, owner-aware external production requests,
field-defense reservation module, exact synchronized path/safety order, nudge and
cell-entry validators, and the narrow exact-queue-owned BaseBuilder economy-SAM
planner.

## Deferred work

- Integration validation: combine CNC-41 PR #88's independent Tiberium-field
  manager with CNC-42's economy-SAM planner in the shared BaseBuilder and queue
  surfaces, then rerun toxic geometry/traffic, low-power/static-SAM, and
  Archipelago save/load scenarios on the reviewed release candidate.
- Completion evidence: obtain three distinct clean post-persistence adversaries
  and the strongest specified fresh literal final regression before promoting
  beyond `First iteration - testing`.
- SAM ownership evidence: add bounded queue/selection/completion ownership
  diagnostics in an evidence configuration and run an identical covered-anchor
  old-behavior control plus air harassment, without changing authored fractions
  or balance.
- Save/load ownership: persist or deterministically reconstruct the exact
  economy-reserved SAM queue/type ownership, then load a save made after
  reservation but before placement and prove the completed site still takes the
  economy-safe anchor location. This is the adopted cycle-20 review blocker.

## Publication handoff

The tested product head is
`84dbf5013d8b6b3c696e8d6f80f24c7be00f1a23` on
`agent/round-20260807-cnc42-economy-field-defense`. Draft PR #89 targets the exact
recorded base, is mergeable, and CI run 31181233145 passed Linux .NET 6.0 and
Windows .NET 6.0. The final branch update after that product head records only
this handoff receipt and report publication metadata.

Handoff status is `First iteration - testing`: all 20 isolated cycles and the one
allowed final-review response are consumed. Do not infer completion from the
green build. The adopted mid-build save/load ownership blocker, reset clean-three,
stressed final regression, stronger cycle-20 causal control, and combined CNC-41
validation remain required before promotion.

## Integrated RC1 repair and evidence

The isolated blocker was exact economy-SAM build ownership living only in a
runtime `ProductionQueue` reference. Product commit
`b6e7eecf15a6993a2349b1595ffb2c350582d976` persists the queue actor ID, queue
type, actor type, and reservation tick. Load reconstructs ownership only against
the original owned/live queue when that exact build is still queued, and safely
discards stale or incompatible records. Legacy saves remain valid. The focused
suite now passes 35/35, `make check && make test` passes cleanly, and the full
test assembly passes 513/513.

Six counted integrated games raise the task total from 66 to 72. G5 discovery
located the reservation window. G5 then saved at tick 300 before the first SAM
placement; uninterrupted and loaded play both chose economy cell `41,17`, while
the load explicitly restored queue `328/Defence.GDI`. Combined CNC-41 G4 reached
tick 6000 with both original harvesters/processors alive and all eight raiders
dead. Combined G7 reached tick 6500 with both harvesters/processors alive, all six
raiders dead, no path spam, and the unreachable MSAM correctly withheld. Source
confirms that `unreachable-domain` is rejected before assignment/reservation, so
the G7 policy review's possible ineffective-ownership condition is resolved.

The stressed final reached tick 9000. Literal left/right screens were ready
before the delayed raids; defender totals recovered after the scripted casualty;
both original harvesters and pristine refineries survived; both measured
harvesters completed later unloads after contact; seven of eight raiders died;
and three SAM sites were live at the final sample. No forbidden occupancy,
preferred-placement miss, refinery traffic failure, runtime/Lua error, or desync
occurred. The launcher summary's three misses were evidence-expression errors,
not game failures: two patterns expected `tick=` where the raw engine text says
`at tick`, and one sampled local recovery before the later literal-screen line.
The factual narrative preserves the exact discrepancy. Routine policy's
conditional evidence request is satisfied by those raw later screen and unload
lines; rerunning the unchanged successful game would violate the no-repeated-
happy-path rule.

This uses one integrated product cycle (`1/3` this RC, `1/12` total). Draft PR
#92 targets the exact integrated release branch. CI run `31185660304` passed
Linux .NET 6.0 in 2m14s and Windows .NET 6.0 in 4m29s on the tested product head.
Task debug is disabled and raw saves, maps, logs, replays, and benchmarks remain
ignored. The integrated handoff status is `Complete - testing`.

## Integrated RC1 final-review response

The final repair reviewer found one remaining evidence defect, not a product
defect: the stressed final lacked its required matched old-behavior control and
three assertions tested a fixed early recovery sample or the wrong unload log
syntax. I corrected the ignored harness to track actual casualty application and
replacement, require both literal screens before fixed raids, accept the engine's
`at tick N: unload completed` syntax, and run identical changed/control scenario
timing. No code, rules, balance, or integrated-cycle count changed.

Both same-build arms used product head
`b6e7eecf15a6993a2349b1595ffb2c350582d976`, identical assemblies, seed
424307, GDI Brutalis versus Nod Skynet, starts/options/initial actors, the
storage-full cancellation, casualty, power recovery, and raids at ticks
6201/6301. The control's only map-rule difference disabled CNC-42 mobile and
Brutalis economy-SAM activation by restricting both to Iron Reaper. Both jobs
passed through tick 9000 with exit 0.

Changed applied the casualty at tick 4502, recovered its infantry total by 5001,
and formed both separated 1/2/1-or-better screens at tick 5926 before contact.
Measured harvesters 342 and 341 unloaded after contact at ticks 6238 and 6889;
all eight raiders were dead by 7323; both measured harvesters and refineries were
pristine at ticks 7701 and 8501; and three SAM sites were live. It emitted no
resource-safety, refinery-traffic, pending-SAM, runtime, Lua, desync, or fatal
signal. Old behavior timed out without screens at tick 6151, had only seven kills
at 7701, and left its measured left harvester at 4,100/35,000 plus its left
refinery at 47,098/100,000. It recovered and killed the last raider at 8256, and
both harvesters later unloaded, making the comparative credit precise: 933 ticks
faster raid clearance and prevention of 30,900 harvester damage plus 52,902
transient refinery damage, not an artificial control collapse. Changed wall time
was 29.080 seconds versus 28.069, about 3.6% slower and within the 10% limit.

The fresh factual Commenter accepts the pair as valid and the local readiness and
damage-prevention difference as decisive. Fresh routine Policy Review returns
`mostly sensible`, medium confidence, and recommends retaining the unload-gated
mixed screen and economy-SAM policy. Its air-only/ground-only proportionality and
unchanged-unsafe-route retry suggestions are bounded next-round hypotheses; the
pair exposes no task defect requiring another product cycle. The staged persistent
scratchpad was not rewritten by the current policy role, so the missing
replacement was rejected and the canonical scratchpad retained unchanged.

This adds two counted games for a total of 74. The sole repair-review correction
passes, `Complete - testing` remains supported, and the tested product head remains
`b6e7eecf15a6993a2349b1595ffb2c350582d976`. Evidence is under
`analysis/worker-4-cnc42/rc1-review-response/`, including the paired manifest/run,
fresh `commenter/NARRATIVE.md`, and fresh `policy/POLICY-REVIEW.md`.
