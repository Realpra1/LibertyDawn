# Ten-task cumulative playtest integration state

## Status

- Candidate: first iteration - testing
- Integration cycle 1: complete
- Valid counted full-engine games: exactly 2
- Release blocker: none observed
- Balance changes: none
- Next action: a bounded cycle 2 is required before release-ready status because the
  counted pressure game did not produce a literal field-defense assignment and its
  ownership audit was aggregate rather than an exact per-actor assertion.

## Integrated ancestry

- Reviewed cumulative base: `07784f11899395bec8c909b588898f2048fe1d81`
- CNC-100: `e1653f9e905742f7b9b659544f3a706e21a2e236`
- CNC-96A: `25fb1fb59cdfe018279e4d557f7392a00da23c32`
- CNC-96B: `7cc0d8da915586f399e669825979d33ec67dca2f`
- Merge commits: `56a7f89554`, `4cef124e8f`, `bdc0e6a3b5`

All four exact inputs were verified as ancestors after the merges. Conflict
resolution retained CNC-100 streaming benchmark/load-shedding/registry behavior
and CNC-96 repair, specialist, transport-objective, and launcher contracts.

## Cycle 1 defects and disposition

1. The Stealth repair lifecycle cached an inactive pre-condition
   `SquadManagerBotModule`, so an active authored repair threshold could be missed.
   The lifecycle now refreshes the active manager before repair evaluation. This is
   a combined integration defect fix, not a policy or balance change.
2. The streaming benchmark signature broke the existing benchmark unit-test API.
   Its `World` argument is now optional and periodic reporting runs only when a
   world is present, retaining both old tests and new runtime instrumentation.
3. Merge-introduced strict analyzer formatting defects were corrected.

## Counted games

### Game 1: VIKI repair lifecycle

- Seed `961201`; VIKI (Nod) versus Brutalis (GDI); ordinary AIs, all modules;
  headless MAX; 2,200 valid world ticks; passed.
- Fixture map SHA-256:
  `ed173b4642e37d524510a7120762521bbc05594524103ba55a874223b89f2a2e`.
- A pre-spawned, reachable VIKI Repair Facility accepted the same damaged Stealth
  Tank at 6,000/15,000 health. Health increased, reached 15,000/15,000, the tank
  rejoined, and it continued useful harassment against a Harvester.
- The surviving member remained controlled after its partner's scripted loss; a
  replacement restored two reserved Stealth members with no ordinary-owner leak.
- No air squad activity, fatal error, desync, or unhandled exception was observed.
- The distinct active no-repair fallback was not mixed into this counted proof;
  this remains a bounded evidence limit rather than an observed defect.

### Game 2: load shedding and ownership pressure

- Seed `961202`; two ordinary Iron Reaper AIs; all modules; paced rendered mode;
  1,000 valid world ticks over 50.093 seconds; passed.
- Fixture map SHA-256:
  `2b29080d7cb31bb05d80525835d28afd960558916197a1b51d300f0e3e2fa305`.
- Load shedding disabled and reprobed advanced modules. Released actors registered
  with the ordinary fallback and were later removed when claimed; Stealth and
  Chemical reservations were exercised.
- Registry audits reported zero corrections while tracking 1,215 actors/339
  eligible actors for player 1 and 1,188/313 for player 2. No Harvester, silo
  Harvester, MCV, or advanced MCV entered fallback combat registration/orders.
- Periodic stall telemetry at tick 1,000 reported a 19.740 ms mean tick, 33 ms p95,
  49 ms p99, and 10 freezes; module calls and orders remained bounded. The 1,595 ms
  maximum was the startup sample, not sustained runaway work.
- A handoff save was created at tick 600 and teardown at tick 1,000 was clean, with
  no fatal error, desync, natural game-over, or unhandled exception.
- The field-defense module was active but reported zero committed fields. Thus the
  run does not literally prove field-defender ownership, and no save reload was
  performed. These are bounded cycle-1 evidence limits, not observed corruption.

## Independent review

- Game 1 factual narration and policy review completed. Policy verdict: mostly
  sensible, medium confidence. Safe repair/rejoin is accepted; danger-screen,
  balance, targeting, and cadence changes were explicitly declined.
- Game 2 factual narration and policy review completed. Policy verdict: mixed,
  medium confidence. Ordinary fallback was accepted as containment, not proof of
  advanced-policy quality. The recommendation for exact per-actor ownership checks
  is accepted for cycle 2; economy-extension and broader strategy work remain out
  of scope.
- Both reviewers' durable general lessons were atomically promoted to the bounded
  policy scratchpad.

## Required bounded cycle 2 focus

Use the same cumulative candidate and one adversarial ordinary-AI pressure fixture
that guarantees at least one actual field-defense commitment. Assert after every
shed/probe transition that each released specialist and field defender has exactly
one eligible owner, while Harvesters remain excluded. Continue from a created save
and verify post-load ownership, CPU/order bounds, and clean teardown. Do not change
balance or policy; modify product code only if this focused run reveals a concrete
defect.
