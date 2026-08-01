# Deferred Work

## Insane AI strategic economy follow-ups

- Location: `HarvesterBotModule`, `BaseBuilderBotModule`, and their queue managers.
- Impact: Red-tiberium stealth-harvester raids, a conservative harvester-per-refinery congestion proxy, saved excess-cash MCV requests, and a strict cross-queue opening coordinator are implemented. Exact refinery wait-time measurement remains deferred.
- Evidence: `Harvester.Contents` and `LinkedProc` are readable, but nested current/queued activities do not expose a stable public delivery-wait duration.
- Next action: add a read-only refinery wait-duration signal maintained by `DeliverResources`, then compare measured wait pressure against the proxy during playtesting.
