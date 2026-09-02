# CNC AI module performance benchmark

Date: 2026-09-02

Empire Earth4 ran for a fixed 10,000 ticks (400 game seconds) with SkyNet,
VIKI, Brutalis, and IronReaper at corner spawns 1, 6, 34, and 35. Runs were
serial, headless MAX, used matched seeds 9609200-9609204, and disabled bot debug
logging. The control started every configured advanced squad module disabled;
removed the transport, crate, red-Tiberium, garrison, capture, and special-order
controllers; and sent released ground combat through the existing bounded
AttackMove fallback. Economy, production, harvesting, repair, and technology
modules remained enabled so both phases still played complete games.

| Mode | Runs | Avg game seconds | Avg wall seconds | Game/wall | Avg CPU seconds | Avg tick ms | Bot-module CPU seconds |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Simple AttackMove baseline | 5 | 400.000 | 42.005 | 9.528x | 41.890 | 3.891 | 20.504 |
| Full modules | 5 | 400.000 | 58.278 | 6.893x | 59.310 | 5.506 | 31.781 |

Full modules changed average wall time by +38.74%, process CPU time by +41.59%,
mean tick time by +41.50%, and non-overlapping top-level bot-module CPU time by
+55.00%. Nested squad timings are diagnostic subdivisions and are not added to
their parent module. Full behavior remained 6.89 times faster than wall clock.

Raw local evidence:
AUTONOMOUS-CNC-LOGS/ai-module-benchmark-20260902-2/.
