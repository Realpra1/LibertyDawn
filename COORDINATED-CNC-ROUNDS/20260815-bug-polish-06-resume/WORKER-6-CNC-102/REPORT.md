# CNC-102 cycle 4 report

## Outcome

Proposed status: **Fourth iteration - testing**.

Cycle 4 identified the missing Iron Reaper queue entry as scenario preemption,
not an ownership-transfer defect. The custom enemy started with a covert branch;
when Iron Reaper's normal technology counter matured, it switched out of Economy
and the completed Resonator lost its `upgrade.economy3` prerequisite. The only
tracked correction removes that enemy starting upgrade from the ignored scenario
generator. Engine behavior, fancy construction, timers, SAM preference, balance,
geometry, and Tiberium rules are unchanged.

Across exactly two full-engine games, both ordinary AIs started ready-only timers.
Game 1 proved Brutalis's one-shot fallback but retained the old enemy branch and
therefore reproduced Iron's preemption. Game 2 removed that unrelated signal,
saved at tick 3000, restored both deadlines on load, and issued exactly one
covered ordinary fallback for Brutalis and Iron Reaper after their respective
deadlines. No fallback appeared before the save.

The intended legal-fancy control was not discriminating: both adjacent tree
projects chose `43,160`, so the east control blocker also blocked Brutalis. This
cycle therefore proves two-AI fallback and persistence, but adds no fresh legal
fancy-placement evidence.

## Verification

- Affected Debug build with warnings as errors: pass, 0 warnings and 0 errors.
- Focused `TiberiumFieldPolicyTest`: 17/17 pass.
- Global CNC and both generated-map MiniYAML validation: pass (the existing
  unused `factundeploy` condition warning remains scenario-local).
- Scenario generator Python syntax and `git diff --check`: pass.
- Game 1, blocked two-AI, tick 5200: both timers started; Brutalis fallback
  `4097 > 4067`, exactly once; Iron preempted before fallback.
- Game 2, save/load control: saved at tick 3000 with deadlines `4063` and `4431`
  and no early fallback; reload restored both deadlines; Brutalis fallback
  `4070 > 4063`, Iron fallback `4445 > 4431`, exactly once each; tick 5200 exit.
- The first reload launch failed before map tick 1 because the isolated support
  map was not staged. After staging the same generated map, the continuation
  loaded and completed; the invalid launch is not counted as a game.

Ignored evidence is retained under `.worktrees/cnc102-cycle4/`, notably
`game-1-run/blocked-two-ai-fallback/`,
`game-2-preload-run/fancy-save-control-preload/`, and the fresh Luna narratives
and policy reviews under `analysis/game-1/` and `analysis/game-2/`.

## Recommendation disposition

- Game 1, high priority: guarantee one ordinary resource-near fallback for every
  fully ready blocked Resonator. **Accepted and tested** in game 2: both restored
  projects issued one fallback after their deadline, including with SAM coverage.
- Game 2, high priority: add deterministic legal-fancy evidence and explicit
  proof that optional SAM preference does not delay timer creation. **Accepted as
  required follow-up**; not run because the authorized cycle requires exactly two
  games and both were consumed. No policy or product change is justified by the
  current evidence.

## Remaining risk and next test

Do not claim acceptance or open a PR. Cycle 5 should construct genuinely distinct
legal and blocked sites, then prove unchanged fancy placement and that SAM
preference cannot delay readiness timing or fallback. Preserve the now-passing
two-AI save/load control and make no engine correction unless new evidence shows
a product defect.
