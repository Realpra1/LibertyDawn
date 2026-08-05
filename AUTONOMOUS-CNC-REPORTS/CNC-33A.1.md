# CNC-33A.1: Early vehicle-production priority

- Status: in progress
- Cycles used: 0 of 30
- Branch: `agent/cnc33a1-vehicle-production-priority`
- Base: green CNC-24.7 PR #64 head `42ca957a2c`
- Pull request: pending

## Literal acceptance

In a full-engine ordinary VIKI match, first measure the feature-disabled/normal VIKI's actual construction-credit split rather than inferring a percentage from authored building fractions. After an AI owns at least one unloading refinery, if useful war-factory or airfield capacity is inadequate and a viable production-building candidate is buildable, refinery work from opening, minimum, smart-throughput, excess-cash, or authored-fraction paths combined may occupy at most half of its active Fact construction slots. The remaining eligible capacity must establish useful vehicle production early and keep converting cash into combat units and harvesters. Literal-zero unloading-refinery recovery may serialize one Fact and briefly reserve scarce cash until that refinery is live.

Refineries remain uncapped, because they are cheap to simulate and reduce harvester travel. Direct and refinery-granted harvesters retain the shared 90 live-plus-queued target, with a small incidental overshoot acceptable. Sustained floating cash is a failure when useful production is available, but zero credits at every sampled instant is not required. Missing a critical Fact/MCV, unloading refinery, or harvester means literally owning zero of that class.

The player-visible gate is earlier useful vehicle capacity, occupied production queues, and at least comparable early survival/economy in matched ordinary-map VIKI games, plus materially better scaling/outcome on small low-cash maps with limited nearby Tiberium. Evidence must report refinery versus production-building starts and credits, time to useful vehicle capacity, Fact and unit-queue occupancy, spending, harvester growth, income, army/assets, and match outcome.

Forbidden outcomes are a refinery count cap; more than half of active Facts committed to refineries after one unloading refinery exists while a useful vehicle-production candidate is viable; treating `proc: 31` or other per-type ceilings as a measured 31% budget; idling Facts or cash when vehicle production is blocked/unavailable/already adequate; exceeding the shared harvester target through direct/free production; feature-only weakening of the control; or regressing opening behavior, 10K many-production recovery, literal-zero recovery, save/load, SkyNet, replay determinism, or release log quietness.

## Contention inventory

- Every refinery source: parallel opening, legacy minimum, literal-zero recovery, smart throughput, and authored building-fraction fallback; live, queued, reserved, completed, and ready-to-place construction.
- Every production-building source: opening goals, excess-cash/adaptive demand, authored fractions, external requests, and prerequisite/power fallback.
- Active Fact queues and their buildability, production state, placement, destruction/capture, ownership changes, and save/load restoration.
- Vehicle queues, direct/free ordinary and stealth harvesters, combat production, cash/repair spending, MCV loss/recovery, and the shared 90 target.
- Reachable/safe Tiberium supply, small-map scarcity, nearby threats, unloading pressure, and cases where refinery or production construction is genuinely unavailable.

## Plan

1. Audit the prior configurable opening/build-order implementation and its history, then instrument a quiet-by-default construction-spending ledger.
2. Run real normal-VIKI games to measure its economy/combat construction-credit fraction and timing; use that observed baseline, not an assumed 50/50 ratio.
3. Add a focused deterministic policy that reserves no more than half of active Facts for all refinery paths after critical recovery while prioritizing viable inadequate vehicle capacity and falling back without idling.
4. Add unit/integration coverage, then pass strict CNC build, all tests, interfaces, exhaustive YAML/maps, and quiet-release logging checks.
5. Use isolated serial or two-/three-wide Linux headless MAX games for ordinary VIKI A/B, scarce-resource small maps, 10K many-production, zero-refinery recovery, save/load, contention/adversarial cases, and final VIKI/SkyNet regressions.
6. Record concise evidence here, publish one cumulative PR to the preceding task branch, wait for Linux/Windows checks, then continue autonomously.

## Evidence cycles

Pending.
