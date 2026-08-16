# Worker State: CNC-96A

## Assignment

- Worker: `WORKER-3`; task: `CNC-96A`
- Role model: `gpt-5.6-sol`, medium reasoning
- Base: `fcfafc21a9a6c2aa24e06b3b7c771c94df918d50` (origin/bleed)
- Task branch: `agent/round-20260816-cnc96a-stealth-claim-hotfix`
- PR: none; do not create an independent PR
- Balance: frozen; no unit statistics, costs, production, or unrelated squad tuning changes
- Cycle: `1/1`

## Literal acceptance authority

Every Stealth Tank an AI produces or captures must be claimed by the Stealth Tank squad system. Limit the AI to at most four Stealth Tank squads. Preserve the reviewed squad routing, targeting, repair, reinforcement, and no-repair behavior.

Diagnose actor discovery, ownership transfer, claim/reservation lifecycle, and squad-count accounting before changing behavior. Prove no eligible produced or captured Stealth Tank is left unclaimed, duplicated, stranded, or assigned outside the system, and prove the hard four-squad limit without weakening the reviewed routing, targeting, repair, reinforcement, or no-repair behavior.

## Required one-cycle contract

- Run focused tests and protected repository checks, including build/test validation appropriate to the touched owner and `git diff --check`.
- Run exactly two distinct ordinary-AI/all-module custom games, each under 120 seconds; no third game counts. Exercise both produced and captured Stealth Tanks and the four-squad ceiling across the two games.
- For each game obtain one fresh Luna-medium narrator and one fresh Luna-medium policy reviewer, with evidence paths recorded here/report. Reviews must address claim coverage, squad count, and preserved routing/targeting/repair/reinforcement/no-repair behavior.
- Obtain a fresh Terra-medium final review after the two games and checks.
- Do not edit another stream, create tasks, push, merge bleed, or open an independent PR. Update only this state, the task report, and the task branch.

## Evidence

- Analysis directory: `/root/github/.build/coordinated-cnc/20260816-playtest-hotfix/WORKER-3-CNC-96A/analysis`
- Report: `COORDINATED-CNC-ROUNDS/20260816-playtest-hotfix/WORKER-3-CNC-96A/REPORT.md`
- Product head, checks, game results, narrator/reviewer paths, and Terra review: pending.
