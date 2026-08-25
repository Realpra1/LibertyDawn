# CNC-96A cached kiting and clearing successor

This cycle replaces broad live ground-threat scans with an Air-derived bounded
planning flow. Stealth targets are grouped into cached 6x6 strategic cells and
shortlisted using the existing Air candidate selector. Scores use the requested
actor priorities, a capped one-to-two-times price bonus, and inverse remaining
health. A configurable 30-second ETA uses the selected ground route and current
squad movement speed as a preference: the planner first chooses nearby safe
economic targets, then a viable clearing opportunity, then a reachable safe
economic target beyond the preference window so distance cannot leave squads idle.

Each defended target's package is exactly the enemy actors in its target-centered
3x3 cache neighborhood. Kiting is selected before mass clearing when every squad
member is at least 20 percent faster than the live target and has a legal firing
band beyond that target's current range. The whole squad focus-fires, excludes
positions covered by any other cached local weapon threat, and moves outward when
too close. After armed vehicles are gone and the squad is cloaked, configured STNK
squads may crush non-detecting infantry. CTNK profiles explicitly disable both
kiting and crushing.

When neither kiting nor crushing is available, the manager-owned shared
`GeneralizedCombatThreatCalculator` evaluates only current squad members against
the cached 3x3 defender package. Mass mode enters strictly above a 2.0 overmatch,
latches the package, and attacks the calculator's highest-threat victim first.
It recalculates only on squad/package membership changes or victim loss, continues
above 1.0, and aborts/flees at or below 1.0. Once Kite, Crush, or Mass starts, the
cached package is authoritative until it is cleared or explicitly aborted, so a
moving Harvester cannot preempt the defense-clear mission. The focused policy tests
exercise all four strict boundaries. Engine fixtures prove strict entry, ordered
highest-threat focus, complete package removal, and a single below-one abort after
a fresh overwhelming package changes membership.

Pending Blue explosions are checked over each member's current strategic cell,
bounded to at most 36 exact cells per squad tick. One latched whole-squad move exits
to a neighboring strategic cell containing no Blue, Red, or pending explosion,
then immediately replans on arrival. Normal Blue Tiberium remains a finite route
cost, Green is legal, and Red/pending cells are forbidden. The old broad pending
annulus and local actor-circle scans are not used. All diagnostic logging remains
behind the existing default-false target-debug flag. The two evidence traits are
absent from normal CNC rules and exist only for generated test maps.

Exact-source validation:

- `make check`: PASS, zero warnings/errors.
- `dotnet build OpenRA.sln --no-restore`: PASS, zero warnings/errors.
- Focused `StealthAIFunctionMatrixTest`: PASS, 9/9 (four unrelated pre-existing
  test-project analyzer warnings).
- `./utility.sh cnc --check-yaml`: PASS when run alone. An earlier concurrent run
  ended in a native bus error during map validation and was discarded.
- `git diff --check`: PASS.
- Four ordinary-AI engine scenarios: PASS 4/4 under
  `.build/cnc96a-kiting/results15`.

The MTNK/kiting fixture reached tick 3200 with six tracked STNKs, 64 specialist
damage events, three Harvester kills, one infantry kill, zero 750-tick stalls, and
zero STNK deaths. It exercised `Kite` and `Crush`; exact `INotifyCrushed` telemetry
records `stnk#146` crushing `e1#138` at tick 656. At tick 3001 the fixture MTNK
remained at 43,680/45,000 HP and the MSAM at 15,305/18,000 HP while nearer safe
economic targets remained available. This is accepted as advisory evidence under
the required precedence (short safe target before Kite), not misreported as a
natural defense-package wipe; the isolated fixtures below prove clearing behavior.

The isolated mass fixture disabled kiting/crushing only in its generated map and
entered `Mass` at tick 11 with crossover 10.296. Its first ordered victim was the
highest-threat MTNK. The exact cached Harvester, Rifle, MTNK, and MSAM package was
fully removed by tick 1579, with zero stalls or STNK deaths. A separate abort
fixture first removed the same original package, then introduced twelve HTNKs into
the same cached 3x3 neighborhood. Package membership changed, crossover recalculated
to 0.407, and the squad issued exactly one flee at tick 381, arrived at tick 556,
and did not reissue, stall, or lose a unit. The release profile retains the normal
kiting-first pipeline.

The Blue fixture selected an actual non-lead squad member, filled that member's
exact 6x6 cell, and called the real `IResourceLayer.DamageResource`. Pending state
appeared at tick 101. The whole squad issued one latched exit from coarse `15,10`
to neighboring `15,11`; telemetry explicitly recorded destination `Blue=false`,
`Red=false`, and `pending=false`. It arrived at tick 204 and replanned, with zero
reissues, stalls, or deaths through the tick-900 horizon.

Raw maps, logs, and build artifacts remain ignored under `.build`. This worker did
not push, publish, merge, edit the task sheet/coordinator state, or mutate `bleed`.

# CNC-96A successor: mandatory first Covert III STNK

Current `origin/bleed` at `4a1461f6c1497a70e48c8bf85078e0e7e0600deb`
already contains PR132. The reviewed repair/Obelisk amendment was carried
forward unchanged from `5a3e36886819d5a7148cec9c6dec0a6bc3592fa6` as
`e850013c62` before this production amendment.

The existing `BaseBuilderBotModule` opening planner now owns an optional,
separate one-shot technology-unit milestone. It is deliberately not part of
`OpeningComplete`, so late technology cannot retain `OpeningActive` or disturb
the existing structure, five-Harvester, and one-MCV sequence. VIKI and Iron
Reaper alone configure one `stnk` after `upgrade.covert3` has been observed.
The observation is latched because Iron Reaper may later change branches.

The milestone waits without issuing a request until STNK is actually buildable
and a compatible queue is idle. Existing requested, queued, or pending STNKs
deduplicate it. One accepted request uses the existing external-unit lifecycle,
timeout, and retry behavior; lifetime production satisfies the milestone, so a
later loss cannot request a replacement. Baseline, unlock, completion, retry,
outstanding-request, and expiry state are game-save persisted. Normal weighted
STNK production remains enabled after the mandatory obligation completes.

Exact-source validation:

- `make check`: PASS, zero warnings/errors.
- Focused opening-policy and Stealth matrix tests: PASS, 22/22 (the test project emits four unrelated
  pre-existing analyzer warnings).
- CNC YAML validation: PASS.
- `git diff --check`: PASS.
- Mandatory production full-engine game: PASS at tick 3200 under
  `.build/cnc96a-mandatory-stank/results4/`. At tick 301 VIKI had zero STNKs;
  after Covert III the planner logged exactly one request and produced exactly
  one STNK at tick 1481. The milestone completed 1/1 and ordinary production
  subsequently produced 14 non-STNK units by tick 3001.
- Prior-amendment single-Obelisk full-engine game: PASS at tick 3500 under
  `.build/cnc96a-mandatory-stank/obelisk-results3/`. The unchanged 750-tick
  watchdog tracked firing STNKs and reported zero stalls and zero
  Obelisk-attributed deaths at tick 3001.

Early harness runs reached their tick bounds but were rejected because the
default generated settings omitted `Debug.LuaDebug`, suppressing Lua evidence.
Accepted reruns use the prior known-good settings template. The old manifest's
tick-3000 expression was corrected harness-only to accept the engine's actual
`Trigger.AfterDelay` marker at tick 3001. No product behavior was changed for
either correction. Raw maps, logs, replays, and build output remain ignored.
No push, publication, merge, task-sheet edit, coordinator-state edit, or
`bleed` mutation was performed.

# CNC-96A PR132 Obelisk/repair follow-up

Stealth already used Air's repair lifecycle, including passive allied repair
auras and timestamped oldest-ready waiting. The parity defect was asymmetric
ownership: Stealth considered both Air and Stealth claims, while Air considered
only Air claims. Both sides now aggregate the same Air+Stealth repair squads,
so a facility has one mutual claim/FIFO policy.

The copied target planner treated ordinary ground weapons as finite transit
cost, which is correct while cloaked, but did not distinguish the firing point
where an attack reveals the unit. The focused correction reuses cached threat
facts to exclude a target position when an enabled enemy weapon covering that
position can destroy a squad member in one volley. Ordinary non-detector transit
remains soft. If every firing position is unsafe, Idle or Attack invalidates the
plan, issues one neighboring 6x6 safety move, preserves it until arrival, and
immediately rescans. This is not completion retreat behavior.

Two distinct ordinary VIKI-versus-Brutalis full-engine scenarios used single
and corridor Obelisk/detector geometries. Their ignored Lua watchdog fails an
owned STNK/CTNK after 750 stationary ticks unless that exact actor fired or
repaired, and separately attributes Obelisk kills. On exact final source both
games reached tick 3500 with zero stalls and zero attributed Obelisk kills.
They retained combat activity with 62 and 86 specialist firing events and
completed 26 and 18 latched safety replans. Artifacts are under
`.build/cnc96a-obelisk-followup/results-exact3/`.

`make check` passed with zero warnings/errors, CNC YAML validation passed, the
focused Stealth matrix passed 7/7, and `git diff --check` passed. No build-order
source or YAML was changed. Existing VIKI and Iron Reaper STNK entries are
probabilistic; repository inspection found no supported existing config/request
surface that guarantees one earliest-tech STNK for both profiles without
changing or abusing production machinery, which the user explicitly prohibited.

# CNC-96A PR132 finite-safety policy evidence follow-up

This second follow-up changes no safety or routing decision. When the existing
default-false `AirTargetDebugLogging` flag is enabled, the nearest-safe helper
now records every accepted neighboring 6x6 candidate's strategic cell,
destination, squared travel distance, danger cost, and threat clearance. It
also records the selected candidate's rank under the existing
danger-ascending/clearance-descending ordering. Candidate collection, sorting,
and logging are skipped when the flag is off.

Game A applies one finite detector/ground-weapon pulse and removes both hazard
actors at tick 300. Its exact-final log records six safe neighboring candidates
at tick 208. All have danger 0.2; destination `27,39` has the greatest threat
clearance (113), is selected as rank zero/minimum under the unchanged risk
ordering, and receives exactly one order batch. The same escape arrives at tick
308 after three preserved 25-tick checks, changes immediately to Idle/replan,
and no second issue or unfinished escape exists at the tick-800 horizon.

Game B uses the equivalent map, actors, hazard pulse, seed, bots, and horizon,
but its map rules omit `AirTargetDebugLogging`. Ordinary gameplay reaches the
configured tick-800 exit. The manifest forbids evidence markers, and the
separate external verifier independently observes zero candidate, safety-state,
and target-evidence telemetry in the release-default debug log.

Both exact-final games passed under
`.build/cnc96a-safety-policy2/results-exact-final/`. The external verifier
`.build/cnc96a-safety-policy2/verify_evidence.py` reparses the debug candidate
set, recomputes the minimum from raw danger/clearance values, requires exactly
one issue and arrival before the horizon, and checks the release log. It reports
`PASS candidates=6 selected=27,39 travel=61 issue=208 arrival=308
release-telemetry=0`.

`make check` passed with 0 warnings/errors, CNC YAML validation passed, the
focused `StealthAIFunctionMatrixTest` passed 7/7, and `git diff --check` passed.
The standalone test-project build retained four unrelated pre-existing analyzer
warnings. Raw maps, logs, replays, summaries, and verifier inputs remain ignored
under `.build`. This worker did not push, publish, merge, edit the task sheet,
or edit coordinator state.

# CNC-96A PR132 policy evidence follow-up

This follow-up changes no target, route, safety, balance, or production policy.
It adds narrowly debug-gated provenance to the existing default-false
`AirTargetDebugLogging` path. Release-default execution does not build the
ranked evidence list or maintain escape counters.

The adversarial target scenario produced both required discriminators. At tick
119, the incumbent `proc#59` and newcomer `proc#50` were both full health and
both scored 500000; the existing 25% switch threshold retained `proc#59`. At
tick 309, a newly visible `proc#61` at 5000/100000 HP scored 25714280, versus
the incumbent's 750000, and the squad switched to the genuinely better damaged
target.

The local-safety scenario records the chosen strategic coordinates as well as
their delta. At tick 258 the squad moved from coarse cell `5,5` to neighboring
cell `4,6` (`delta=-1,1`) with `order-batches=1`. It preserved that escape
through five subsequent 25-tick safety checks without reissuing it. Arrival at
tick 408 records the same destination and immediately changes to Idle, which is
the normal target-replanning state. Additional squads independently produced
the same one-neighbor/preserved-arrival sequence.

Exact-final real-engine artifacts are under
`.build/cnc96a-policy-followup/results-exact-final/`: `target-retention-switch` and
`neighbor-safety` both passed at tick 1600. `make check` passed with 0 warnings
and errors, CNC YAML validation passed, `StealthAIFunctionMatrixTest` passed
7/7, and `git diff --check` passed. The standalone test-project build retained
four unrelated pre-existing analyzer warnings.

Raw scenario maps, logs, summaries, and replays remain ignored under `.build`.
This worker did not push, publish, merge, edit the task sheet, or edit
coordinator state.

# CNC-96A PR132 corrective cycle report

PR132 had regressed the prior live `StrategicCellSize: 6` configuration while
Air remained 6x6. The copied Stealth target branch consequently fell back to
the old four-cell waypoint spacing, rebuilt all world threat/resource facts,
and ran A* once for each of up to 48 actors.

This cycle restores one explicit and default 6x6 Stealth grid for every STNK
and CTNK profile. It adapts Air's manager/profile caching and bounded
strategic-cell shortlist: current-health utility is refreshed, incumbents are
injected and retained for reassessment, harvester slots are preserved, and A*
runs once per selected cell. The cached ground grid treats detector coverage,
terrain and red tiberium as hard danger, while ordinary ground weapons and blue
tiberium remain finite route costs.

Stealth squads now join Air's independent safety cadence. Every 25 ticks a
single bounded live scan checks detectors and ground weapons. A genuinely
unsafe or red-tiberium position produces one move to the nearest safe
neighboring 6x6 cell. That active move is preserved until arrival, after which
the squad immediately returns to Idle and replans. There is no completion
retreat. Repair routing now interprets ground/detector threats and the shared
Stealth terrain/resource grid, uses ground Mobile speed, and coordinates repair
facility claims across both Air and Stealth squads.

Validation on the exact final source:

- `make check`: PASS, 0 warnings/errors.
- `./utility.sh cnc --check-yaml`: PASS.
- `StealthAIFunctionMatrixTest`: 7/7 PASS.
- `git diff --check`: PASS.
- Two distinct full-engine games: 2/2 PASS at world tick 3000 under
  `.build/cnc96a-air-cache-safety/results-final/`.
- Game 1 observed a live Stealth safety escape exactly one 6x6 strategic cell
  plus subsequent route activity.
- Game 2 observed explicit coarse=6 caches for both `stealth-tank` and
  `chemical`, and live arriving reinforcements joining both formations.
- Benchmark evidence records independent Stealth phases. STNK cache
  hits/rebuilds were 812/268 in Game 1 and 857/261 in Game 2.

Raw maps, logs, replays and benchmarks remain ignored under `.build`. This
worker did not push, publish, merge, edit the task sheet, or edit coordinator
state. Fresh narration/policy review and final Terra review remain coordinator
gates.

# Prior CNC-96A Air-verbatim integration cycle 4 report

## Cycle 4 CI correction

Linux CI reported IDE0005 at
`OpenRA.Mods.Common/Traits/BotModules/BotModuleLogic/StealthSquadDefinition.cs:4`.
The correction removes only the unused `using System;` directive. There is no
behavior, configuration, balance, architecture, or refactor change.

The corrected final source passes the exact Linux `make check` gate with 0
warnings and 0 errors. It also passes `dotnet build OpenRA.sln --no-restore`
with 0 warnings and 0 errors, the 6/6 focused provenance tests, the Air-copy
checker, CNC YAML validation, and `git diff --check`.

The unchanged cycle-3 HP/detector and reinforcement scenarios were rerun against
the corrected final source with the same maps, seeds, lobby commands, tick-3000
bound, and required/forbidden patterns. A sequential accepted batch passed 2/2
under `/tmp/cnc96a-cycle4-games-final-rerun`. An earlier concurrent attempt had
Game 1 reach natural game-over after satisfying every task-specific pattern but
before tick 3000; it was rejected under the manifest contract and the same pair
was rerun sequentially without source or scenario changes.

Fresh separate Luna narrators and serialized fresh Luna policy reviewers passed
each accepted game:

- `/root/github/LibertyDawn/analysis/20260824-cnc96a-air-verbatim/cycle4/game1/{NARRATIVE.md,POLICY-REVIEW.md}`
- `/root/github/LibertyDawn/analysis/20260824-cnc96a-air-verbatim/cycle4/game2/{NARRATIVE.md,POLICY-REVIEW.md}`

Game 1's recommendation to retain the HP/detector regression is satisfied by the
unchanged scenario. Game 2's suggestion for future no-valid-target/route coverage
is recorded as advisory; scenario expansion is rejected for this expressly
nonbehavioral, one-line CI correction.

## Result

CNC specialist squads now live inside the original ownership graph. The existing
`SquadManagerBotModule` creates, recruits, ticks, saves, loads, releases, and
failsafe-manages ordinary `Squad` instances typed `Stealth`. There is no live
parallel manager, specialist controller, or sibling Squad class.

Named `stealth-tank` and `chemical` definitions configure STNK and CTNK behavior
inside the same manager. Recruitment claims eligible specialists before ordinary
ground adoption and enforces the deployed aggregate maximum of four squads.
Membership continuity, busy ownership, reinforcement state, target reassessment,
repair, and save/load reuse the manager-owned Air lifecycle fields on `Squad`.

The copied Air Idle/Attack/Flee state bodies remain the live decision loop. Marked
ground extensions add ground passability, detector and weapon influence,
resource hazards, A* routing, crushing, profile priorities, and HP-aware target
scoring. Stealth entry into Air retreat order paths is blocked; all 35 archived
retreat/completion-retreat bodies remain absent. Original Air sources are unchanged.

The redundant copied `StealthAIModule.cs` and `StealthAISquad.cs` were moved out
of compilation to `.agents/inspiration/stealth-ai-pre-air-copy/air-derived-nonowning-reference/`
with an ownership-correction note.

## Matrix and provenance

- Authoritative matrix preserved at `.agents/inspiration/stealth-ai-pre-air-copy/FINAL-MATRIX.json`.
- Independent final live map: `.agents/inspiration/stealth-ai-pre-air-copy/live-provenance/LIVE-MAP.json`.
- Independent audit: 98/98 mappings: 47 exact bodies, five retreat-free extractions,
  and 46 responsibilities composed into copied-Air bodies; zero missing and zero
  conflicting old bodies.
- The same audit confirms 51 authoritative Air bodies and 35 excluded retreat bodies.
- `scripts/check-stealth-ai-air-copy.py` passes the 3,040-line state copy and
  549-line threat-geometry copy after reversing identities/removing marked ground
  extensions, and proves both removed owner files equal their immutable base forms.

## Verification

- `dotnet build OpenRA.sln --no-restore`: passed, 0 warnings / 0 errors.
- `dotnet test OpenRA.Test/OpenRA.Test.csproj --no-restore --filter FullyQualifiedName~StealthAIFunctionMatrixTest`:
  passed 6/6.
- `./utility.sh cnc --check-yaml`: passed all CNC rules, sequences, and shipped maps.
- `git diff --check`: passed.
- Original `AirStates.cs` and `AirThreatGeometry.cs`: no diff from the exact base.

Focused tests prove sole manager ownership, direct copied-Air state Tick bodies,
the exact 184-function disposition counts, all 98 independent live mappings,
five extraction constraints, two profiles/max-four wiring, pre-ordinary recruitment,
and default-false diagnostic provenance.

## Final full-engine games

Generated maps, logs, benchmarks, and replays remain outside Git. Both final-source
games used the canonical content link, isolated support directories,
`Logs/debug.log`, MAX headless mode, a 120-second timeout, and a tick-3000 bound.

- Game 1: `/tmp/cnc96a-cycle3-games-final2/cycle3-game1-hp-detector/summary.json` —
  passed at tick 3000. Debug evidence directly records low-HP candidate scoring,
  a detector-exposed straight path, the selected hard-safe ground detour and
  waypoints, route/attack execution, busy stall reassessment, and no retreat marker.
- Game 2: `/tmp/cnc96a-cycle3-games-final2/cycle3-game2-reinforcement/summary.json` —
  passed at tick 3000. Actual `ctnk#35` and `stnk#33` reinforcements arrived and
  joined their formations; both named profiles continued under shared manager
  ownership through busy attacks and stall rescans with no retreat marker.

Fresh separate Luna narrators and serialized fresh Luna policy reviewers passed
each final game with no policy action:

- `/root/github/LibertyDawn/analysis/20260824-cnc96a-air-verbatim/cycle3/game1/{NARRATIVE.md,POLICY-REVIEW.md}`
- `/root/github/LibertyDawn/analysis/20260824-cnc96a-air-verbatim/cycle3/game2/{NARRATIVE.md,POLICY-REVIEW.md}`

An earlier diagnostic pair ended naturally before its overlong tick-5000 harness
bound; a narrow manifest-only correction to tick 3000 and regex escaping produced
the accepted pair. The first Game 1 review then identified detector routing as an
evidence-only gap. Debug-only route provenance, gated behind the existing
default-false `AirTargetDebugLogging`, resolved that gap without changing algorithms
or balance; a fresh Terra provenance audit and both final games/reviews were rerun.

Draft PR 132 was already published at the cycle-3 head. Cycle 4 did not push or
update it pending a fresh Terra rereview of the corrected commit.
