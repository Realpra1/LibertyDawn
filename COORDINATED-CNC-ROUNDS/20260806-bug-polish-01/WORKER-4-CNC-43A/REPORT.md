# CNC-43A Flame Tank balance report

## Status

Complete - testing after 2 of 20 isolated code cycles and 36 completion-worthy full-engine games. Four tick-0 custom-map/save staging failures are documented in ignored evidence but excluded from the game count because they never exercised gameplay.

Task branch: `agent/round-20260806-cnc43a-flame-tank-balance`

Pinned control: `09ccdac3c1ecb5134a4751f2bcbd8a7970dfe6bf`

Product head: `6f3a33ea165e0b4b90d0e4a9c974b70a12f78a12`

## Implemented behavior and ownership

- `mods/cnc/rules/vehicles.yaml` changes only FTNK `Health.HP` from 30,000 to exactly 36,000.
- `mods/cnc/weapons/other.yaml` adds BigFlamer-local `SpreadDamage` overrides for `Warhead@1Dam` through `Warhead@7Dam`, changing only `Versus.Heavy` from 20 to exactly 22.
- The explicit `SpreadDamage` values preserve MiniYaml merge behavior while satisfying the YAML checker. Cycle-2 resolved outputs are byte-identical to the cycle-1 passing outputs.
- Cost 600, Light armor, repair step 2,046, speed 92, prerequisites, queues, AI weights/delays, burst/reload/range/projectile, raw damage/spread/delays, non-Heavy modifiers, shared flame weapons, and the FTNK death explosion remain control-identical.
- No C#, AI policy, shared explosion template, product logging, or unrelated mod was changed.

Product commits:

1. `1fc17f351aabfebd3654f3aada03e798b0786b98` — balance FTNK durability and tank damage.
2. `6f3a33ea165e0b4b90d0e4a9c974b70a12f78a12` — declare the seven local warhead types and remove YAML warnings without changing resolved behavior.

## Static and build evidence

- Base resolved evidence: `analysis/worker-4-cnc-43a/base-resolved/`.
- Initial changed resolved evidence: `analysis/worker-4-cnc-43a/changed-resolved/`.
- Final cycle-2 evidence: `analysis/worker-4-cnc-43a/final-resolved-cycle2/`.
- FTNK resolved output differs from control only at HP 30,000 -> 36,000.
- BigFlamer resolved output differs only at seven Heavy modifiers 20 -> 22. Each node remains `SpreadDamage`, raw damage 1,800, spread 750, delays 0/25/50/75/100/125/150, and all sibling modifiers/timing are preserved.
- Final cycle-2 FTNK, BigFlamer, `^FlametankExplode`, Flamethrower, Chemspray, BigChem, and Napalm outputs are byte-identical to the saved cycle-1 changed outputs. Death/sibling outputs are also control-identical.
- Final `git diff --check` passed. The base-to-product diff is restricted to `mods/cnc/rules/vehicles.yaml` and `mods/cnc/weapons/other.yaml`.
- Final `make test` passed: Release build 0 warnings/0 errors, CNC MiniYAML validation, default sequences, and every CNC map.
- The first publication build exposed seven new “does not define a warhead type” warnings on the local nodes. Cycle 2 fixed those warnings by naming `SpreadDamage`; no warning was suppressed or ignored.

## Literal full-engine acceptance

Focused map: `analysis/worker-4-cnc-43a/harness/literal-v1/CNC43A-Flame-Tank-Literal-v1.oramap`

Map SHA-256: `aa997a9cd8a545f955a84a43476d046adc8b8935d1c7315e922234c88f9045f6`

Seed: 43001; SkyNet/Nod versus Brutalis/GDI; Headless MAX; matched changed/control processes.

The first pair correctly exposed a harness defect: AutoTarget reacquired after `Stop`, yielding 42 rather than 14 hits, and the death FTNK attacked before being killed. Those runs were labeled invalid. The analysis-only repair set the actors to HoldFire before the one forced order/death.

The corrected pair and both fresh final-regression pairs passed. Final cycle-2 results at sample tick 281 were:

| Lane | Changed damage | Control damage | Delta |
|---|---:|---:|---:|
| Heavy | 5,544 (14 x 396) | 5,040 (14 x 360) | +504 / +10% |
| None | 25,200 | 25,200 | 0 |
| Wood | 30,240 | 30,240 | 0 |
| TiberiumWood | 30,240 | 30,240 | 0 |
| Light | 17,640 | 17,640 | 0 |
| Tiberium | 5,040 | 5,040 | 0 |
| Adjacent FTNK death collision | 1,991 / 9 events | 1,991 / 9 events | 0 |

Fresh FTNKs reported 36,000/36,000 changed versus 30,000/30,000 control. All primary lanes used the same 14 hit ticks through tick 176. Both final processes reached tick 340, wrote replay/benchmark artifacts, and exited cleanly. Final commenter/policy paths are `analysis/worker-4-cnc-43a/final-literal-v1/{commenter,policy}/`; the narrow literal result was accepted while broader strategy was correctly left to adversarial games.

## True-tank counter integrity

### Medium Tank

The clean isolated comparison used four FTNK (2,400 credits) against three Medium Tanks (2,400) on long open ground for seeds 43010–43012, in changed and control builds. Medium Tanks won all six runs with two survivors every time.

- Changed terminal ticks: 799, 799, 727; survivor aggregate HP: 64,627 / 64,654 / 65,833.
- Control terminal tick: 727 in all three; survivor HP: 66,695 / 66,722 / 66,722.
- Changed FTNK completed bounded extra work and sometimes survived 72 ticks longer, but did not displace the equal-credit counter.

Artifacts and roles: `analysis/worker-4-cnc-43a/{harness/mtnk-isolated-v1,mtnk-isolated}/`.

### Light Tank and Mammoth Tank

Final paired map SHA-256: `155287c0e7b9ccbdc714f5739a751ce77a79eaf32288580af0c3f7646a4a599c`; seed 43040.

- Five changed FTNK (3,000 credits) versus four Light Tanks (3,000): Light Tanks won 4–0 at tick 437 with three survivors / 39,764 aggregate HP. Control ended at tick 412 with four survivors / 57,932 HP. The buff exacted a bounded additional tank and 25 ticks without reversing the counter.
- Seventeen changed FTNK (10,200) versus six separated Mammoths (10,200): all six Mammoths held after killing 14 FTNK and retained 536,480 aggregate HP; three geographically separated full-health FTNK did not engage before the bound. Control retained 540,000 HP with the same unit counts. This is recorded as a strong hold, not a complete wipe.
- An earlier concentrated Mammoth layout was invalidated because death-chain geometry killed clustered Mammoths in both changed and control. The final separated layout removes that artifact.

Artifacts and roles: `analysis/worker-4-cnc-43a/{harness/ltnk-htnk-isolated-v1,true-tanks-v1}/`. The routine reviewer considered the narrow counter evidence useful but correctly declined to infer whole-AI policy from a fixed exchange. No AI-policy change is authorized by this task.

## Defense, range, and aircraft

Six unsupported FTNK (3,600 credits) crossed a long open approach toward a 110,000-HP Weapons Factory screened by two turrets, one MLRS, and one Orca. Seed 43020; matched changed/control.

- Both builds first contacted a turret at tick 198.
- Both lost FTNK at the same ticks 295, 305, 439, and 837; the Orca killed two.
- At tick 1,976 the objective was untouched in both builds: 110,000 HP, zero FTNK hits.
- Both had two FTNK alive. Changed aggregate HP was 34,105 versus 22,105 control, exactly reflecting the retained +12,000 pool.
- Both defenses retained the two turrets and Orca; MLRS was destroyed. Defense, range, and air remained a decisive unsupported hard stop.

Artifacts and roles: `analysis/worker-4-cnc-43a/{harness/defense-v1,defense-v1}/`.

## Ordinary natural match and persistence

An analysis-only copy of ordinary connected Empire Earth4 enabled existing bounded AI production logging and event-driven FTNK creation/first-damage/kill observations; it did not change bot policy, production weights, actors, starts, or combat. Map SHA-256: `30b86077afbdad2bf5a74c873395176c9a28a39d5b597b4d60f022ee29910ab9`. Seed 43032; SkyNet/Nod spawn 1 versus Brutalis/GDI spawn 17; 20,000 cash; Headless MAX; natural conclusion.

- Both builds naturally reached Recon II, produced FTNK, entered combat, saved at tick 18,500, wrote replay/benchmark artifacts, and reached natural game over without fatal/desync/rules/Lua errors.
- Changed observed 203 completed FTNK creations at 36,000 max HP, 186 first-damage events, and 162 kills over the longer trajectory. Control observed 48 completed creations at 30,000 HP, 28 first-damage events, and 22 kills before its earlier conclusion. Production-request counts can exceed completed observations when queued units remain at termination.
- Changed reached at least tick 55,000 and 512.572 seconds; control at least tick 20,000 and 137.198 seconds. The changed combat trajectory diverged strongly, so this single pair is exercise/regression evidence rather than a causal performance or win claim. The staged artifacts do not name the winning side.
- Commenter/policy paths: `analysis/worker-4-cnc-43a/natural-v1/{commenter,policy}/`. Both identified the missing terminal winner/scoreboard as an interpretation limit. That recommendation is retained for future policy studies; it does not invalidate production/contact/natural-conclusion acceptance.

The changed tick-18,500 save was staged with its exact custom map and reloaded through tick 20,000. An FTNK created before the save was damaged by a Mammoth at tick 18,572 and reported 30,852/36,000 afterward; ordinary SkyNet/Brutalis production and combat continued and the bounded process exited cleanly. The passing run is `analysis/worker-4-cnc-43a/harness/natural-v1/changed-load-run-v4/`; roles are `analysis/worker-4-cnc-43a/save-load-v1/{commenter,policy}/`.

## Evidence loop and policy decisions

- 36 full-engine games are counted, including labeled diagnostic/invalid games that advanced far enough to expose an evidence problem. Four tick-0 Lua/map-availability packaging failures are excluded.
- Required fresh Commenter and routine Policy Reviewer outputs exist for every materially judged batch. Direct `codex exec --ephemeral` was used with the mandated role skill, Terra 5.6 medium, workspace sandbox, and strict path-only jobs because the shared helper initially encountered a CLI compatibility issue; no role received source, worker state, or implementation notes.
- Reviews repeatedly distinguished narrow combat facts from whole-AI strategy. Their calls for literal localization, true-tank/defense pressure, natural production, and save/load were adopted and tested.
- Recommendations to retune AI production/commitment, hold HP constant, or change composition are rejected for this task: the authoritative contract explicitly requires both the HP and Heavy-damage changes and forbids AI changes. The natural match plus focused counter/defense games supply the strategic guardrails appropriate to this content-only task.
- Sol-xhigh escalation was not used; no persistent policy problem warranted it.

## Performance, determinism, and diagnostics

- The product change is static YAML and adds no scans, allocations, ordering, or hot-path work.
- The final focused changed/control pair completed in 6.006/6.007 seconds with 56.566/56.559 valid ticks/s, effectively identical throughput.
- Medium Tank results repeated across three seeds; final literal cadence and totals repeated across fresh processes before and after cycle 2.
- Natural wall times are intentionally not compared as a performance metric because combat outcomes and match duration diverged.
- No product diagnostics remain. All custom maps, logs, replays, saves, manifests, benchmarks, direct-role transcripts, and test runner wrappers remain ignored under the assigned analysis directory. The temporary repository-root save-load wrapper was removed.

## Dependency and scope check

CNC-43 PR #78 (`agent/round-20260806-cnc43-mcv-crush-flavor`, product commit `4f36851179`) now changes MCV locomotor ownership in `mods/cnc/rules/vehicles.yaml` plus structure/world locomotor rules. Its `vehicles.yaml` edit is at MCV and is disjoint from this task's FTNK block; it does not touch `mods/cnc/weapons/other.yaml`. No semantic overlap or dependency was found.

## Deferred work and known limitations

- Future natural-match policy studies should record named winner/defeat tick, terminal economy/force/scoreboard, and replay-derived target/route evidence. This task does not add broad telemetry.
- Three separated FTNK remained unengaged at the final Mammoth bound; the conclusion is deliberately “Mammoths held decisively,” not “Mammoths wiped every FTNK.”
- Natural production and combat are adaptive and diverge after the requested balance change. One natural pair cannot attribute the longer match to a single mechanism.
- The existing fixed repair step means a full 36,000-HP FTNK takes more repair steps than at 30,000 HP. Scaling repair was explicitly forbidden and remains unchanged.
- No known product failure remains. Publication and required GitHub checks are the only pending gates.

## Publication

Individual PR: [#79](https://github.com/Realpra1/LibertyDawn/pull/79), against `agent/cnc38-early-viki-infantry-rush`. At tested head `bcff2af985a3fbc205a0d8b4e03c9cc4ee0dc03d`, GitHub reported `MERGEABLE/CLEAN`. The assigned base branch is unprotected and GitHub reported no required checks, no check rollup, and no workflow runs after polling; final local `make test` is green. This receipt update is documentation-only. Do not merge this task PR directly.
