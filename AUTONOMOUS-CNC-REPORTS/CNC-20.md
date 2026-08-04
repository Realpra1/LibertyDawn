# CNC-20: Threat-aware Routing Refactor

- Status: complete; GitHub Linux and Windows checks passed
- Cycles used: 12 of 30
- Branch: `agent/cnc20-refactor`
Pull request: https://github.com/Realpra1/LibertyDawn/pull/39

## Desired and forbidden behavior

The coarse danger-grid router needed a reusable owner independent of aircraft target policy, while preserving every existing path, score, smoothing decision, repair route, and escape route. The task must not alter AI configuration, target selection policy, threat values, balance, or maps.

## Implementation

- Extracted deterministic coarse-grid A*, nearest-safe escape search, and line-of-sight smoothing from the 700-line `AirThreatGeometry` policy class into `ThreatAwareRoutePlanner`.
- Updated air targeting, AA clearing, reinforcement routing, local escape, repair routing, and repair holding to use the extracted component.
- Moved all six route-specific unit tests to `ThreatAwareRoutePlannerTest`; threat/target policy tests remain with `AirThreatGeometryTest`.
- Kept the algorithms byte-for-byte equivalent apart from names and ownership. This provides CNC-21 transports a reusable routing service without coupling them to aircraft target decisions.

## Validation

- Debug build and interface checks: passed with zero warnings and zero errors.
- Unit tests: 239/239 passed.
- CNC-only MiniYAML, default sequences, and all CNC maps: passed.
- Normal Empire Earth run: two SkyNet and three Brutalis bots all loaded, constructed bases, produced units, harvested, and remained responsive.
- Threat-corridor adversarial game: 12 spawned Orcas routed to a harvester past a SAM with six coarse steps, zero exposure, and one smoothed waypoint; 12/12 survived the checkpoint.
- No-threat adversarial game: direct route had zero exposure and was smoothed to one waypoint; aircraft attacked the intended moving harvester and reassessed it across cells.
- Unavoidable-danger adversarial game: 48 Orcas received live mobile-AA influence; routes remained valid with 400-500 finite exposure and five to six waypoints instead of failing; 48/48 survived the checkpoint.

## Failed/setup cycles

- A long five-AI run did not produce aircraft soon enough to prove the extracted path component, so a focused map was created.
- An early focused map left easier enemy-base targets available, so it did not force the corridor scenario.
- Two cleanup attempts called `Kill` on non-killable player/upgrade actors; the harness was corrected with `HasProperty("Kill")`.
- A static SAM owned by a player without power was correctly offline and did not force danger. It was replaced with a live mobile SAM.

## Remaining risks

- The extracted planner intentionally preserves its existing bounded-grid complexity and caller-clamped endpoint contract. Performance and more defensive shared-use validation remain appropriate for CNC-21/CNC-47.
- The extracted planner remains behavior-equivalent and all required GitHub checks are green.
