# CNC-43 task report — MCV crush flavor

## Result

Implemented the literal config-only change. CNC's normal `mcv` now uses a dedicated
`mcvheavywheeled` locomotor whose crush set exactly matches Mammoth
`heavytracked`: `wall, heavywall, crate, infantry`. Its terrain speeds remain
the pinned `heavywheeled` values. `FACT.TransformsIntoMobile` uses the same
locomotor so repack placement and the live MCV stay coherent.

No engine, AI, target, weapon, cost, health, speed, turn-rate, production-policy,
Mammoth, or Stealth Tank value changed.

## Design and assumptions

- Added one actor-scoped world locomotor instead of assigning MCV to
  `heavytracked` (which would import tracked terrain behavior) or broadening
  shared `heavywheeled` (which would buff STNK).
- Updated only the two owning references: `MCV.Mobile.Locomotor` and
  `FACT.TransformsIntoMobile.Locomotor`.
- Campaign MCV overrides inherit the shared actor and therefore receive the
  capability without map-specific edits.
- Test maps, bounded Lua diagnostics, logs, replays, manifests, builds, and
  resolved rules remain ignored outside Git. No product logging was retained.

## Product diff

- `mods/cnc/rules/world.yaml`: add `mcvheavywheeled` with Mammoth crush
  classes and the exact old MCV terrain-speed table.
- `mods/cnc/rules/vehicles.yaml`: point MCV at the dedicated locomotor.
- `mods/cnc/rules/structures.yaml`: point FACT repack validation at it.

One product/config cycle was used. Later iterations changed only ignored evidence
harnesses and assertions.

## Static and resolved-rule evidence

Evidence root:
`/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260806-bug-polish-01/analysis/worker-3-cnc-43/resolved`.

Base and changed resolved outputs were saved for World, MCV, HTNK, STNK, and FACT.
The changed MCV differs only by locomotor name; FACT differs only by its matching
transform locomotor. The new locomotor resolves to exactly
`wall, heavywall, crate, infantry` with Clear 90, Rough 63, Road 190, Bridge
190, all three Tiberium types 63, and Beach 50. The shared `heavywheeled`
locomotor remains `crate, infantry`.

HTNK resolved checksum is identical before/after:
`839a151a4c7dbfe900f1a610eebde402ade0a1697a39dd805b734279f920c704`.
STNK is also identical:
`2071a71415682d9a30a42420bc8ff557c6241d61a926b6837d4ab6d938de4296`.

`git diff --check` passes, and the publication diff contains only the three CNC
rules files plus this worker's state/report. There are no changes under
`OpenRA.*`, `mods/cnc/rules/ai.yaml`, other mods, committed maps, or generated
output.

## Full-engine evidence

Forty-six full-engine game tests were completed, including invalid harness runs
that produced materially judged evidence. All games used the global capacity
lock, isolated support directories, headless MAX, ordinary real AI players, and
fresh artifacts.

### Review response: ordinary long-pressure MCV recovery

The required CNC-43-EVIDENCE-1 response used seed 43043 on normal connected
Empire Earth geometry with matched changed and pinned-base packages (package
SHA-256 `bf289b584ed449dc7ddd3fa4170385cfee1b49d63d1df5e031486e0e1b0b24e9`).
Both sides used the same four ordinary VIKI/Brutalis/SkyNet players and teams,
5,000 starting cash, normal AI modules, Yard undeploy, and no configured game
exit. Map rules only enabled bounded diagnostic logging on the ordinary bot
modules; they did not override actors, locomotors, targets, crush behavior,
production policy, or AI order policy.

The target VIKI deployed its initial MCV at tick 13 and completed a 16-wall
enclosure. Once it had a live factory and HQ, six Multi2 units applied real
hostile pressure. The first hostile damage triggered a deterministic one-HP
threshold so the next hostile artillery hit killed the Yard; the bounded wave
was then removed while all ordinary enemy modules continued. At Yard loss:

- Changed tick 5,931: cash 0/resources 443, zero Yards/MCVs, one factory, two
  harvesters, 16 walls.
- Base tick 4,431: cash 1/resources 563, zero Yards/MCVs, one factory, two
  harvesters, 16 walls.

Both VIKI instances entered the ordinary externally managed MCV production path
through their sole `Vehicle.*` queue (`combat-vehicle-queues=1/1`) and spent
4,000 while cash and production were contended. The status stream records the
pending MCV request; the request call itself has no tick-bearing log. The
tick-bearing production-spend marker is emitted when UnitBuilder accepts that
request, reserves the free queue, queues `StartProduction`, and records the MCV
as queued: tick 6,672 changed and 4,908 base. Changed produced at 8,408 with cash
1/resources 4,119; base produced at 6,823 with cash 0/resources 3,076. The
instrumentation therefore proves request accounting plus the exact
reservation/queue and normal-production ticks, although it cannot name the exact
non-MCV item occupying the queue at each instant.

Changed's recovery MCV moved at tick 8,470 and deployed at 9,282; base moved at
6,888 and deployed at 7,187. Both move records name
`McvManagerBotModule` as owner and report `scripted_mcv_orders=0`,
`combat_orders=0`, and `crush_orders=0`. Neither recovery MCV died. Both retained
all 16 enclosure walls throughout the recovery interval; no claim is made about
individual wall topology. The owner/counter evidence rules out the instrumented
scripted, combat, and crush paths, though the logs do not expose every internal
engine order packet.

Both runs reached natural game over beyond tick 20,000 with exit code 0 and no
configured exit, fatal Lua error, unhandled exception, desync, or recovery-MCV
death. Changed's target won at tick 22,770 after expanding to multiple Yards;
base's target lost at tick 22,997, long after successful recovery. This outcome
difference is not attributed to the locomotor: the preceding matched v4
repetition had the opposite winner direction, showing normal adaptive-match
divergence.

Runner throughput also diverged in v5 (78.522 changed versus 110.104 base valid
ticks/sec), but repeated aggregate per-world-tick telemetry did not reproduce a
regression: v4 changed/base means were 184.870/178.899 ms (+3.34%), while v5
were 215.411/243.805 ms (-11.65%); their two-run arithmetic means favor changed
by about 5.3%. Workload and outcome divergence prevent a causal performance
claim in either direction. The dedicated locomotor still adds no per-tick code
or allocation.

Ten full-engine games were added in this review response: early pairs exposed
only harness enablement, timing, actor-lifetime, pressure-threshold, and assertion
faults; the final v5 pair passed all lifecycle and natural-completion assertions.
No product defect was found, no product/config code changed, and the isolated
cycle count remains one. Raw evidence is ignored under
`analysis/worker-3-cnc-43/games/review-long-pressure-{changed,base}-v5/`.
Fresh factual narrative:
`analysis/worker-3-cnc-43/commenters/review-long-pressure-v5/NARRATIVE.md`.

### Literal changed/base differential

Seed 43001, focused map `CNC-43 Literal Crush Lane`, ordinary GDI VIKI and Nod
Brutalis, 20,000 cash, tick 2,000:

- Changed v5: the MCV removed the crate and killed E1, SBAG, BRIK, and APC, with
  every kill attributed to `mcv/Tester`; it arrived alive and transformed.
- Pinned-base v5: the MCV removed only crate and infantry, did not kill wall,
  concrete, or APC, then retained normal movement/deployment.
- Narrative:
  `analysis/worker-3-cnc-43/commenters/cycle1-v5/NARRATIVE.md`.

The stronger final differential used seed 43003 and an allied one-cell corridor:

- Changed `final-regression-v4b`: complete MCV and Mammoth matrices; STNK and
  medium-tank negative matrices; allied infantry/wall/APC survival; hostile wall
  killed only by MCV; MCV transform; tick 10,000 pass at 499.852 ticks/sec.
- Pinned base `final-regression-base-v2`: MCV matrix exactly
  crate/infantry=true and wall/concrete/vehicle/defense=nil, with no new-class
  kill by MCV; normal MCV transform still succeeded; Mammoth completed the same
  full reference matrix; tick 10,000 pass at 499.82 ticks/sec.
- Final matched narrative:
  `analysis/worker-3-cnc-43/commenters/final-matched-pair/NARRATIVE.md`.

### Adversarial scenario 1: class matrix and negative crushers

Seed 43003. The corridor-hardened full-engine map forces MCV and hold-fire Mammoth
through crate, mobile infantry, SBAG, BRIK, APC, and GUN twice. Both visibly
removed/killed every intended target with exact killer attribution. Hold-fire
STNK retained only crate/infantry behavior; hold-fire MTNK could not crush
heavywall targets. No forbidden kill, fatal Lua error, or desync occurred.

### Adversarial scenario 2: allied safety and order contention

In the same full-engine sequence, allied infantry, SBAG, and APC remained alive
while the MCV detoured after the corridor was removed, killed only the hostile
SBAG, arrived alive, and the scenario retained a successful MCV-to-FACT transform.
The separate lifecycle scenario exercised ordinary manager order ownership.

### Adversarial scenario 3: production, repack, and real managers

Seed 43004, `CNC-43 Production and Repack Lifecycle`, ordinary GDI VIKI versus
Nod Brutalis, tick 12,000:

- VIKI started with a normal WEAP/HQ/economy and no FACT/MCV.
- Its normal queue produced a harvester before the requested MCV, demonstrating
  shared queue contention.
- UnitBuilder produced `mcv/Multi0`; McvManager removed it and a normal FACT
  appeared.
- Brutalis's normal FACT repacked to `mcv/Multi1`; no Lua MCV movement/deploy
  order was issued, and ordinary McvManager redeployed it to FACT.
- Final booleans for bot production/deploy and repack/redeploy were all true.
- Pass: 12,000 ticks, 599.824 ticks/sec, no fatal/desync.
- Narrative:
  `analysis/worker-3-cnc-43/commenters/cycle3-lifecycle-final/NARRATIVE.md`.

The exact old MCV terrain table is proven in resolved rules rather than a
map-local actor override; runtime production and repack use that same normal actor
and coherent FACT locomotor.

### Ordinary matched control

Seed 43002 on normal connected `Empire Earth4`, four ordinary bots, fixed
teams/starts/options, tick 12,000:

- Changed and pinned-base runs both passed without fatal/desync.
- Changed: 85.138 seconds (140.943 ticks/sec).
- Base: 83.162 seconds (144.291 ticks/sec).
- Wall-time delta is +2.38%, below the 5% investigation threshold; Commenter
  benchmark analysis found mean overall tick time slightly lower in changed
  (5.223 ms versus 5.258 ms), with bot-tick variation attributable to ordinary
  simulation divergence.
- Narrative:
  `analysis/worker-3-cnc-43/commenters/ordinary-pair/NARRATIVE.md`.

### Natural-conclusion endurance

Seed 43005 on normal `Empire Earth4`, VIKI + Brutalis versus Brutalis + SkyNet,
standard 10,000 cash, no configured tick exit:

- Reached a genuine natural game-over after progress beyond tick 20,000.
- Summary duration 180.018 seconds and 111.098 ticks/sec through its recorded
  progress; benchmark telemetry extends to tick 23,217.
- No configured-exit marker, fatal error, exception, or desync.
- Narrative:
  `analysis/worker-3-cnc-43/commenters/natural-endurance/NARRATIVE.md`.
- The staged launcher telemetry does not identify the winning team; this limits
  strategic narrative only, not the required natural-completion/stability result.

## Final checks

- `make test`: pass; Release build 0 warnings/0 errors and all CNC MiniYAML/maps.
- `make check`: pass; Debug build 0 warnings/0 errors and interface checks.
- `make check-scripts`: pass.
- `dotnet test OpenRA.Test/OpenRA.Test.csproj --configuration Debug --nologo`:
  438 passed, 0 failed, 0 skipped.
- Final focused map `utility --check-yaml`: pass.
- Final changed/base regression pair: pass.
- Initial pre-edit `make test` and exact pinned-base `make test`: pass.

## Determinism, performance, and diagnostics

The final changed/base focused pair used the same map, checksum content, seed,
bots, factions, teams, starts, cash, options, route, and tick cap, differing only
by pinned-base versus task rules. Throughput was effectively identical
(499.852 versus 499.82 ticks/sec). The dedicated locomotor adds one static world
locomotor/cache entry and no per-tick code or allocation.

Early invalid runs exposed Lua-debug enablement, pathable-cell selection,
same-frame actor cleanup, transform placement, callback timing, and open-route
pathfinder avoidance. These were corrected only in ignored harnesses. Final
instrumentation is bounded to setup, production, target removal/kill, arrival,
transform, safety, and final one-shot markers. No diagnostic ships in product.

The review-response harness similarly progressed through enablement, trigger,
actor-lifetime, pressure-threshold, and assertion corrections without touching
product code. Its final matched package and lobby controls were identical. The
v4/v5 reversal in winner direction and non-repeating throughput delta are
reported as ordinary simulation/workload divergence, not evidence of a product
regression or benefit.

## Dependencies

Publication-time check:

- Intended base advanced from pinned `09ccdac3c1e` to
  `e09177bb8ddb4bce18fd028f6a0a3b72d79da9b0` through coordinator/role-launch
  metadata commits only; no CNC/engine product-file delta exists.
- PR #31 remains open at `4e65c05fed4e809578aea39a15fbac0f630cf66d`
  and still edits wall-planning AI, tests, launcher, and `ai.yaml`, not the
  MCV/locomotor product files.
- No CNC-45 branch or matching PR exists. No Mammoth/AI overlap was found.

## Publication

Branch: `agent/round-20260806-cnc43-mcv-crush-flavor`.

PR #78: https://github.com/Realpra1/LibertyDawn/pull/78

The branch was rebased onto current intended base
`e09177bb8ddb4bce18fd028f6a0a3b72d79da9b0`; the only conflict was the expected
coordinator-created worker-state add/add, resolved by preserving this completed
state. GitHub reports the PR mergeable and `CLEAN`. No status checks are
reported for the branch and `gh pr checks --required` reports no required
checks. The PR remains open and was not merged.

The CNC-43-EVIDENCE-1 review response adds only this durable state/report
summary. Its maps, manifests, logs, replays, benchmark CSVs, and factual
Commenter narrative remain ignored evidence outside Git.

## Deferred work

No product changes are deferred. Optional harness infrastructure improvement:
expose winner/elimination and per-player economy/combat telemetry from headless
ordinary matches so factual Commenters can narrate the winning team without replay
post-processing. This is outside CNC-43's config-only scope.

## Remaining risks

No known task defect. The long-pressure instrumentation identifies McvManager
ownership and zero scripted/combat/crush counters but cannot prove exclusive
authorship of every internal engine order packet. It records queue contention
without naming the exact blocking item at every request. Repeated ordinary AI
runs can diverge in workload, winner, and wall-clock throughput, so no strategic
or performance effect is inferred from a single pair. Terrain equality remains
resolved-rule/static evidence rather than per-terrain timed runtime telemetry.
These are low-risk limitations for a static actor-scoped locomotor whose exact
table is copied from and compared against the pinned MCV values. All normal
production/repack/recovery paths and hostile/allied collision boundaries passed
in the full engine.

## Integrated RC2 validation

### Candidate and result

Tested the exact cumulative RC2 release metadata head
`fd15540ffc98c70f085688fe0b38a4a6341fc6ed` and code candidate
`b456fd89fac88d71dfadd65c47cfb7b409d44122` from repair branch
`agent/round-20260806-cnc43-rc2-repair`. Result: **combined pass, no repair**.
No product/config change was made, so integrated code-change cycles remain 0/3
for RC2 and 0/12 total.

Six fresh full-engine games bring the CNC-43 total from 46 to 52. All used the
shared game lock, isolated support directories/maps/replays/benchmarks/displays,
ordinary real AI modules, and headless MAX.

### Combined adversarial behavior

The seed-43003 matrix/safety run passed at tick 10,000. The normal MCV killed or
removed crate, infantry, SBAG, BRIK, APC, and GUN targets with exact MCV
attribution, remained alive, and deployed. Mammoth completed the same matrix.
STNK retained only crate/infantry crushing, MTNK retained its negative behavior,
and allied infantry, wall, and vehicle survived while the MCV killed only the
hostile wall. No fatal/desync signal occurred.

The clean two-way seed-43004 lifecycle run passed at tick 12,000. VIKI's normal
vehicle queue produced an MCV and ordinary McvManager deployment created a Yard;
Brutalis repacked a normal Yard to MCV and redeployed it. This jointly exercises
the dedicated live-actor locomotor and `FACT.TransformsIntoMobile` validation in
the cumulative build.

The seed-43043 long-pressure confirmation used four ordinary
VIKI/Brutalis/SkyNet players, 5,000 cash, real queue contention, an enclosed
target base, hostile Yard destruction, and natural conclusion. Critical events:

- initial MCV deployment: tick 13;
- recovery trigger with one factory and all 16 walls: tick 5,183;
- Yard killed by `arty/Multi2`, still 16 walls: tick 5,252;
- normal VIKI 4,000-credit MCV production spend: tick 5,688;
- recovery MCV produced: tick 7,525;
- movement owned by `McvManagerBotModule`, with zero scripted/combat/crush
  orders: tick 7,590;
- normal transform removal/deployment to one Yard, all 16 walls intact: tick
  8,489;
- natural game over after tick 34,000, exit code 0, with no fatal, exception,
  desync, configured-exit, or recovery-MCV-death marker.

### Capacity trial and performance

The requested three-way trial launched matrix, lifecycle, and long pressure
together. Matrix passed; lifecycle reached every required success marker but
returned nonzero during benchmark/shutdown processing; long pressure reached
normal Yard destruction and MCV queue spend but ended before MCV production.
Batch elapsed time was 37.383 seconds, completion was 1/3, and sampled peak
aggregate process-tree RSS was 6,447,160 KiB on a 7.8 GiB host with no swap.

The two-way retest passed both lifecycle and long pressure: 2/2 completion,
143.593 seconds elapsed, 223.186 valid world ticks/second, and peak aggregate RSS
4,094,272 KiB. The serial enclosure confirmation passed in 473.825 seconds in a
Debug rebuild, with peak 1,305,896 KiB. The data establishes that three-way is
unreliable on this host, while two-way completed cleanly with about 2.24 GiB less
sampled peak RSS. Recommend reducing the shared capacity to two. The differing
batch workloads mean this is an operational reliability conclusion, not a claim
that CNC-43 changes runtime cost.

### Resolved rules and checks

Integrated resolved outputs are under
`analysis/worker-3-cnc-43/resolved/integrated-rc2`:

- MCV `mcvheavywheeled` and Mammoth `heavytracked` both resolve to exactly
  `wall, heavywall, crate, infantry`.
- MCV terrain remains Clear 90, Rough 63, Road/Bridge 190, all Tiberium 63,
  Beach 50; `Mobile.Speed` remains 60.
- FACT uses `mcvheavywheeled`; STNK uses shared `heavywheeled` with only
  `crate, infantry`.
- HTNK SHA-256 remains
  `839a151a4c7dbfe900f1a610eebde402ade0a1697a39dd805b734279f920c704`;
  STNK remains
  `2071a71415682d9a30a42420bc8ff557c6241d61a926b6837d4ab6d938de4296`.

Current cumulative gates pass: `make test`, `make check`, `make check-scripts`,
and 454/454 Debug unit tests, with zero build warnings/errors. `git diff --check`
passes and the repair branch has no product delta from the recorded candidate.
Draft PR #84 is mergeable; all four reported Linux/Windows CI checks are green.

PR #31 remains unchanged at `4e65c05fed4e809578aea39a15fbac0f630cf66d`.
No CNC-45 remote branch/ref was found, and no Mammoth, target class, AI order, or
balance value was changed.

### Evidence and review

Primary artifacts:

- `analysis/worker-3-cnc-43/games/integrated-rc2-three-way` and sibling resource
  JSON;
- `analysis/worker-3-cnc-43/games/integrated-rc2-two-way` and sibling resource
  JSON;
- `analysis/worker-3-cnc-43/games/integrated-rc2-enclosure-recovery` and sibling
  resource JSON;
- `analysis/worker-3-cnc-43/commenters/integrated-rc2/NARRATIVE.md`;
- `analysis/worker-3-cnc-43/commenters/integrated-rc2-enclosure/NARRATIVE.md`.

Fresh no-history factual Commenters verified the causal sequences and run
integrity. No Policy Reviewer was launched because CNC-43 remains literal
config-only behavior and no AI policy changed. No diagnostics ship in product;
the measurement wrapper, manifests, maps, logs, replays, and benchmarks are
ignored evidence only.

Remaining risk is unchanged: bounded logs identify McvManager ownership and zero
instrumented combat/crush orders but do not expose every internal order packet.
The cumulative full-engine matrix, lifecycle, enclosed recovery, natural match,
resolved-rule, and build evidence exposed no CNC-43 defect.
