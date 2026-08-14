# CNC-84 Task Report

## Outcome

Commit `4ad929e441b34399f2bee11874f4e10c82bd2713` repairs the concrete
Skynet helipad queue starvation without changing repair policy or balance.

The reproduced defect was an ownership mismatch. Pending repair claims lived
on individual air squads, while a helipad's `Reservable` capacity is shared by
the whole player. Two Apache squads could therefore both issue an active repair
order to the same free pad during one bot tick. The later engine reservation
replaced the earlier one, and the displaced non-idle order was then treated as
healthy progress forever.

The repair lifecycle now separates holding waiters from active claimants,
checks active claims across all of Skynet's air squads, and records the first
tick at which each aircraft waited. A newly available pad promotes only the
oldest compatible waiter already within the bounded base/pad vicinity; an
incoming distant aircraft cannot reserve over it. If a different squad did
replace a claim, the stale order is replanned through the same shared owner.

## Verification

- Focused `AirThreatGeometryTest`: 68 passed, 0 failed.
- Release build: passed, 0 warnings and 0 errors.
- Lua syntax and `git diff --check`: passed.
- Control scenario, seed 8404, 1,100 ticks: one active claim at a time;
  `#352` repaired before `#353`, distant `#354` waited, and repaired `#352`
  resumed with a routed order to `fact#363`. Healthy `#355` attacked normally.
- Provider scenario, seed 8412, 1,200 ticks: destruction of pad `#350` cleared
  stale destinations; creation of pad `#364` recovered interrupted `#351`, then
  advanced to `#352` and `#353` without duplicate active reservations. Healthy
  `#355` stayed full-health and active.

Raw packages/logs remain outside Git at `/tmp/cnc84-scenarios.Vl3v5D/`.
