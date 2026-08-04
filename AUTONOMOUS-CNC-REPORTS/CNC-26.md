# CNC-26: Iron Reaper Technology Switching

- Status: complete
- Cycles used: 12 of 30
- Branch: `agent/cnc26-tech-switching`
Base: `origin/agent/cnc24-6-headless-max`
- PR: https://github.com/Realpra1/LibertyDawn/pull/47
- Commit: `1720a9b398`

## Plan and literal acceptance

Desired outcome: in a real match, an Iron Reaper detects an enemy technology-branch change, retains that observation for a configurable delay (default about two game minutes), then purchases the first upgrade in the counter branch: covert over economy, recon over covert, and economy over recon. It continues that branch through ordinary production. Technology already owned through captures remains usable by every AI.

Forbidden outcomes: Skynet, VIKI, Brutalis, or legacy bots switching; Iron Reaper switching before the delay; wall-clock rather than game-tick timing; non-deterministic selection; repeatedly resetting or buying conflicting branches; opening/economy recovery deadlocks; disabling captured off-branch production; or changing branch balance/config outside Iron Reaper policy.

Implementation and evidence plan:

- Inventory current upgrade production, authored branch weights, branch exclusivity/downgrade mechanics, and every queue manager that can reserve cash or issue upgrade orders.
- Add a focused, configurable Iron Reaper switching policy with deterministic enemy/branch tie-breaking, game-tick delay state, clean cancellation/reselection, and bounded transition/rejection logging.
- Add pure policy/state tests plus build, unit, interface, and CNC YAML validation.
- Run a literal real-AI acceptance game, then at least three distinct adversarial engine games covering all counter relationships, multiple/defeated enemies, queue/economy contention, captured technology, and unchanged ordinary AIs. Include a naturally completed Fastest match and a final original acceptance regression.
- Commit and push only the task branch, open a cumulative PR against `agent/cnc24-6-headless-max`, and require green Linux and Windows checks before completion.

## Normal AI contention inventory

- `UnitBuilderBotModule` and all production queues that choose/buy upgrade actors.
- `BaseBuilderBotModule`, opening construction, adaptive cross-queue spending, and emergency economy recovery that may reserve the same cash or queues.
- Upgrade downgrade and `maxupgrades` prerequisite actors, owned/captured prerequisite providers, TechTree ownership updates, and captured factories/barracks exposing off-branch units.
- Enemy selection, ownership changes, defeat/removal, and multiple enemies changing branches near the same time.

## Implementation

- Added a configurable Iron-Reaper-only technology counter module with deterministic branch observation, game-tick delay, counter mappings, upgrade/downgrade actor mappings, save/load state, and bounded transition/rejection/completion logging.
- Kept ordinary technology mutually exclusive by repeatedly buying the configured downgrade until the old branch is gone, then buying the desired tiers in order. `Technology Level: Extra` instead preserves already owned branches.
- Isolated all managed upgrade/downgrade actors from Iron Reaper's adaptive random production and removed the opening policy's conflicting forced Recon unlock. Emergency economy and opening-refinery recovery retain priority.
- Added eight pure tests for deterministic dominance/ties, all counter relationships, immature/missing observations, exact delay behavior, downgrade choice, and tier order.

## Cycles

1. **Failed, opening contention.** Iron Reaper's opening-defense unlock repeatedly requested Recon while the counter manager downgraded it. Removed the conflicting Iron-Reaper-only opening unlock while leaving Skynet unchanged.
2. **Passed, Economy to Recon.** Observed enemy Covert at tick 2, retained Economy III through tick 3,002, then downgraded Economy and completed Recon III.
3. **Passed, live enemy switch.** Observed enemy Economy, completed Covert III, then observed the live enemy change to Covert at tick 12,877. The desired counter changed to Recon at exactly tick 15,877 and completed Recon III without premature switching.
4. **Partial/pass, natural match and contention.** Observed an enemy transition to Recon, waited exactly 3,000 ticks, and began Covert-to-Economy conversion. Emergency refinery recovery correctly paused the manager; it later resumed and reached Economy II before the match ended naturally. A clean Economy III completion remains to be demonstrated.
5. **Passed, clean Recon to Economy completion.** High-cash real AI play kept the conversion out of emergency recovery: Iron Reaper waited the full delay, removed Covert III, and completed Economy III against Recon.
6. **Passed, save/load mid-delay.** Saved at tick 1,500 after an Economy observation at tick 2, loaded the save, and retained the pending decision. The loaded game changed to the Covert counter without emitting a new observation/reset.
7. **Passed, Extra technology preservation.** With `Technology Level: Extra`, Iron Reaper retained Economy III and added Covert I-III without issuing any Economy downgrade.
8. **Passed behavior, multi-enemy reset; incomplete final tier.** With allied VIKI and Skynet enemies, the dominant observation changed from Recon at tick 8,127 to Covert at tick 10,752. The stale Recon observation never matured; the manager waited until tick 13,752 before selecting Recon, then reached Recon II before combat triggered emergency recovery.
9. **Passed after edge-case fix, Nod start.** Review found that the configured Economy fallback could make a Nod start abandon owned Covert before any mature observation. Initialization now selects the actually owned starting branch. A Nod Iron Reaper initialized to Covert at tick 2, never downgraded it, and completed Covert III against Economy.
10. **Passed, multi-enemy completion after final queue fix.** Added cancellation of obsolete pending and queued technology transitions before a mature replan. In a balanced two-versus-two match, Iron Reaper countered Covert with Recon III, observed the dominant enemy branch change to Recon, waited exactly 3,000 more ticks, then completed Economy III.
11. **Passed, ordinary bots isolated.** A VIKI-versus-Skynet match emitted zero technology-counter lines. Skynet continued its existing adaptive Economy downgrade and Recon I/II production, proving the new module and external-management list are Iron-Reaper-only.
12. **Passed, complete graphical Fastest match.** At ordinary 20,000 credits, Iron Reaper held Economy until tick 3,002, completed Covert III, observed VIKI change to Covert at tick 10,252, held until tick 13,252, and completed Recon III. Iron Reaper then decisively reduced VIKI from roughly 112 mobile units to zero while retaining 300; the graphical game reached its natural result screen and flushed a valid 1.23 MB replay.

## Validation

- Strict Debug build: passed, zero warnings and errors.
- Unit tests: 285 passed, including eight focused technology policy tests.
- Explicit-interface and conditional-trait-interface checks: passed.
- Full CNC YAML utility: no diagnostic output, but exceeded a 20-minute local timeout; recorded as inconclusive rather than passed. Required GitHub Linux and Windows checks are the final whole-repository authority.
- GitHub checks: Linux passed in 3m13s and Windows passed in 5m38s.

The golden updated-versus-disabled identical-bot comparison was not introduced because bot type is the condition boundary and this engine has no hidden test-only bot type. Adding a permanent lobby-visible clone would have changed the shipped bot list and other bot-type strategy gates, so real branch-transition matches plus the ordinary-bot isolation match provided the safer evidence.

Archived logs, performance traces, and replays are in `AUTONOMOUS-CNC-LOGS/cnc26-cycle1-*` through `cnc26-cycle4-*`.
