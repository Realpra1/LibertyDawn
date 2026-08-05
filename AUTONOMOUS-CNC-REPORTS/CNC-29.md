# CNC-29: Stealth Chemical Harassment Squad

- Status: complete
- Cycles used: 13 of 30
- Branch: `agent/cnc29-stealth-chem-squad`
- Base: `origin/agent/cnc28-stealth-chem`
- Draft PR: https://github.com/Realpra1/LibertyDawn/pull/56

## Literal acceptance

When an eligible AI owns at least two chemical tanks, one dedicated chemical harassment group reserves approximately half while leaving the remainder available to ordinary ground squads. It prioritizes harvesters, clustered infantry, and Tiberium-armored buildings, never deliberately targets tanks, attacks rather than crushes infantry, retains detector and non-infantry-defense route safety, and releases transported, captured, dead, or otherwise ineligible actors without contention.

Forbidden outcomes include duplicating the stealth-squad implementation, changing chemical cloak or balance, regressing stealth-tank allocation, reserving all chemical tanks, creating multiple chemical specialist groups, deliberately selecting tanks or generic structures, ignoring detectors, crushing infantry, competing with transports/ordinary squads, unbounded scans, or new nondeterministic state.

## Implementation

- Generalized the existing stealth-tank specialist manager through configuration instead of adding a second implementation.
- Preserved the stealth-tank defaults: two harassment groups, one cooperative attack group, the original reservation curve, priorities, and crush behavior.
- Added configurable group labels/counts, attack-group inclusion, reservation policy, target-type exclusions, armor priorities, bounded infantry-cluster scoring, weapon-threat exemptions, and attack-versus-crush behavior.
- Configured chemical tanks for one harassment-only group and a half-preserving reservation curve: 2/1, 3/2, 4/2, and 10/5 specialist/ordinary splits.
- Hard-excluded actors with the `Tank` target type, set no generic structure priority, and prioritized harvesters plus `Tiberium`/`TiberiumWood` armor.
- Added a bounded 3-cell infantry-cluster multiplier and issued ordinary attack orders so chemical splash is used without stealth-tank crush behavior.
- Ignored ordinary infantry and creep weapon ranges for intended chemical engagements while retaining detector geometry and every non-infantry defense check.
- Added `vice` and `pvice` as immediate self-defense targets after adversarial testing showed killed infantry can become hostile visceroids that otherwise retaliate against the squad.

## Cycles

1. Audited both specialist-manager instances, ordinary squad and transport reservations, targeting, armor, threat geometry, and lifecycle contention; implemented the configurable extension and passed strict build, tests, and full YAML validation.
2. Focused live test proved an 8-tank 4/4 specialist/ordinary split and selection of a harvester, a Tiberium-armored structure, and clustered infantry while leaving a mammoth tank and generic Wood structure untouched.
3. A low-level Lua passenger fixture failed because direct cargo registration left the passenger in the world and unloading attempted to add the same actor twice; retained as a harness failure, not product evidence.
4. Replaced the invalid harness with real `EnterTransport` orders; lifecycle ran, but tightly packed chemical death blasts contaminated exact counts.
5. Found and corrected a fixture race that matched a stale log marker and terminated the new process before its scenario ran.
6. Repeated with a unique marker; packed death-blast contamination remained, so the fixture was redesigned without product changes.
7. A clean spaced lifecycle fixture proved exact rebalancing through capture, death, boarding, unloading, and two reinforcements, while a harmless live detector prevented specialist departures.
8. Adversarial clustered-infantry combat exposed that killed infantry spawned hostile `vice` actors; the squad ignored them and lost five tanks.
9. Added bounded `vice`/`pvice` self-defense priorities and creep-weapon tolerance, then passed the focused policy tests and full YAML validator.
10. First clean post-fix adversarial pass: the squad chose the six-infantry cluster, immediately cleared spawned visceroids, killed a safe Tiberium-armored structure and lone infantry, and rejected guarded economy targets, the detector, a tank, and a generic structure.
11. Second clean post-fix pass repeated every exact lifecycle reservation transition and retained detector safety after the creep-response fix.
12. Third clean post-fix pass was a full normal-AI VIKI-versus-Brutalis headless MAX match to natural game over. Chemical squads attacked infantry, visceroids, harvesters, and Tiberium structures, rejected late obelisk coverage, rebalanced down to zero cleanly, and VIKI won decisively with no new fatal, Lua, or unhandled error.
13. A dedicated stealth-tank regression retained the original 12-tank `2/2/2` specialist-group allocation with six ordinary tanks; harassment groups attacked economy targets and the attack group selected medium/light tanks.

Ignored raw logs, fixture packages, and the full-match replay are under `AUTONOMOUS-CNC-LOGS/CNC-29/`.

## Validation

- Strict Debug solution build: passed with zero warnings and errors.
- Unit tests: 322/322 passed, including eight added allocation, exclusion, and cluster-policy tests.
- Full CNC YAML/map validator: passed.
- Lifecycle: exact reserve/release/reinforce transitions passed for capture, death, real transport boarding/unloading, and new production.
- Threat behavior: detector and non-infantry defenses remained active; intended infantry and spawned-creep engagements were not rejected by their own short-range weapons.
- Target behavior: clustered infantry, harvesters, Tiberium-armored structures, and spawned visceroids selected; tanks and generic structures not deliberately selected.
- Existing stealth-tank live allocation and role targeting passed unchanged.
- Three consecutive clean post-fix adversarial passes completed, including one full natural match.
- The recurring map-cache warning naming the user's pre-existing invalid `TibTest.oramap` is unrelated to the CNC-29 fixtures.
- GitHub implementation/report head `c3eed0f7f9`: Linux passed in 3m15s and Windows passed in 4m35s; PR #56 is mergeable.

## Deferred boundary

CNC-29 does not alter chemical cloak/balance, air squads, general threat caching, or ordinary ground-army targeting. Crate exploration remains CNC-30, and generalized unit-versus-unit threat calculation remains its separately queued task.
