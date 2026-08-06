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

Thirty-six full-engine game tests were completed, including invalid harness runs
that produced materially judged evidence. All games used the global capacity
lock, isolated support directories, headless MAX, ordinary real AI players, and
fresh artifacts.

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

## Deferred work

No product changes are deferred. Optional harness infrastructure improvement:
expose winner/elimination and per-player economy/combat telemetry from headless
ordinary matches so factual Commenters can narrate the winning team without replay
post-processing. This is outside CNC-43's config-only scope.

## Remaining risks

No known task defect. The natural-match artifact lacks winner identity, and
terrain equality is resolved-rule/static evidence rather than per-terrain timed
runtime telemetry. Both are low-risk for a static actor-scoped locomotor whose
exact table is copied from and compared against the pinned MCV values. All normal
production/repack paths and hostile/allied collision boundaries passed in the full
engine.
