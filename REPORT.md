# CNC-96A Air-verbatim integration cycle 3 report

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

No push or pull request was created.
