# CNC AI module performance benchmark

Date: 2026-09-02

Empire Earth4 ran to natural game-over with SkyNet, VIKI, Brutalis, and
IronReaper at corner spawns 1, 6, 34, and 35. Runs were serial, headless MAX,
used matched seeds 9609400-9609404, and disabled bot-debug logging. No run
crashed, desynced, timed out, or disabled an advanced module.

The control started every configured advanced squad module disabled, removed
the transport, crate, red-Tiberium, garrison, capture, and special-order
controllers, and sent released ground and air combat through the bounded
aggressive AttackMove fallback. Economy, production, harvesting, repair, and
technology remained active so every sample was a natural game.

| Mode | Runs | Avg ticks | Avg game sec | Avg wall sec | Game/wall | Avg tick ms | Process CPU sec/1k ticks | Top-level bot CPU sec/1k ticks |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Simple AttackMove control | 5 | 43,978.0 | 1,759.120 | 357.440 | 4.981x | 7.879 | 1.196 | 2.185 |
| Full modules | 5 | 61,725.8 | 2,469.032 | 988.191 | 2.538x | 15.702 | 1.457 | 7.276 |

The full-module games lasted 40.36% longer in simulated time, so raw wall time
is not a like-for-like CPU comparison. The normalized results are:

- simulation throughput is 49.05% lower (`2.538x` versus `4.981x`);
- mean tick cost is 99.29% higher (`15.702 ms` versus `7.879 ms`);
- sampled process CPU per 1,000 ticks is 21.78% higher;
- non-overlapping top-level bot-module CPU per 1,000 ticks is 232.98% higher.

All full-module runs remained faster than wall clock; the slowest individual
sample was `2.160x`. The top-level bot figure sums only module names directly
under each player. Nested squad/owner timings are diagnostic subdivisions and
are not added to their parent modules.

| Seed | Control ticks | Control wall sec | Control game/wall | Full ticks | Full wall sec | Full game/wall |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 9609400 | 49,533 | 442.904 | 4.473x | 56,569 | 752.919 | 3.005x |
| 9609401 | 48,368 | 413.054 | 4.684x | 63,024 | 1,167.185 | 2.160x |
| 9609402 | 35,679 | 265.032 | 5.385x | 58,702 | 987.838 | 2.377x |
| 9609403 | 43,043 | 334.888 | 5.141x | 64,582 | 949.978 | 2.719x |
| 9609404 | 43,267 | 331.323 | 5.224x | 65,752 | 1,083.036 | 2.428x |

The matched 20,000-tick diagnostic that isolated the overload reduced chemical
MassAttack from `4,174.422 ms / 8 calls` (`583.189 ms` maximum) to
`85.487 ms / 14 calls` (`24.799 ms` maximum). Across the five final natural
games, 558 MassAttack owner calls completed without a failsafe transition; the
largest single call was `72.252 ms` in the longest local engagement.

Raw local evidence (not committed):

- control: `AUTONOMOUS-CNC-LOGS/ai-module-benchmark-natural-final-v5/baseline/`
- full release candidate: `AUTONOMOUS-CNC-LOGS/ai-module-benchmark-natural-final-v5-full-rc/`
- matched diagnostic: `AUTONOMOUS-CNC-LOGS/pr143-massattack-fixed-smoke-valid/`
