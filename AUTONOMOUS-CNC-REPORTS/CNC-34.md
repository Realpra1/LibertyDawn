# CNC-34: First Economy Tower

- Status: locally complete; awaiting required PR checks
- Cycles used: 30 of 30
- Branch: `agent/cnc34-first-economy-obelisk`
- Base: `origin/agent/cnc33b-red-tiberium-deploy` at `3ccf143607`

## Literal acceptance

Start an ordinary GDI Skynet with its normal economy specialization and all normal AI modules. If an Obelisk is buildable, the first defense structure it requests, completes, and places must be an Obelisk; it must not spend the first defense-queue slot on a SAM site. The Obelisk must be placed by the existing first-tower planner beside the initial construction yard and visibly defend that yard during an early enemy rush.

If an Obelisk is not buildable, the opening must continue instead of stalling and may use the existing guard-tower/turret alternatives after their configured unlock. Low power, missing refinery recovery, authored opening structures, five harvesters/one MCV, construction placement, limits, save/load, and non-Skynet bots must remain unchanged.

## Design and contention

GDI starting units already grant Economy III, making `obli` buildable in the shared GDI/Nod defense queue. Skynet's ordered `OpeningDefenseTypes` currently lists only `gtwr, gun`, while `obli` is already recognized by the first-tower placement planner. Prefer the smallest rules-only correction: put `obli` first in Skynet's opening defense alternatives and optional list, retaining the existing types as deterministic fallbacks.

The defense queue contends with low-power recovery, the ordered opening coordinator, defense upgrade requests, authored building fractions/delays/limits, adaptive defense scoring, wall planning, first-tower placement, multiple Facts, and save/load state. Tests must prove selected/requested/completed/placed order—not only buildability—and include a GDI economy opening with short-distance rush pressure, an unavailable-Obelisk fallback, queue/power contention, save/load, a natural MAX match, three distinct adversarial games, and a final literal regression.

## Implementation

- Skynet's ordered opening-defense alternatives are now `obli, gtwr, gun`; the Obelisk remains optional so a faction/technology that cannot build it does not stall.
- A default-off `PrioritizeOpeningFirstTower` option lets the existing first-tower planner reserve the first buildable configured opening defense before the shuffled defense fallback. Only Skynet enables it, so other bots retain their prior selection behavior.
- The reservation is shared across defense queues and all Facts, retries after the normal opening timeout, and yields to low-power checks. Until the preferred tower completes, Skynet processes defense queues before its building queues so the start order is not cash-starved by parallel Facts.
- While a preferred first-tower build is reserved or queued, the opening coordinator defers its incompatible defense-technology unlock. Skynet's ordinary `downgrade.economy` choice is delayed to tick 5,000 so it cannot remove Economy III during Obelisk construction; normal branch logic resumes afterward.
- Placement, legal-cell fallback, save/load completion state, building limits, and post-first-tower selection remain owned by the existing planner and queue code. Release debug switches remain false.

## Evidence

- Strict Debug and Release builds passed with zero warnings, both interface checks passed, 406/406 unit tests passed, and Lua plus exhaustive CNC YAML/map validation passed. Existing focused opening-order and first-tower placement suites passed 9/9; `git diff --check` is clean.
- The literal GDI Skynet opening logged one preferred `obli` reservation, selected the nearest legal cell beside the initial Fact, completed the Obelisk before radar, and never selected a SAM first. The initial apparently successful seed exposed that rules order alone still fell through the shuffled selector, which drove the deterministic reservation implementation.
- With Obelisk removed from the rules, Skynet continued its opening and completed the faction-valid Gun Turret fallback beside the Fact. The final fallback run acquired Recon through normal delayed branch production instead of the harness's expected explicit opening request; the observable fallback and every forbidden condition passed.
- A high-contention setup with 20 harvesters and four starting MCVs exposed both a start-order cash race and Economy-to-Recon cancellation. After the final fixes, the same hostile seed produced exactly one Obelisk reservation, one completion, zero stale releases, and no duplicate tower across the parallel Facts.
- A tick-2,500 save with the Obelisk pending loaded through one placement/completion without a second reservation or desync. The custom map was staged only for reload resolution and removed afterward.
- Two short-distance ordinary Skynet-versus-Brutalis games reached natural tick-20,000 conclusions. The final cycle completed the Obelisk beside the Fact, preserved the normal five-harvester/one-MCV opening, produced replay/benchmark evidence, and contained no destroyed-source exception, desync, Lua/rules failure, or unhandled exception.
- Final adversarial cycles 27-29 ran concurrently and covered the ordinary Economy path, unavailable-Obelisk fallback, and four-Fact reservation/technology race. All three player-visible outcomes were clean; the batch marked the fallback failed only because its over-specific expected log named the unlock requester, not because selection, placement, progress, or safety failed. Final natural cycle 30 passed on unchanged code. Raw evidence is ignored under `.build/cnc34/evidence/`, especially `cycles27-29-final-adversarial/` and `cycle30-final-natural/`.

## Remaining risk

The four-Fact fixture intentionally starts far beyond an ordinary opening and validates bounded reservation behavior rather than balance. The fallback path's exact technology producer remains seed-dependent by design, but its resulting first legal ground tower and opening progress are deterministic once buildable. No known functional failure remains at the 30-cycle ceiling.
