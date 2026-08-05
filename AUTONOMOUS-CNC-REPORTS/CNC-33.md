# CNC-33: Smart economy

- Status: first iteration
- Cycles used: 30 of 30
- Branch: `agent/cnc33-smart-economy`
- Base: green CNC-32 head `1ef4e17d70`

## Observable goal

An ordinary AI should react to sustained unload congestion by adding one usable refinery at a time, add storage only when a buildable silo can relieve real storage pressure, and convert sustained excess cash into useful production and bounded MCV expansion. Ordinary short queues, an idle refinery, missing silo technology, busy production queues, opening construction, and other AI requesters must not stall or duplicate this work.

The golden comparison requires a feature-enabled AI to outperform an otherwise matched feature-disabled control in both early scaling and the natural late-game outcome. VIKI must remain live without silo technology.

## Implementation

- Added a once-per-second deterministic smart-economy sampler with sustained-evidence hysteresis, configurable thresholds, and saved state.
- Counts only loaded harvesters linked to owned, live resource-accepting refineries near their delivery cells. Congestion is zero whenever any usable refinery is idle; otherwise it counts overflow beyond one service slot per refinery.
- Serializes congestion relief through one outstanding refinery reservation, tracking queued and completed structures with bounded expiry.
- Separates usable unloading refineries from other economy structures such as Tiberium resonators.
- Uses real storage capacity before requesting a silo, so unavailable or zero-capacity silo configurations cannot block another queue.
- Requests expansion MCVs through the existing unit-production interface, serializes their lifecycle, and requires a configurable minimum army-to-assets ratio before investing. Excess-cash expansion is bounded by a configurable total-asset ceiling.
- Preserves external requests while compatible queues are busy and allows a later independent request to use another free queue instead of discarding or globally blocking requests.
- Added bounded decision/progress diagnostics and pure policy unit tests.
- Enabled the policy for Brutalis/Wavemaker, VIKI, Skynet, and Iron Reaper; the scenario-only exclusion list permits matched differentials without replacing their normal AI modules.

## Key choices

- Normal nearest-refinery waiting remains ordinary behavior. Relief begins only after every usable refinery is occupied and at least two overflow harvesters persist for the configured duration.
- Smart construction is additive to the existing opening and normal build logic. It does not own or replace harvesting, resource growth, unloading, or ordinary production selection.
- A 35,000-credit expansion step and 20% army-to-assets readiness floor avoided both no-op expansion and the reproduced early four-yard overinvestment loss. These are authored CNC values, not engine constants.
- VIKI cannot currently produce an expansion MCV without its HQ/radar prerequisite. Smart economy logs and releases the unavailable request instead of stalling; the pending optimized-VIKI-opening task owns that prerequisite progression.

## Test evidence

- The final strict Debug build passed with zero warnings and zero errors. The freshly rebuilt suite contains 343 passing tests, including pressure hysteresis, all-refineries-occupied congestion, storage edge cases, expansion sizing, and army readiness.
- Final explicit-interface checks, conditional-trait-interface checks, and exhaustive `utility.cmd cnc --check-yaml` validation passed every CNC ruleset, sequence set, and shipped map.
- Real full-engine cycles reproduced and corrected opening-MCV lifecycle races, external-request loss behind busy queues, resonators selected as refineries, discretionary spending starving congestion relief, unbounded duplicate refineries, and smart-only logic accidentally weakening the feature-disabled control.
- Cycle 20 fair A/B acceptance: exactly three smart reservations produced three relief refineries, no expiry or duplicate decision, and waiters repeatedly returned to zero. At the bounded stop smart Skynet led 132,440 to 20,500 army value, 303,190 to 22,000 assets, and 13,685 to zero current income.
- Cycle 21 natural adversarial loss exposed unsafe expansion: four early yards left smart Skynet at 8,900 assets/1,500 army against 360,710/145,460. Cycles 22-27 tuned and gated expansion from this evidence.
- Cycle 28 natural final-policy A/B: one delayed extra MCV, two serialized completed relief refineries, no expiry, and a decisive smart result of roughly 108,570 army/361,820 assets versus 8,500/12,100.
- Cycle 29 natural VIKI no-silo A/B: both sides retained active parallel queues and reached up to 20 production buildings with zero silos and no engine failure. The updated side never reached sustained congestion and lost after combat snowballed, so this is a no-stall regression only, not golden comparison proof.
- Cycle 30 swapped-side natural VIKI A/B: updated VIKI kept zero-silo queues live, activated sustained congestion, made three serialized reservations during refinery losses, safely expired two, completed one relief refinery, and reduced measured waiters from twelve to zero. The process exited naturally with zero stderr and a 1,016,603-byte replay. Combat still reversed the early smart lead; final logged assets were 11,300 versus 229,670, so this is functional edge-case evidence but a failed golden outperformance gate.

Raw evidence is ignored under `.build/cnc33/evidence/`; it is intentionally not committed.

## Remaining risks

- The final policy has one clean decisive post-fix natural Skynet differential, not the required three clean adversarial passes plus a final regression. The 30-cycle cap therefore requires `first iteration`, regardless of local/CI health.
- Independent seeded matches in this engine are known to diverge, and spawn economy/combat snowballs materially affected VIKI comparisons. The swapped cycle reduces but cannot eliminate that limitation.
- Excess-cash production scaling overlaps the existing low `NewProductionCashThreshold`; the principal differentiated benefits proven here are bounded MCV expansion and measured congestion relief.
- VIKI cannot demonstrate smart MCV expansion until its normal opening obtains the required HQ/radar technology.
