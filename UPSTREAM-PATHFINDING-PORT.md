# Selected upstream pathfinding port

## Baseline

LibertyDawn's latest common ancestor with the official OpenRA `bleed` branch is
[`74cced319c`](https://github.com/OpenRA/OpenRA/commit/74cced319c9fe05288ab9c9aef4cbd0d61f2acbe)
from 2022-01-29. The first release containing that commit is `release-20220307`.
This port is intentionally selective: it does not merge or rebase the thousands of subsequent
upstream engine and content changes.

## Ported fixes

- [`2ed0656d1b`](https://github.com/OpenRA/OpenRA/commit/2ed0656d1b36d99d5a1e814ce66c6a6d084fb891),
  [PR #21391](https://github.com/OpenRA/OpenRA/pull/21391): report whether a `Move` reached,
  was cancelled, or could not reach its destination. Parent activities now stop or delay retries
  after an unreachable path instead of launching another potentially exhaustive search every tick.
  The retry delay is 20-30 ticks with deterministic jitter.
- [`300947345f`](https://github.com/OpenRA/OpenRA/commit/300947345fea8361525aee7ed562aa3ebffbb619)
  and [`9dd2ef0636`](https://github.com/OpenRA/OpenRA/commit/9dd2ef0636907cd910049a815bf890ab9da8c0d1),
  [PR #22487](https://github.com/OpenRA/OpenRA/pull/22487): preserve deadlock recovery after the
  cooldown change. A blocking unit can immediately move aside, and its fallback destination must
  be an actually enterable cell.

The original upstream patch also modifies the newer generic docking framework. LibertyDawn predates
that framework, so those files and interfaces were deliberately not imported. The equivalent legacy
harvester delivery code was left unchanged.

## LibertyDawn compatibility audit

- Resource creation, regrowth, rendering, depletion, and map resource data are untouched.
- Harvester resource selection, refinery choice, unloading, claims, search radii, and authored
  timing remain unchanged. Only a failed movement retry is delayed; harvesters retain retry behavior
  instead of abandoning their job.
- Existing AI bot modules, squad logic, production policy, and adaptive weighting are untouched.
- Rules, maps, missions, sequences, and balance values are untouched.
- Activities that should keep pursuing a live objective (`Enter`, `Follow`, harvesting, repair and
  resupply) retry after the cooldown. One-shot movement and attacks stop when their destination is
  proven unreachable.

`MoveResult` and cooldown fields are runtime-only and add no save data. Network peers must still run
the same build because movement timing is simulation behavior. Old replays that exercise failed paths
may diverge when played with this build.

## Validation

`MoveCooldownHelperTest` covers blocked completion, delayed retry, successful/cancelled movement,
hidden targets, and immediate deadlock-unblocking behavior. The normal engine build, lint checks,
unit tests, mod rules, sequences, and map validation remain the integration gate.
