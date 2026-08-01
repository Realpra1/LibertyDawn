# Deferred Work

## AI allied supply production

- Location: `UnitBuilderBotModule` and `SpecialOrderBotModule`.
- Impact: Existing idle supply trucks are now delivered to economically stranded allies, but the AI does not yet request production specifically for allied rescue.
- Evidence: Production requests currently have no per-ally rescue state or per-factory/airfield five-minute quota.
- Next action: add a typed rescue-production request to `UnitBuilderBotModule`, capped at one truck per 7,500 ticks for each currently available vehicle factory or airfield; cancel pending requests once the ally gains cash, a harvester/refinery, or loses every production building, MCV, and mobile unit.

## Insane AI strategic economy follow-ups

- Location: `HarvesterBotModule`, `BaseBuilderBotModule`, and their queue managers.
- Impact: Red-tiberium stealth-harvester raids and a conservative harvester-per-refinery congestion proxy are implemented. Exact refinery wait-time measurement, excess-cash MCV requests, and a strict cross-queue opening build script remain unchanged.
- Evidence: `Harvester.Contents` and `LinkedProc` are readable, but nested current/queued activities do not expose a stable public delivery-wait duration. Building, defense, and unit production are owned by independent queues without one shared opening-order cursor.
- Next action: add a read-only refinery wait-duration signal maintained by `DeliverResources`, then compare measured wait pressure against the proxy during playtesting. Add expansion requests through the existing `IBotRequestUnitProduction` interface with a saved cash cooldown. Implement opening-order coordination above individual queues so Building and Defense queues cannot advance the same sequence independently.
