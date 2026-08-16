# 2026-08-16 Playtest Hotfix Integration Receipt

## Candidate

- Base: `fcfafc21a9a6c2aa24e06b3b7c771c94df918d50` (`origin/bleed`).
- Branch: `agent/20260816-playtest-hotfix-release`.
- Reviewed heads merged in order with ordinary merge commits:
  `f7b6bf5876`, `7bcfd70f25`, `3284f185b2`, `df41aa3101`.
- All four reviewed heads are ancestors of the candidate. No merge conflict or
  integration product correction was required.

## Protected checks

- `make check`: passed; build completed with 0 warnings and 0 errors and both
  interface checks passed.
- Focused `OpeningPolicyLogicTest`, `AdaptiveWeightingTest`, and
  `StealthTankSquadPolicyTest`: 123/123 passed.
- Full CNC YAML/map validation: passed, including both integration maps.
- `git diff --check`: passed.

## Full-engine evidence

Exactly two qualifying distinct ordinary-AI/all-module games were retained under
`.build/20260816-hotfix-integration/` using the canonical installed content and
global game resource slot.

### Game A — GDI opening, routing, and produced specialists

The initial leg reached tick 5000 in 17.015 seconds, then its tick-4990 save was
continued to tick 7000 in 16.014 seconds. This is one continuous physical game.
Four default-duration sandbag walls completed at tick 325 (13.0 seconds), the
Silo completed at tick 4850, and the configured first defense completed at tick
6380, emitting the exact-order pass. Brutalis exercised economy and combat routes
at `split=0.50`; two produced Stealth Tanks were reserved 2/2 with
`ordinary=0`. Replay, saves, benchmark output, and clean configured exits exist.

The initial harness status remains visibly failed because its tick-5000 cutoff
preceded the configured defense; the saved continuation supplies the accepted
end-to-end order. An earlier pre-tick Lua setup failure and an unqualified Nod
scenario that chose 9.4-second cyclone walls are retained as non-qualifying setup
evidence and are not counted. Narrator: `game-a-narrator.md` (conditional pass);
separate policy: `game-a-policy.md` (accept as one continuous Game A, no blocker).

### Game B — Nod alternate-spawn transfer pressure

The distinct Nod Brutalis spawn-2 versus GDI VIKI spawn-1 game ran under an
early three-unit pressure package to tick 7500 in 23.030 seconds. Construction
continued through wall tick 235, Silo tick 5275, additional walls, and configured
defense tick 7355, with exact order. Economy and combat decisions both carried
`split=0.50`. Two Stealth Tanks transferred ownership at tick 751 and two were
produced by tick 1133; Brutalis reached `total=4 reserved=4 groups=2/2/0
ordinary=0`, and no snapshot showed ordinary leakage. The game exited cleanly
with replay/save and no crash or desync. Narrator: `game-b-narrator.md` (pass);
separate policy: `game-b-policy.md` (accept, no blocker).

Scenario-only debug routing instrumentation was removed before final protected
checks and is absent from the candidate diff. Raw logs, saves, replays, maps, and
benchmarks remain ignored under the analysis directory.

## Final gate and publication

- Final native review: READY with no blocker at
  `.build/20260816-hotfix-integration/final-review.md`. The gate used a fresh
  Luna-medium turn because the native-only fixed threads could not provide
  Terra. Its only low-severity note corrects the Game B narrator's arithmetic:
  9.4 seconds is not within 10–16 seconds, and that timing window applies only
  to Game A; this receipt does not use it for Game B acceptance.
- Draft successor PR: `#123`,
  `https://github.com/Realpra1/LibertyDawn/pull/123`.
- CI receipt: both Linux jobs and both Windows jobs completed successfully;
  GitHub reported `MERGEABLE` / `CLEAN` for the reviewed candidate.
- Never merge `bleed`; no release merge is authorized here.
