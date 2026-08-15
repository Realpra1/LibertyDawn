# Round 06 resume cumulative integration state

## Status

- Release branch: `agent/round-20260815-bug-polish-06-resume`
- Cycle-3 assigned clean base: `fef3e96e4ccbc3b5b5520c5208ba8833b44392df`
- Product candidate before integration receipt commits: `5e74c362ba95986b422516bda074128262355081`
- Integration cycles 1-3: complete
- Cycle-3 counted full-engine games: exactly 2
- Concrete cycle-3 defect: after a valid exact unload emptied cargo, the passenger briefly existed outside both cargo and the world. `DiscardInvalidPairs` treated that engine-frame handoff as a dead lifecycle, released every valid pair, and skipped ordinary squad adoption.
- Narrow correction: retain a nondead passenger/pair only during the existing bounded `AssaultOrderRetryTicks` interval after an unload order, and delay `FinishWave` while that handoff remains pending. Dead actors still invalidate immediately; no balance, timing, retry, geometry, or policy value changed.
- Release blocker: none established.
- Next authorized action: a fresh Sol-medium integration worker may begin cycle 4 from the post-receipt head. It must receive the policy dispositions below, preserve exactly two distinct games, keep balance/policy frozen, and stop before cycle 5. This worker stopped before cycle 4 and did not push or merge `bleed`.

## Checks and review

- Worktree began clean at the exact assigned base.
- `make check`: pass; Debug CNC build completed with 0 warnings and 0 errors, followed by passing explicit-interface and conditional-trait override checks.
- HeavyDrop-focused `dotnet test` slice: exit 0.
- `git diff --check`: pass.
- Fresh native `gpt-5.6-luna` cycle-3 code review: `clear`; `advisory_concern: none`.
- Top-concern disposition: none existed, so no code response or cycle-4 correction is required.
- Review: `analysis/20260815-round06-integration/cycle-3/reviews/code-review/CODE-REVIEW.md`.

## Counted game A: HeavyDrop post-unload lifecycle

- Custom map derived from shipped CNC content through the prior integration map, not a fixture:
  `analysis/20260815-round06-integration/cycle-3/maps/round06-heavydrop-post-unload.oramap`
- Map SHA-256: `aa34e3fdc2497b3054e9419081d3e83ba9dbb53f4c94b3cd30c5a620ed9205bb`
- Seed `606301`; GDI Brutalis versus Nod IronReaper; ordinary AIs, all normal modules, unrestricted tech, one deliberately destroyed carrier/pair, surviving HeavyDrop pressure, headless MAX.
- Passed with exit 0, 2,200 valid world ticks, and 10.011 seconds wall time. No fatal error, exception, or desync signal was observed.
- Wave 1 created ten pairs at tick 3. The destroyed carrier/passenger pair released independently at tick 120 with `unloadOrders=0`; no later HeavyDrop order targeted it.
- Nine surviving pairs each submitted exactly one HeavyDrop unload (9 total, 0 retries), then all nine completed at tick 1,717 with `carrierUsable=True` and `passengerInWorld=True`.
- The ground-force handoff adopted `9/9` passengers into ordinary squad activity. A later ten-pair HeavyDrop wave was created at the same tick using the released transport pool, directly proving continuation/reuse.
- Performance: mean tick 3.291 ms, p95 3 ms, p99 7 ms, max 2,767.387 ms; five bounded freeze-threshold samples included the mass handoff outlier and did not prevent the configured stop.
- Evidence: `analysis/20260815-round06-integration/cycle-3/games/game-a-counted-2200-rerun/round06-heavydrop-post-unload/`.

## Counted game B: cumulative queue/economy recovery

- Distinct custom map derived from shipped `Empire-Earth.oramap` through the matched integration scenario, not a fixture:
  `analysis/20260815-round06-integration/cycle-3/maps/round06-cumulative-queue-economy.oramap`
- Map SHA-256: `2b63743df4f2534b113f7e422e07bae090c3b7359e020f71c7cac6decb96ba87`
- Seed `606302`; GDI Brutalis versus Nod IronReaper; ordinary AIs, all normal modules, unrestricted tech, sustained ready-economy/queue pressure, simultaneous radar loss, headless MAX.
- Passed with exit 0, 6,500 valid world ticks, and 46.055 seconds wall time. No fatal error, exception, or desync signal was observed.
- Both AIs completed their opening Silo goal. Brutalis repeatedly recorded smart-economy reservations and serialized queue fronts with paid progress rather than a total deadlock.
- Brutalis accepted Resonator production at tick 1,801, ordered placement at tick 2,680, and later regained powered one-to-one field coverage after low-power interruptions.
- IronReaper reserved/restored radar at ticks 3,266/3,903; Brutalis reserved/restored at ticks 4,195/4,827 and was operational again after later low-power oscillation.
- Specialist recovery remained active through late play: factory captures and harvester/Orca husk restorations continued, including a harvester restoration at tick 6,313 and a factory capture at tick 6,431.
- Direct air-repair queue evidence was not present because the scripted damaged-aircraft discriminator did not produce an authoritative repair event. Queue, power, radar, field, Silo, and recovery evidence remains valid; cycle 4 may focus the repair-capacity surface without treating this absence as a release blocker.
- Performance: mean tick 6.490 ms, p95 14 ms, p99 20 ms, max 1,362.616 ms; four bounded freeze-threshold samples did not prevent the configured stop.
- Evidence: `analysis/20260815-round06-integration/cycle-3/games/game-b-counted/round06-cumulative-queue-economy/`.

## Fresh native Luna analysis and recommendation dispositions

Each counted game received its own fresh native `gpt-5.6-luna` factual narrator and a separate fresh native `gpt-5.6-luna` policy reviewer. Policy calls were serialized through the shared scratchpad slot and used staged regular-file copies. Valid bounded scratchpad replacements were promoted in order.

### Game A

- Narrative: `analysis/20260815-round06-integration/cycle-3/reviews/game-a-narrator/NARRATIVE.md`
- Policy: `analysis/20260815-round06-integration/cycle-3/reviews/game-a-policy/POLICY-REVIEW.md`
- Verdict: mostly sensible, medium confidence.
- Highest recommendation: a paired control with a fully completed subsequent wave and more identity-linked instrumentation.
- Disposition: rejected as required cycle-3 work and recorded as cycle-4 advisory. The literal lifecycle acceptance is directly proven by zero invalid-pair unloads, nine identity-linked one-order completions, `passengerInWorld=True`, `adopted=9/9`, usable carriers, and later wave creation. A historical control cannot replace acceptance evidence for the new handoff invariant, and another game would violate this cycle's exactly-two contract.

### Game B

- Narrative: `analysis/20260815-round06-integration/cycle-3/reviews/game-b-narrator/NARRATIVE.md`
- Policy: `analysis/20260815-round06-integration/cycle-3/reviews/game-b-policy/POLICY-REVIEW.md`
- Verdict: insufficient strategic evidence, high confidence; required follow-up requested.
- Highest recommendation: one focused adversarial game with IronReaper prerequisites available, Brutalis congestion, radar recovery, and minimal combat/recovery instrumentation.
- Disposition: rejected as a cycle-3 blocker and accepted as cycle-4's preferred Game B focus. This cycle was explicitly a cumulative regression rather than a strategy/control certification, already consumed exactly two valid games, and directly proved sustained Silo/queue/Resonator/radar/recovery operation. The unresolved prerequisite deferral, direct repair-queue evidence, and combat attribution merit a discriminator but do not establish a concrete regression or authorize balance/policy changes.

## Invalid and uncounted launches

- One current-code preflight at tick 3,000 exposed the transient handoff defect and is diagnostic only.
- Several exit-0 Game-A iterations were uncounted because their over-specific manifests missed the selected invalid pair, adoption, or later-wave marker.
- One 16-carrier run timed out before its configured stop under excessive concurrent transport activity, and one tick-2,200 rerun stopped before all lifecycle assertions. Both are invalid and uncounted.
- One pre-map launch was rejected because its output directory already existed. It never counted as a game.
- The final counted map/topology was not changed after the timeout; the successful bounded rerun used the same final map and seed.

## Cycle 3 decision

The transient post-unload classification defect was narrowly corrected and directly verified. Game A proves passenger re-entry/adoption, carrier reuse/continuation, clean invalid release, bounded per-pair unloads, and destroyed-actor safety. Game B passes a distinct sustained cumulative regression with remaining evidence gaps explicitly routed to cycle 4. The fresh code review is clear. No release blocker or balance/policy change is established.
