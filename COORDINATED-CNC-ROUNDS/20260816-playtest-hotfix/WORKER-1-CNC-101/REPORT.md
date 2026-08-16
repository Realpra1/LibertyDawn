# CNC-101 Worker Report

## Status

Complete-testing; final native Terra review pending.

## Result

- Product commit: `ffd7707c855cd92b83c6894b0506089c9a08a2db`.
- Preserved and audited the interrupted worker's narrow queue-priority edit. The final repair retains that ordering and closes the remaining empty-queue/planner reconciliation lag.
- The pre-four-wall secondary opening is now an explicit policy phase, runs before missing-refinery serialization, and receives next-tick queue/planner reconciliation only until four walls exist. Ordinary maintenance cadence resumes at the boundary.
- Low-power priority, Silo then configured-defense protection, first-refinery recovery, balance values, and unrelated construction remain unchanged.
- The CNC-101 scenario generator was repaired to use a real opposing spawn, correct bot control, faction-neutral opening prerequisites, default wall build durations, and authoritative wall-type/order observations.

## Games

1. GDI Brutalis vs Nod Brutalis, seed `10103`, exit tick `3000`: fourth wall tick `325` (13.0 seconds), Silo `2120`, configured defense `2535`, exact-order PASS, normal continuation `2680`. Narrator PASS: `/root/github/.build/coordinated-cnc/20260816-playtest-hotfix/WORKER-1-CNC-101/analysis/game1-narrator.md`. Policy PASS/no update: `.../game1-policy.md`.
2. Nod Brutalis vs GDI Brutalis, seed `20204`, exit tick `3000`: fourth wall tick `310` (12.4 seconds), Silo `2110`, configured defense `2520`, exact-order PASS, normal continuation `2665`. Narrator PASS: `.../game2-narrator.md`. Policy PASS/no update: `.../game2-policy.md`.

The superseding final games use ordinary Brutalis AIs with all configured modules, two players, default wall build durations, and finish well below the 120-second wall-clock cap. Earlier no-content, wrong-controller, one-spawn, short-horizon, and invalid cross-queue setup attempts are not counted.

## Validation

- `make check`: PASS, zero warnings/errors.
- Full `OpenRA.Test`: PASS, 791/791.
- Focused `OpeningPolicyLogicTest`: PASS, 14/14.
- `./utility.sh cnc --check-yaml`: PASS.
- Scenario `py_compile`: PASS.
- `git diff --check`: PASS.
- Fresh Terra-medium final review: pending at `/root/github/.build/coordinated-cnc/20260816-playtest-hotfix/WORKER-1-CNC-101/analysis/terra-final.md`.

## Policy

Both final policy reviews passed and recommended no persistent scratchpad update.
