# CNC-26A: Iron Reaper All-Technologies Lobby Mode

- Status: complete
- Cycles used: 11 of 30
- Planned branch: `agent/cnc26a-all-technologies`
Base: `origin/agent/cnc26-tech-switching`

- Draft PR: https://github.com/Realpra1/LibertyDawn/pull/48
Commit: `e5883edacb`

## Literal acceptance

With the lobby's verified all-technologies setting enabled, Iron Reaper alone owns and can use Economy III, Recon III, and Covert III concurrently without issuing downgrades or losing a previously acquired branch. With ordinary technology level, CNC-26's mutually exclusive delayed counter switching remains unchanged. Skynet, VIKI, Brutalis, and legacy bots retain their authored single-branch behavior under both settings.

Forbidden outcomes include granting all branches to ordinary bots, treating every high technology level as Extra, downgrading an Iron Reaper branch under Extra, repeatedly buying already owned tiers, stalling opening/economy recovery, disabling captured/off-branch production, or changing branch unit/building balance.

## Plan

- Verify the exact lobby prerequisite and current `techlevel.extra` actor/condition flow.
- Inventory CNC-26's preservation behavior and ordinary bot upgrade queues for contention.
- Implement the smallest configurable Iron-Reaper-only completion policy needed to acquire every missing branch under Extra while retaining ordinary counter logic elsewhere.
- Add focused state/policy tests and tick-stamped diagnostics.
- Run real games for Extra enabled/disabled, faction starts, ordinary-bot isolation, save/load, recovery contention, and at least three clean adversarial cycles including a complete real match.
- Publish cumulatively against PR #47 and require green Linux/Windows checks.

## Cycles

1. Extra-mode GDI Iron Reaper retained Economy III, then completed Covert III and Recon III without a downgrade (`seed 26101`).
2. Ordinary-mode GDI Iron Reaper preserved CNC-26 behavior: it downgraded Economy and completed only the delayed Covert counter branch (`seed 26102`).
3. Extra-mode VIKI-versus-Skynet isolation control emitted zero technology-counter lines. Skynet independently selected Recon II/III and its authored Economy downgrades, confirming that Extra does not grant the new policy to ordinary bots (`seed 26103`).
4. Nod-start Extra Iron Reaper retained Covert III, completed Economy III, and requested the final Recon III tier. The run ended one upgrade interval before final ownership, so this is supporting rather than final evidence (`seed 26104`).
5. Saved an Extra-mode GDI match at tick 9000 after Recon III ownership and an in-flight Covert I request (`seed 26105`).
6. A short direct reload owned Covert I and requested Covert II without restart, downgrade, or stall.
7. Clean adversarial save/load pass: reloading the same mid-transition state continued through Covert II and III and logged final progress `covert:3,economy:3,recon:3` at tick 12252.
8. Clean multi-enemy adversarial pass: enemy dominance changed from Economy to Recon while Recon III was in flight. Iron Reaper retained the useful transition, changed its preference after the configured delay, and completed all three branches at tick 12127 (`seed 26108`).
9. A 5,000-credit recovery stress match failed because the existing opening stalled at four structures and zero harvesters before the base was captured. The new module correctly stayed paused and spent no recovery resources, so the failure is recorded but is not counted as a clean adversarial pass (`seed 26109`).
10. Clean low-cash contention pass: at 10,000 credits, technology remained paused through refinery/opening recovery, eight structures, five harvesters, and an expansion MCV. It then completed every branch at tick 13877 despite enemy branch churn (`seed 26110`).
11. Full graphical Fastest result: Iron Reaper completed all branches at tick 12377 and decisively eliminated VIKI, with final logged mobile/harvester snapshots of 181/15 versus 0/0. No fatal or unhandled errors occurred; the 848 KB replay is archived (`seed 26111`).

## Validation

- Strict Debug build: passed with zero warnings and errors.
- Unit tests: 286 passed, including the all-branch ordering policy test.
- Explicit-interface utility: no diagnostic output but exceeded a five-minute local timeout; inconclusive under the validator slowness tracked by CNC-66.
- GitHub checks: Linux passed in 3m06s; Windows passed in 4m17s.

## Key implementation choices

- Reused the CNC-26 counter module and added a configurable completion prerequisite instead of creating a second competing upgrade manager.
- Under Extra, enemy technology only reorders the remaining useful branches; it never cancels or downgrades an acquired/in-flight branch.
- Outside Extra, the original delayed exclusive counter path is untouched.
- Activation remains gated by Iron Reaper's existing bot-owner condition, which kept every ordinary bot isolated without adding test-only bot variants.
