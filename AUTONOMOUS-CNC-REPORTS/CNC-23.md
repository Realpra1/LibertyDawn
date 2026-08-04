# CNC-23: Allied Recovery Help

- Status: complete; GitHub Linux and Windows checks passed
- Cycles used: 11 of 30
- Branch: `agent/cnc23-allied-help`
Pull request: https://github.com/Realpra1/LibertyDawn/pull/42

## Behavior

An AI can now request a supply truck for an allied playable AI that has no spendable cash, MCV, harvester, or refinery but still has a production building or mobile unit. The truck uses the normal delivery order against a compatible allied building. Aid stops when the recipient recovers cash or any critical asset, and eliminated allies are ignored.

Dispatches use a rolling five-minute limit of one truck per live donor war factory or airfield. Pending and queued production reserves capacity so slow queues cannot overproduce aid trucks. Supply trucks are externally managed for all CNC AIs, preventing adaptive/random production from bypassing the limit.

## Design choices

- Kept state scanning, recipient selection, rate limiting, and production requests in `AlliedSupplyAidManager`; the existing special-order module owns only truck assignment and delivery lifecycle.
- Used deterministic cash/asset snapshots and actor-ID tie breaking.
- Counted a deployed production building or any mobile unit as evidence the ally can still recover.
- Reused CNC-22 stall recovery for allied destinations, including blocked or destroyed targets.

## Validation

- Strict Debug build: passed with zero warnings and zero errors.
- Unit tests: 246/246 passed, including recovery conditions and per-factory capacity.
- Explicit-interface, conditional-interface, and CNC YAML/map validation: passed.
- Successful aid: donor requested one truck, delivered it, consumed the truck, and raised recipient cash from 0 to 880.
- Recovered recipient: gaining a harvester cancelled aid; a completed truck reverted to the donor's own delivery target.
- Eliminated recipient: no truck was requested.
- Two factories: at most two aid requests remained committed during the rolling interval.
- Blocked nearest allied target: the truck retargeted to an accessible allied building after the no-progress timeout.
- Normal Empire Earth regression: two Skynet and three Brutalis AIs loaded, constructed, produced units, and issued orders without fatal errors.

## Failed cycles and corrections

- The first two-factory fixture exposed pending reservations expiring while a truck was still genuinely queued. Queued, requested, and newly produced unassigned trucks now protect those reservations.
- The first post-extraction delivery fixture was too distant to prove completion within its observation window. A nearer accessible target produced complete delivery evidence.

## Remaining risks

- An ally with only mobile combat units but no compatible cash-accepting building cannot receive a truck until it owns such a target.
- Recovery is evaluated on the five-second special-order scan, so an in-flight truck may retain its old order briefly after the ally recovers.
