# Deferred Work

## AI allied supply production

- Location: `UnitBuilderBotModule` and `SpecialOrderBotModule`.
- Impact: Existing idle supply trucks are now delivered to economically stranded allies, but the AI does not yet request production specifically for allied rescue.
- Evidence: Production requests currently have no per-ally rescue state or per-factory/airfield five-minute quota.
- Next action: add a typed rescue-production request to `UnitBuilderBotModule`, capped at one truck per 7,500 ticks for each currently available vehicle factory or airfield; cancel pending requests once the ally gains cash, a harvester/refinery, or loses every production building, MCV, and mobile unit.

## Insane AI strategic economy follow-ups

- Location: `HarvesterBotModule`, `BaseBuilderBotModule`, and their queue managers.
- Impact: IronReaper counter-technology switching is implemented, but stealth-harvester red-tiberium raids, refinery unload-congestion feedback, and a strict cross-queue opening build script remain unchanged.
- Evidence: harvest activities do not currently expose carried resource composition to a bot module; base construction prioritizes refinery/storage capacity and excess-cash production independently, without an unload-delay signal or one shared opening-order cursor.
- Next action: expose a read-only harvester cargo summary and refinery wait metric, then add deterministic policy helpers and saved per-player state before wiring existing Move/Harvest and production orders. Implement opening-order coordination above individual building queues so Building and Defense queues cannot advance the same sequence independently.
