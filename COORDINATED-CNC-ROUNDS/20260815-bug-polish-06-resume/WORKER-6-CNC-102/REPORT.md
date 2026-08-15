# CNC-102 cycle 5 report

## Outcome

Proposed status: **First iteration - testing**.

Cycle 5 closes the distinct legal-fancy and optional-SAM evidence requested by
the cycle-4 handoff. It does not change engine behavior, policy, balance,
placement timing, SAM behavior, geometry, or Tiberium rules. The tracked change
only makes the ignored custom scenarios discriminating and enables existing SAM
diagnostics.

Game 1 passed through tick 3500. While ordinary SAM reservations and placements
were active, Brutalis's completed Resonator started its ready-only timer at tick
2693 and issued the legal fancy placement order for `43,160` on that same tick.
The project completed at tick 2701, no simple fallback appeared, and later field
development began. This proves optional SAM work did not delay a ready item or
replace legal fancy placement.

Game 2 reached tick 7200 and reconfirmed the rescue path amid active SAM work.
Brutalis and Iron Reaper started their timers at ticks 2596 and 2948, then issued
exactly one SAM-covered simple fallback at ticks 4114 and 4476, after deadlines
4096 and 4448. Its added continuation assertions failed: after fresh trees were
introduced, neither AI produced a second Resonator. The later diagnostics show
cash, harvester-route, queue/admission, and extension-placement constraints, so
the scenario did not maintain the clarified enabling conditions and does not
isolate a concrete product defect.

## Verification

- Affected Debug build with warnings as errors: pass, 0 warnings and 0 errors.
- Focused `TiberiumFieldPolicyTest`: 17/17 pass from the existing test build.
- Global CNC and both generated-map MiniYAML: pass; the existing scenario-local
  unused `factundeploy` condition warning remains.
- Generator Python syntax and `git diff --check`: pass.
- Exactly two custom full-engine games with ordinary Brutalis, Economy Iron
  Reaper, and Skynet and all normal modules: game 1 manifest pass at tick 3500;
  game 2 engine-valid exit at tick 7200 but manifest failure on the two missing
  continuation placement-order patterns.
- Fresh Luna factual narration and a separate fresh Luna policy review were
  completed for each game under `.worktrees/cnc102-cycle5/analysis/game-{1,2}/`.

Ignored raw evidence is retained under `.worktrees/cnc102-cycle5/game-1-run/`
and `.worktrees/cnc102-cycle5/game-2-run/`.

## Recommendation disposition

- Game 1: preserve the ordering that lets a ready Resonator take a legal fancy
  site without waiting for optional SAM. **Accepted and preserved**; the game is
  the direct passing test and no product change is warranted.
- Game 2: rerun continuation with sustained cash, queue capacity, harvester
  routing, and build space to demonstrate more than one Resonator. **Accepted as
  required follow-up**, but not implemented or rerun because exactly two games
  were consumed. A cycle-5 code change is rejected: the failed evidence did not
  isolate a narrow defect, and balance/policy remain frozen.

## Remaining risk and handoff

Legal fancy placement, ready-only timing during active SAM planning, two-AI
post-timeout fallback, one-shot ordering, and save/load persistence are proven.
The clarified ordinary multi-Resonator continuation expectation remains
unproven. If the coordinator routes optional cycle 6, use one resource-sustained,
route-valid custom scenario that permits two legal field projects per ordinary
AI; do not redesign policy or repeat the already-passing fallback evidence.

No PR or Terra final review is requested because the clarified acceptance is not
fully met.
