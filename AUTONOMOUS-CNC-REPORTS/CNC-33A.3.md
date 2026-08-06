# CNC-33A.3: No-mutants lobby option

- Status: first iteration
- Cycles used: 30 of 30
- Branch: `agent/cnc33a3-no-mutants`
- Base: green CNC-33A.1.1 draft PR #68 head `398e11e8d5`
- Pull request: draft PR #69 targeting `agent/cnc33a1-1-hard-ai-openings`; GitHub checks pending

## Literal acceptance

With `No mutants ever` disabled, ordinary mutation and viceroid creation remain unchanged. With it enabled, attempts after match initialization to create a mutant produce no live actor regardless of whether the source is Tiberium terrain damage, carried cargo, a weapon or delayed warhead, a death trait, a crate, AI/player action, or Lua/script creation. Mutants authored into the starting map remain alive and may change owner, enter cargo, and survive save/load; the option prevents new mutants rather than deleting existing ones.

Red-Tiberium detonations follow this precedence: `No red Tiberium explosions` suppresses them completely; otherwise `No mutants ever` replaces their mutating payload with the configured existing blue-Tiberium explosion. The replacement is itself suppressed by `No blue Tiberium explosions`. With neither restriction, current red behavior is preserved.

Forbidden outcomes are deleting map-authored or loaded mutants, leaving a newly spawned mutant briefly targetable, bypassing the gate through scripts/crates/capture/delayed effects, suppressing ordinary actors or nonmutating explosions, weakening default red explosions, allowing a replacement through the blue-explosion gate, changing mutation/resource balance when disabled, desynchronizing replay/save state, or emitting release debug spam.

## Contention and source inventory

- Runtime actor creation and re-addition: death-spawn traits, crate rewards, actor spawn managers, production/free actors, Lua `Actor.Create`, reinforcement scripts, map actors, save restoration, cargo unload, transform/replacement, ownership remove/re-add, and disposal callbacks.
- Mutation sources: infantry deaths from `TiberiumDeath` and `TibGun`; red field `AtomicTib`/`TiberiumMeteor`; ordinary and unstable HARV/SHARV cargo death; chemical/Tiberium weapons; delayed warheads; AI red bomb-truck and player/script detonation.
- Explosion-option precedence: red suppression before nonmutating replacement, then blue suppression of the replacement; field scheduling/blinking, projectile impacts, immediate/delayed warheads, actor `Explodes`, and mixed cargo.
- Normal AI squads, harvesting, bomb-truck reservations/orders, combat targeting, creeps, map scripts, replay, and save/load remain active during integrated tests.

## Initial design

Mark mutant actor families semantically in CNC rules and add one world-level creation policy that rejects only their first runtime insertion. The marker records whether an actor came from the map and whether it has ever entered the world, which preserves starting, captured, transported, and loaded mutants while covering every runtime creation API through one boundary. Save restoration replays the original simulation, so it must apply the same runtime gate rather than treating replayed creation as an existing actor. Add configurable nonmutating replacement weapons at the existing semantic red-impact boundaries, using blue-tagged CNC weapons so the independent blue option still applies. Keep the policy fixed from synchronized lobby settings, add pure precedence/lifecycle tests plus full-engine observables, and retain quiet-by-default logging.

## Implementation

- Added the default-off `No mutants ever` lobby setting to the existing synchronized Tiberium option trait.
- Added a semantic `Mutant` marker to the viceroid family and a common actor-insertion suppressor. It disposes only a mutant's first runtime insertion; map-authored mutants and actors that already entered the world survive capture, cargo removal/re-addition, and save replay.
- Added configurable nonmutating weapon replacement at weapon and actor-explosion boundaries. Red field weapons use the existing blue `ChemtankExplodeOnce`; unstable harvesters use a blue-tagged alias of their ordinary carried-Tiberium explosion. Original red suppression runs before replacement, and the replacement re-enters the normal blue suppression gate.
- Default behavior and ordinary actors/weapons are unchanged. Release debug logging remains disabled; the ignored evidence map explicitly enabled it.

## Evidence cycles

- Cycles 1-4 covered the initial focused-test correction, 15 focused lifecycle/option tests, strict zero-warning Debug build and interface checks, Lua checks, and exhaustive CNC rules, sequences, and shipped-map validation.
- Cycles 5-8 were invalid launcher setup: `--content` pointed at the `cnc/` child instead of its parent, opening the content manager at tick zero. Interrupting the invalid three-wide batch exposed an out-of-scope orphan-cleanup regression now recorded in `DEFERRED_WORK.md`.
- Cycles 9-16 rejected an eight-run fixture whose custom infantry omitted a render-image override and failed at tick 25. No product conclusion was taken.
- Cycles 17-24 ran the full eight combinations of no-mutants, no-red, and no-blue with ordinary SkyNet and Brutalis. All four no-mutants combinations passed: zero runtime viceroids, one preserved map mutant through capture, red replacement damage from 1,000,000 to 960,000, and complete suppression at 1,000,000 when either red or replacement-blue was disabled. Default controls preserved red/no-red/no-blue damage semantics, but four harness expectations were rejected because a same-owner victim did not receive the intended hostile mutation blast.
- Cycles 25-26 corrected only that fixture ownership and passed a matched hostile differential. Default behavior created both the Lua-scripted viceroid and the guaranteed `TibGun` death mutation and dealt the 100,000 red payload; no-mutants created neither and dealt the 40,000 blue replacement. Both retained and captured the starting viceroid.
- Cycle 27 created a save too near the bounded exit; the harness found the file but cycle 28 correctly rejected its missing footer as an invalid save.
- Cycle 29 allowed network save finalization through tick 1,000. The save contained the required EOF/metadata footer and retained the zero-runtime-mutant, captured-starting-mutant, and blue-fallback observables.
- Cycle 30 loaded that exact save after staging its ignored map UID. Save reconstruction re-suppressed the scripted mutation at tick 26, the observable remained zero runtime mutants with the starting mutant alive under its new owner, and the match then ran naturally beyond tick 15,103. Normal combat later attempted and suppressed several viceroids plus a player-owned `pvice` crate reward. SkyNet and Brutalis, headless MAX, benchmark streams, and two replays were present; no fatal, desync, or unhandled error occurred. Evidence: `.build/cnc33a3/evidence/`.

Final local gates pass: strict Debug build with zero warnings/errors, both interface checks, all 390 unit tests, Lua syntax, `git diff --check`, and exhaustive CNC YAML/sequences/maps.

## Result and remaining risks

The common creation boundary covers death traits, direct scripts, crates, production/spawners, capture/re-addition, save reconstruction, and the natural battle without source-specific exceptions. The red/no-mutants/no-red/no-blue precedence and starting-mutant lifecycle have direct full-engine evidence.

The 30-cycle limit was reached without separately forcing a human-issued unstable-harvester detonation or an isolated delayed-warhead mutation observable. These enter the tested source-independent actor and weapon boundaries, and the natural match exercised delayed blue impacts, but the literal source cases are not claimed as direct evidence. The result is therefore published conservatively as `first iteration` rather than `complete`.
