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
