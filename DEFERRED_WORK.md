# Deferred Work

## Insane AI strategic economy follow-ups

- Location: `HarvesterBotModule`, `BaseBuilderBotModule`, and their queue managers.
- Impact: Red-tiberium stealth-harvester raids, a conservative harvester-per-refinery congestion proxy, saved excess-cash MCV requests, and a strict cross-queue opening coordinator are implemented. Exact refinery wait-time measurement remains deferred.
- Evidence: `Harvester.Contents` and `LinkedProc` are readable, but nested current/queued activities do not expose a stable public delivery-wait duration.
- Next action: add a read-only refinery wait-duration signal maintained by `DeliverResources`, then compare measured wait pressure against the proxy during playtesting.

## Strategic ground squads (Tasks 8 and 17 follow-ups)

- Location: `SquadManagerBotModule`, `StrategicGroundTargeting`, and ground squad states.
- Implemented scope: one cohesive mixed assault squad with reinforcements; deterministic bounded 6x6-cell target scoring; exponential defender-overmatch reduction; slowest-unit travel cost; and configurable one- or two-unit stealth harassment squads that prefer exposed economy targets and only clear weak screens as a fallback.
- Deferred Task 8 scope: terrain-domain-aware coarse routing, multiple independently tasked assault formations, formation-aware staging, transport hand-off for lagging reinforcements, and a shared world influence cache if profiling shows the bounded per-squad scans are material.
- Deferred Task 17 scope: moving-detector influence prediction, dedicated disengage/evasion routes for revealed stealth squads, explicit crush orders instead of attack-move opportunities, and coordinated stealth squads that strike different targets.
- Reason: the first integration deliberately favors a smaller save-safe assignment/selection/cohesion loop; route ownership overlaps the transport workstream and should be integrated after its reservation API settles.
