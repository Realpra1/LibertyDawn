# Round 06 resume cumulative integration state

## Status

- Release branch: `agent/round-20260815-bug-polish-06-resume`
- Cycle-2 assigned clean base: `9812ebca7461e4fdf214e59da2da9a9c4ba46360`
- Product candidate before receipt-only commits: `5e74c362ba95986b422516bda074128262355081`
- Integration cycles 1-2: complete
- Cycle-2 counted full-engine games: exactly 2
- Concrete cycle-2 defect: HeavyDrop exact-unload orders were resubmitted every
  world tick in the travelling state because that branch bypassed its existing
  bounded retry gate.
- Narrow correction: apply the existing `ReadyToRetry` gate to travelling-state
  exact unloads and add per-pair tick/order lifecycle diagnostics. No balance,
  policy, tuning, geometry, or strategy value changed.
- Release blocker: none established.
- Required cycle-3 evidence: one focused HeavyDrop lifecycle game proving that a
  genuinely valid pair continues after exact unload while retaining invalid-pair
  release and safe no-repair air activity checks.
- Next authorized action: a fresh worker may begin integration cycle 3 from the
  post-receipt head. This worker stopped before cycle 3 and did not push or merge
  `bleed`.

## Checks and defect evidence

- The worktree began clean at the exact assigned base.
- `make check`: pass; Debug CNC build completed with 0 warnings and 0 errors,
  followed by passing explicit-interface and conditional-trait override checks.
- `git diff --check`: pass.
- Cycle-1 contained 5,601 `AI heavy drop ... committing exact unload` lines.
  Source inspection established that these were 5,601 order submissions, not
  merely duplicate logger output: the travelling-state commit branch had no
  retry check, while the unloading-state retry branch already used one.
- Cycle-2 Game B recorded exactly 9 commit submissions for 9 surviving pairs,
  each at `order=1`, and 0 retry submissions. The scripted invalid pair released
  independently at tick 53 with `unloadOrders=0`.

## Counted game A: matched Resonator/radar discriminator

- Custom map derived from shipped `Empire-Earth.oramap`, not a fixture:
  `analysis/20260815-round06-integration/cycle-2/maps/round06-resonator-radar-matched.oramap`
- Map SHA-256:
  `4f5ef348b18026230103e908856c61a8276750573781fed0440d3c8279df7f7b`
- Seed `606201`; GDI Brutalis versus Nod IronReaper; ordinary AIs with all
  normal modules, unrestricted tech, matched ready-economy starts, one forced
  blocked field site and one legal path, simultaneous named-radar loss,
  headless MAX.
- Passed with exit 0, 6,500 valid world ticks, and 49.060 seconds wall time.
  No fatal error, unhandled exception, or desync signal was observed.
- Brutalis accepted Resonator production at tick 1,701 and placed actor 611 at
  tick 2,651. Its legal continuation accepted and completed two extension steps
  through tick 5,151, then boundedly deferred at `NoLegalProgressCell`.
  IronReaper's forced illegal path deferred with zero progress at ticks 3,622
  and 6,018.
- IronReaper reserved/restored radar at ticks 3,622/4,250; Brutalis did so at
  ticks 4,367/5,010. Both released recovery reservations. Continued production
  and actor outcomes extended through tick 6,187; Brutalis's harvester count
  rose from 17 at tick 3,001 to 68 at tick 6,451.
- Performance: mean tick 6.888 ms, p95 16 ms, p99 24 ms, max 1,397.165 ms;
  six bounded freeze-threshold samples were not attributed to the product fix.
- Evidence:
  `analysis/20260815-round06-integration/cycle-2/games/game-a-final/round06-resonator-radar-matched/`.

Two earlier runs of this scenario reached tick 6,500 and exited 0 but failed
fragile Lua or seed-timing harness expectations. They are invalid and uncounted.
The final manifest uses only stable authoritative engine/module patterns. Record
the recurring fragility of `Media.Debug` acceptance markers as a harness issue;
do not reuse them as required evidence.

## Counted game B: HeavyDrop lifecycle and safe air activity

- Distinct custom map derived from shipped `island-duel.oramap`, not a fixture:
  `analysis/20260815-round06-integration/cycle-2/maps/round06-heavydrop-air-repair.oramap`
- Map SHA-256:
  `8d1bc322de2c50e9b7142139bf7d923dae3fba8ded97cd7cd40435fe7c09f6d1`
- Seed `606202`; GDI Brutalis versus Nod IronReaper; ordinary AIs with all
  normal modules, unrestricted tech, ten-carrier HeavyDrop pressure, conquest
  stop disabled for the bounded discriminator, headless MAX.
- Passed with exit 0, 3,000 valid world ticks, and 10.008 seconds wall time.
  No fatal error, unhandled exception, or desync signal was observed.
- Wave 1 created 10 pairs at tick 3. The scripted invalid pair released at tick
  53 with zero unload orders. Nine surviving pairs boarded, retained distinct
  threat-aware safe-return plans after a bggy-covered cell rejection, and each
  submitted exactly one exact unload at tick 296. They released at ticks
  325-331 and the wave released cleanly.
- The logs mark every selected passenger as `not in world` and finish with
  `restored=0/0`; therefore successful post-unload continuation is not directly
  proven. This is a required cycle-3 evidence discriminator, not a demonstrated
  balance/policy defect and not authority for another cycle-2 change.
- CNC-84 safe no-repair coverage was direct: the ordinary Generic air squad
  continued attacks, target reassessments, threat-aware routing, and defended
  cell rejection with no repair event present.
- Performance: mean tick 2.149 ms, p95 3 ms, p99 6 ms, max 2,004.000 ms;
  five bounded freeze-threshold samples included startup/early TransportManager
  outliers and were not attributed to a release blocker.
- Evidence:
  `analysis/20260815-round06-integration/cycle-2/games/game-b-counted/round06-heavydrop-air-repair/`.

One earlier startup reached tick 1,771 and natural game over before the required
bound; it is invalid and uncounted. One later tick-3,000 run failed only its
over-specific repair marker and is also uncounted. The counted rerun retained
the same scenario while accepting authoritative safe no-repair air evidence.

## Fresh native Luna analysis and recommendation dispositions

Each counted game received its own fresh native `gpt-5.6-luna` factual narrator
and a separate fresh native `gpt-5.6-luna` policy reviewer. Policy calls were
serialized through the shared scratchpad resource slot and used staged copies.

### Game A

- Narrative:
  `analysis/20260815-round06-integration/cycle-2/reviews/game-a-narrator/NARRATIVE.md`
- Policy:
  `analysis/20260815-round06-integration/cycle-2/reviews/game-a-policy/POLICY-REVIEW.md`
- Verdict: pass, high confidence; no code or balance change justified.
- Highest recommendation: retain causal prerequisite/legal-cell tracing only if
  a future diagnostic needs to explain the IronReaper asymmetry.
- Disposition: recorded as advisory and rejected as required cycle-3 work. The
  matched run met the literal discriminator, both radar recoveries completed,
  and the unknown cause does not establish a defect or authorize policy change.

### Game B

- Narrative:
  `analysis/20260815-round06-integration/cycle-2/reviews/game-b-narrator/NARRATIVE.md`
- Policy:
  `analysis/20260815-round06-integration/cycle-2/reviews/game-b-policy/POLICY-REVIEW.md`
- Verdict: required follow-up, high confidence; no further code or balance
  change justified.
- Highest recommendation: one focused valid-pair lifecycle verification after
  the discriminator.
- Disposition: accepted as the required cycle-3 evidence. Cycle 2 already
  consumed exactly two valid games; the 5,601-to-9 defect correction is proven,
  while post-unload continuation remains an evidence gap rather than a second
  proven defect. Balance and policy remain frozen.

Policy reviewers produced bounded updated scratchpads in their isolated ignored
directories. The canonical tracked scratchpad was not changed by this worker.

## Cycle 2 decision

The concrete repeated-order defect was narrowly corrected and verified. Game A
passes its field/radar discriminator. Game B proves bounded per-pair submissions,
independent invalid-pair release, threat-aware continued handling, and safe
ordinary air activity, but leaves valid post-unload passenger continuation for
cycle 3. No release blocker or balance/policy change is established.
