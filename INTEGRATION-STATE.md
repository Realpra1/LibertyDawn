# Round 06 resume cumulative integration state

## Status

- Tested release branch: `agent/round-20260815-bug-polish-06-resume`
- Tested cumulative head: `5e74c362ba95986b422516bda074128262355081`
- Integration cycle 1: complete
- Counted full-engine games: exactly 2
- Concrete cumulative product defect: none observed
- Product-code or balance change: none
- Release blocker: none from cycle 1
- Next authorized action: a fresh worker may begin integration cycle 2 from the
  post-receipt head. This worker stopped before cycle 2 and did not push or merge
  `bleed`.

## Verified ancestry and protected checks

The worktree began clean at the exact assigned cumulative head. The common base
`4f806e742bd12145d2a601cc9ff71c3a0b141a13` and all reviewed task heads are
ancestors of the tested head:

1. CNC-95 `d4bb47a6f1798bf4f9e1aca358e7afe963f840d7`
2. CNC-101 `6d386d83f505609c7c7704492c1645a7a6dc6053`
3. CNC-103 `f8fc1d447d2dee324a1e3fbfcd57d95c86a29ac0`
4. CNC-106 `0100a95f96d24d8f40aef4489a0937b39e510a31`
5. CNC-102 `22160c18dfd657196a51a5059ce1496c0d686536`
6. CNC-84 `83ded7c1a1ccb45e7f6b4b7488a9d567da83872a`
7. CNC-110 `4a515170f1b5a15f5e37fba6dba81bfab8f08db9`

- `git diff --check` from the common base through the tested head: pass.
- Protected `make check`, using the candidate's reviewed protected-entry helper
  with the canonical `/root/github/LibertyDawn/.agents/locks` namespace: pass;
  Debug CNC build completed with 0 warnings and 0 errors, followed by passing
  explicit-interface and conditional-trait override checks.
- A first check command selected the older helper in the main checkout, which
  rejected the reviewed `--large-build-entry` option before starting a check. It
  consumed no check and was corrected to the candidate helper.

## Counted game 1: combined build-policy pressure

- Custom map derived from shipped `Empire-Earth.oramap`, not a fixture:
  `analysis/20260815-round06-integration/cycle-1/maps/cnc102-blocked-both.oramap`
- Map SHA-256:
  `e4164c7e8a8746db47c3896521bbc7cf1caba376ce40a4f9cdc8896d3cb4f8c7`
- Seed `606101`; GDI Brutalis versus Nod IronReaper; ordinary AIs with all normal
  modules, unrestricted tech, custom ready-economy starts, headless MAX.
- Canonical launcher staged installed CNC content from
  `/root/github/LibertyDawn/.build/cnc33a/runtime-content`; the isolated support
  directory contained its own `Content` link.
- Passed with exit 0, 6,500 valid world ticks, and 44.037 seconds wall time.
  No fatal error, unhandled exception, or desync signal was observed.
- Combined signals included both nine-goal openings; Brutalis field admission,
  Resonator placement, field completion, low-power coverage loss/recovery, and
  bounded illegal-extension backoff; and IronReaper radar loss, busy/power queue
  contention, one HQ reservation/production, radar restoration, reservation
  release, and later field-production continuation.
- Performance stayed bounded: mean tick 6.216 ms, p95 14 ms, p99 20 ms. The
  1,428.914 ms maximum was a bounded startup/runtime outlier; four freeze-threshold
  samples were not attributed to a product module.
- Evidence:
  `analysis/20260815-round06-integration/cycle-1/games/game1-counted/round06-build-policy-pressure/`.

One earlier launch of this same map reached tick 6,500 and exited 0, but the
harness rejected two fragile Lua `Media.Debug` expectations. It is explicitly
uncounted. The repaired manifest used authoritative engine/module evidence and
the clean rerun above is the sole counted Game 1.

## Counted game 2: HeavyDrop lifecycle and threat pressure

- Distinct custom map derived from shipped `island-duel.oramap`, not a fixture:
  `analysis/20260815-round06-integration/cycle-1/maps/cnc110-passenger-invalid.oramap`
- Map SHA-256:
  `64c62c738a120f38f9f5a04e2de1906de07ec1c741c1fabbfb70ed14054385b3`
- Seed `606102`; GDI Brutalis versus Nod IronReaper; ordinary AIs with all normal
  modules, unrestricted tech, ten-carrier HeavyDrop pressure, headless MAX.
- Canonical installed-content staging and an isolated `SupportDir/Content` link
  were verified by the launcher.
- Passed with exit 0, 3,000 valid world ticks, and 8.040 seconds wall time. No
  fatal error, unhandled exception, or desync signal was observed.
- Brutalis created a ten-pair HeavyDrop, discarded the scripted invalid passenger
  pair, retained/routed the other pairs, rejected unsafe cells under enemy threat,
  returned to safe assembly, released an all-invalidated wave cleanly, and later
  issued threat-aware recovery routes and unload replans.
- Performance stayed bounded: mean tick 1.518 ms, p95 3 ms, p99 5 ms, maximum
  872.806 ms, with three bounded freeze-threshold samples.
- Evidence:
  `analysis/20260815-round06-integration/cycle-1/games/game2/round06-heavy-drop-air-pressure/`.

The factual trace contained 5,601 repeated exact-unload diagnostics while
passengers were logged as not in world. The game remained fast and terminated
cleanly, and the available trace does not distinguish repeated logging from
repeated lifecycle transitions. This is a required cycle-2 discriminator, not a
proven strategic or release defect. CNC-84 air-squad avoidance was not directly
observed; only the shared threat geometry used by transport planning was exercised.

## Fresh native Luna analysis and recommendation dispositions

Each counted game received its own fresh native `gpt-5.6-luna` factual narrator
and a separate fresh native `gpt-5.6-luna` policy reviewer. Analysis was isolated
to copied inputs and policy work was serialized through the canonical scratchpad
resource slot.

### Game 1

- Narrative:
  `analysis/20260815-round06-integration/cycle-1/reviews/game1-narrator/NARRATIVE.md`
- Policy:
  `analysis/20260815-round06-integration/cycle-1/reviews/game1-policy/POLICY-REVIEW.md`
- Verdict: insufficient evidence, high confidence; no narrow release-blocking
  repair required.
- Highest recommendation: run a matched changed/control game with forced legal
  and illegal field placement plus radar loss and bounded queue/actor outcomes.
- Disposition: accepted as required follow-up evidence for cycle 2. Rejected as a
  cycle-1 code or balance change because the two-game cap is complete, the game
  showed bounded progress and recovery, and no duplicated reservation, permanent
  starvation, or failure to restore an eligible radar provider occurred.

### Game 2

- Narrative:
  `analysis/20260815-round06-integration/cycle-1/reviews/game2-narrator/NARRATIVE.md`
- Policy:
  `analysis/20260815-round06-integration/cycle-1/reviews/game2-policy/POLICY-REVIEW.md`
- Verdict: insufficient evidence, medium confidence; no narrow release-blocking
  repair required. Repeated exact-unload output is an unresolved lifecycle,
  logging, or performance concern, not yet a proven strategic defect.
- Highest recommendation: run a matched full-engine discriminator with per-pair
  timestamps/counts for creation, boarding, world removal, unload success/failure,
  and restoration.
- Disposition: accepted as required follow-up evidence for cycle 2. No cycle-1
  code change is justified because lifecycle transitions were not counted
  independently, the mission released cleanly, and measured execution remained
  bounded. Cycle 2 should also force actual air-squad movement through layered
  threat geometry to close the CNC-84 evidence gap.

Policy reviewers produced bounded updated scratchpads in their isolated analysis
directories. The canonical tracked scratchpad was not changed because this worker
was explicitly authorized to update only integration state and receipt.

## Cycle 1 decision

No concrete narrow cumulative defect was established, so no repair branch or
product change was created. Balance and task policy remain unchanged. Cycle 1
passes as bounded integration evidence; the two accepted discriminators above are
the next worker's explicit recommendation carry-forward, not silent omissions.
