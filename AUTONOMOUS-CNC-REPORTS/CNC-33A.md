# CNC-33A: Smart-economy refinery throughput follow-up

- Status: first iteration
- Cycles used: 33 of 33 (user extended the original ceiling by three verification cycles)
- Branch: `agent/cnc33a-refinery-throughput`
- Base: green CNC-33 PR #60 head `962b09b5b2`
- Pull request: pending

## Literal acceptance

In full-engine seeded matches with normal AI modules, smart VIKI must calculate refinery throughput demand soon enough to remove sustained harvester unload pressure, exploit genuinely idle Facts concurrently when justified, and keep vehicle queues producing combat units. Refinery totals are not a performance constraint and are intentionally uncapped: extra refineries may shorten harvester travel. Direct production and refinery-granted ordinary/stealth harvesters share a 90 live-plus-queued target. A small incidental overshoot from another source such as captured vehicle husks is acceptable; the feature must prevent the former unbounded refinery-free-harvester growth. It must decisively outperform a feature-disabled VIKI in both swapped spawn orientations and across multiple seeds. The final regression must preserve the proven Skynet smart-economy advantage.

Forbidden outcomes are direct production or refinery freebies continuing materially above the shared 90-harvester target, global unit-production pauses while refinery construction is already funded or queued, one global reservation suppressing justified parallel Facts, duplicate placement/build intents for one Fact/decision, resonators or silos counted as unloading capacity, direct-harvester demand ignoring refinery-spawned free harvesters, opening/emergency-economy regression, altered VIKI `proc: 31` building fraction, or a control weakened by feature-only code.

As an RTS rule of thumb, avoid sustained idle balances while useful production is available and generally spend toward zero; a transient nonzero balance is not a strict failure. Per the user's 2026-08-05 clarification, “missing” means the AI literally owns zero of the critical class: only zero Facts/MCVs, zero unloading refineries, or zero harvesters may justify pausing other production for recovery. A throughput deficit with one or more live refineries may reprioritize an idle Fact but may not reserve cash globally.

## Contention inventory

- Smart and legacy refinery requests, live/queued/reserved unloading refineries, building queues, Facts, placement reservations, power prerequisites, and construction expiry/recovery.
- Ordinary and stealth harvester managers, refinery-spawned free harvesters, direct vehicle-queue harvester requests, combat production, and actual cash shortfall reservation.
- Opening build locks, emergency economy, excess-cash expansion, external building/unit requests, queue destruction/replacement, ownership changes, and save/load restoration.

## Plan

1. Reproduce the current policy in focused tests and inventory live queue/actor data already available to the manager.
2. Add deterministic refinery-throughput demand using capacity/ratio plus congestion, including free-harvester accounting and distinct parallel reservations for idle Facts; remove smart-bot refinery count limits.
3. When the AI has literally zero unloading refineries, serialize one first-refinery build and retain that recovery priority until it is live; this prevents several Facts from splitting scarce cash across incomplete critical builds. After one refinery exists, reserve only an actual cash shortfall and never pause combat spending for throughput additions. Instrument deficit inputs, idle/busy Facts, reservation lifecycle, vehicle-queue uptime, floating cash, and pause reasons.
4. Run strict CNC build, focused/full unit tests, interface checks, and exhaustive CNC validation.
5. Use Linux headless MAX full-engine games for literal acceptance, the shared 90-harvester target across direct and refinery-free sources, swapped/multi-seed VIKI differentials, scarce-cash/queue contention, Covert-2/free-stealth-harvester behavior, destruction/recovery, save/load, three clean adversarial passes, and final VIKI plus Skynet regressions. Include a matched custom scenario with many pre-spawned production buildings but only 10,000 starting credits: unmodified VIKI is expected to choke its production while smart VIKI must scale refinery throughput and convert the constrained economy into useful production.
6. Update durable state/report, publish one cumulative PR against `agent/cnc33-smart-economy`, and require green Linux and Windows checks.

## Resume environment

The previous laptop's ignored `.worktree`, raw evidence, saves, and replays were not present in the Linux VM. The fetched PR #60 head and committed coordinator files were therefore the authoritative handoff. Linux engine tests used `Launch.Headless=true`, MAX speed, Xvfb, llvmpipe, the repository-declared freeware content hashes, flushed logs/replays, and explicit tick-progress evidence.

## Implemented result

- Refinery demand uses live, queued, requested, and refinery-pending free ordinary/stealth harvesters against live, queued, and per-Fact reserved unloading refineries. Unloading congestion and deterministic ratio capacity can reserve distinct idle Facts concurrently after critical recovery.
- Smart-bot refinery count limits were removed; VIKI's authored `proc: 31` building fraction remains unchanged. Resonators and silos do not count as unloading capacity.
- A feature-enabled AI with zero live unloading refineries exclusively serializes one refinery across opening, legacy-minimum, throughput, and authored-fraction construction paths. Other construction queues wait (except a required power prerequisite) until it is live. Once one refinery exists, throughput additions never pause combat spending.
- Direct and refinery-granted `harv`/`sharv` share a configurable 90 target. Unit queues, normal production, airdrop delivery, and free-refinery actors count live, queued, in-flight, and same-tick reservations. A small incidental overshoot from an unrelated source such as recovered husks remains acceptable per the final user clarification.
- In-flight airdrop actor types and carrier identity are persisted for cap accounting across save/load. The generic policy is unit-tested and configured for every CNC bot plus GDI war-factory and Nod airfield delivery paths.
- Cash behavior is measured as active conversion rather than a strict zero snapshot. Throughput demand does not reserve global cash; literal-zero refinery recovery is the only new construction exclusivity.

## Evidence cycles

- Cycles 1-5 covered implementation/build setup and Linux harness bring-up. Missing X, an invalid mod-content redirect, and an empty harvester cache were diagnosed; cycle 5 established the working Xvfb/llvmpipe headless path.
- Cycles 6-12 exercised ordinary VIKI, the original 75 boundary, clarified literal-zero pause behavior, save/load, and the first throughput differential. These exposed invalid early A/B geometry and production-source accounting gaps.
- Cycles 13-18 added same-tick shared reservations and in-flight airdrop accounting. Cycle 14 held exactly 75 under direct/free contention. Cycle 17 ended in decisive smart-VIKI victory on the favorable orientation; cycle 18 used the corrected far spawn pair and showed sustained higher income/throughput, but was stopped after a recovered stealth-harvester husk produced 76 under the then-75 requirement. The user subsequently raised the target to 90 and allowed small incidental overshoot.
- Cycles 19-27 changed and validated the 90 target, then built the requested 10,000-credit fixture with 12 pre-spawned production buildings per VIKI. The fixture successively exposed the mobile-only starting spawner, four-way refinery cash splitting, opening-versus-legacy contention, and normal throughput contention during zero-refinery recovery. Strict builds and focused policy gates were clean after each correction; an initially stale no-build test invocation was counted and corrected.
- Cycles 28-29 found the last two zero-refinery cash consumers: the authored 31% refinery fallback and unrelated construction queues. Both were routed into the single critical recovery decision.
- Cycle 30 passed the strict full gate and the final 10K engine differential. Smart VIKI reserved exactly one refinery, completed it with 3,946 spendable credits remaining, and immediately resumed ordinary parallel construction and unit production. At the bounded stop it had 10 live refineries, 343,560 earned, 352,129 spent, 126,350 current income, 182,070 army value, and 413,420 assets. Control remained at one credit, zero earned/income, 4,180 army value, 65,180 assets, and four incomplete refineries. Smart VIKI reached exactly 90 live-plus-queued harvesters; four later free `SHARV` spawns were explicitly suppressed at 90 while 5/17 to 15/16 combat-capable vehicle queues remained occupied during the observed scaling window. Headless/MAX activation and world ticks through 14,000 were logged.
- Cycle 31 swapped the 10K fixture to smart spawn 1/control spawn 35 with seed 3331007 and saved at tick 1,300. Smart completed exactly one critical first refinery while control split its cash across four incomplete refineries. By tick 12,000 smart had reached exactly 90 live-plus-queued harvesters, eight refineries, 293,533 earned, 301,364 spent, 143,320 army value, and 345,370 assets; control still had zero income and about 64,800 assets. The save flushed successfully.
- Cycle 32 was an invalid reload harness: the fresh support directory omitted the generated map package referenced by the save, so loading stopped before simulation. The missing map was added for the corrected run.
- Cycle 33 loaded the cycle-31 save in fresh support. It exposed a replay-tail window where the restored manager could issue another smart refinery intent before recorded production orders settled. A one-sampler post-load request guard removed that independent intent; the corrected reload resumed headless MAX at tick 1,303, counted all restored refinery/free-harvester obligations, reached exactly 90 live-plus-queued harvesters, and explicitly suppressed repeated free `SHARV` spawns while 12-15 of 16 vehicle queues remained active. Final locked-Nod comparisons then preserved a decisive SkyNet advantage (at tick 10,000: smart 191,955 earned/34,040 army/212,340 assets versus control 80,565/7,270/85,570), but exposed VIKI construction contention. Routing opening, minimum, throughput, and authored-fraction refinery work through one reservation and preserving roughly half the Facts for other construction improved production coverage. Even after that correction, the same VIKI seed still trailed at tick 10,000: smart 139,819 earned/21,620 army/109,070 assets versus control 166,891/36,960/149,510, and smart had entered zero-yard MCV recovery. This fails the final consistent-VIKI gate, so the capped result is honestly classified `first iteration`.

Raw ignored evidence is under `.build/cnc33a/evidence/`, especially `cycle18-throughput-far-spawns-swapped/` and `cycle30-10k-many-production-exclusive/`. The generated 10K map remains an ignored test artifact under `.build/cnc33a/scenarios/`.

## Automated gates

- Strict Debug `OpenRA.sln` build: zero warnings and zero errors.
- Full `OpenRA.Test`: 354 passed, zero failed/skipped.
- `--check-explicit-interfaces`: passed.
- `--check-conditional-trait-interface-overrides`: passed.
- Exhaustive `./utility.sh cnc --check-yaml`: passed for all CNC sequences and maps.
- `git diff --check`: passed.

## Remaining gates and risk

The literal 10K recovery, swapped orientation, shared 90 cap, save/load accounting, and SkyNet regression are strong. The final ordinary locked-faction VIKI comparison still failed despite reducing refinery construction contention, so this result cannot be called complete. A future iteration should determine why VIKI converts fewer Facts and harvesters into combat production on this seed without reintroducing refinery caps, global throughput cash pauses, or weakening the control. Publication and required PR checks remain.
