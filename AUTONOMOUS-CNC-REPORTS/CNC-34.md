# CNC-34: First Economy Tower

- Status: in progress
- Cycles used: 0 of 30
- Branch: `agent/cnc34-first-economy-obelisk`
- Base: `origin/agent/cnc33b-red-tiberium-deploy` at `3ccf143607`

## Literal acceptance

Start an ordinary GDI Skynet with its normal economy specialization and all normal AI modules. If an Obelisk is buildable, the first defense structure it requests, completes, and places must be an Obelisk; it must not spend the first defense-queue slot on a SAM site. The Obelisk must be placed by the existing first-tower planner beside the initial construction yard and visibly defend that yard during an early enemy rush.

If an Obelisk is not buildable, the opening must continue instead of stalling and may use the existing guard-tower/turret alternatives after their configured unlock. Low power, missing refinery recovery, authored opening structures, five harvesters/one MCV, construction placement, limits, save/load, and non-Skynet bots must remain unchanged.

## Design and contention

GDI starting units already grant Economy III, making `obli` buildable in the shared GDI/Nod defense queue. Skynet's ordered `OpeningDefenseTypes` currently lists only `gtwr, gun`, while `obli` is already recognized by the first-tower placement planner. Prefer the smallest rules-only correction: put `obli` first in Skynet's opening defense alternatives and optional list, retaining the existing types as deterministic fallbacks.

The defense queue contends with low-power recovery, the ordered opening coordinator, defense upgrade requests, authored building fractions/delays/limits, adaptive defense scoring, wall planning, first-tower placement, multiple Facts, and save/load state. Tests must prove selected/requested/completed/placed order—not only buildability—and include a GDI economy opening with short-distance rush pressure, an unavailable-Obelisk fallback, queue/power contention, save/load, a natural MAX match, three distinct adversarial games, and a final literal regression.
