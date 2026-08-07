# Review 1 — CNC-39 PR #83

## Verdict

**ready with one fix**

`required_fix`: Cancel the released surplus Engineer's existing capture activity
when a paired building becomes solo-capturable, and prove the same released actor
stops or receives a distinct replacement order in a full-engine threshold-crossing
regression.

## Scope and evidence checked

- PR #83 head `53874e4328b8f00ff691d591625d5f548ed1b551` against assigned
  base `09ccdac3c1ecb5134a4751f2bcbd8a7970dfe6bf`; one product commit and
  four task-scoped files.
- Assigned worker state/spec, task report, implementation and surrounding capture
  code, focused tests, cycle journal, game summaries/logs, final gates, and current
  GitHub checks. Linux and Windows CI are green at the reviewed head.
- Required dependency PR #80 at
  `937ef0204870ff2eca39c413af7431adb279c082`, limited to its commits and
  product diff. The overlap is in `CaptureTargeting.cs`,
  `CaptureManagerBotModule.cs`, and `CaptureTargetingTest.cs`.

The exact HP boundary, CNC-only rule ownership, transformed husk value, strict
retarget margin, worse-member pair score, distinct-solo allocation, bounded
approach check, deterministic ordering, transport precedence, and compact capture
save data are otherwise consistent with the assigned behavior and recorded green
evidence. The first changed-policy behavioral test used ordinary full-engine AI
with a matched same-build threshold-50 control. The final focused/full tests,
CNC validation, builds, differential games, natural Empire endurance, fixed-horizon
performance comparison, final regression, and published CI are recorded green.

## Finding

### High — bookkeeping-only surplus release leaves the old `CaptureActor` running

- **Code:**
  `OpenRA.Mods.Common/Traits/BotModules/CaptureManagerBotModule.cs:338`.
  Lines 340–347 remove every surplus pair member from `activeCapturers` but do not
  queue `Stop` or a replacement order. That actor is still present in the earlier
  `capturers` snapshot. When the solo target is reserved by the retained member and
  no distinct solo target exists, lines 276–285 cannot stop the surplus because
  `activeCapturers.Remove(capturer.Actor)` now returns false. Its pre-existing
  `CaptureActor` activity therefore continues toward the same now-solo target.
- **Observed evidence:** in
  `analysis/worker-1-cnc-39/games/postcycle7-crossing-extended-husk-01/damage-repair-crossing-extended/support/Logs/debug.log:13`,
  `e6#13` is declared surplus-released. Line 14 immediately rejects that same target
  as reserved by `e6#12`, but no Stop is issued to `e6#13`; the only Stop at line 16
  is for the separate former solo assignee `e6#14`. `e6#13` becomes available only
  after `e6#12` completes the capture and is then paired with `e6#14` at line 22.
  The passing manifest at
  `analysis/worker-1-cnc-39/games/postcycle7-crossing-extended-husk-01/manifest.json:25`
  accepts unrelated wildcard actor IDs for “surplus released” and “stopped,” so it
  does not verify that the released member actually relinquished the order.
- **Failure mechanism:** two specialists remain committed and travel toward a
  51–80-percent building even though only one is needed. The stale surplus can be
  unavailable for a better target until ownership changes and the old activity
  cancels. This is exactly the forbidden duplicate reservation/stranding behavior,
  despite the internal dictionary and diagnostic claiming release.
- **Affected contract:** “If an above-80-percent target becomes solo-capturable,
  release the surplus Engineer deterministically without allowing both specialists
  to reacquire the same solo target”; the damage/repair crossing must make a
  coherent transition by the next 125-tick review; and a 51–80-percent building
  must not consume, reserve, or strand two Engineers.
- **Smallest safe correction:** make the pair-to-solo transition issue `Stop` for
  each removed surplus member before ordinary allocation, while allowing a later
  same-scan distinct replacement order to supersede that Stop. Add a full-engine
  ordinary-AI case with a healthy pair falling to exact 80 percent and no other
  eligible target; bind the released ActorID to evidence that it stops/ceases
  approach while only the retained ActorID captures. Rerun the affected crossing
  and final acceptance/regression evidence.

## PR #80 integration verification

Integration is feasible but cannot take either overlapping file wholesale. Keep
PR #80's `SpecialistTargetReservations` purpose/cardinality API, demolition safety,
progress/defer state, and broader save schema (`CaptureManagerCaptureScanTicks`,
`CaptureManagerDemolitionScanTicks`, combined capture/demolition assignments, and
deferred targets). Port PR #83's exact HP/MaxHP boundary, pair reassessment,
distinct allocation, approach reachability, deterministic solo pre-reservation,
capture-side transport cleanup precedence, and bounded diagnostics into that
model.

Every CNC-39 pair retain/retarget/dissolve/surplus transition must update PR #80's
shared reservations atomically. In particular, the required surplus fix must call
`targetReservations.Release(surplusId)` and convert the surviving capture
assignment's saved claimant cardinality from two to one when the target becomes
solo-eligible; otherwise PR #80's restore validation can drop the incomplete
two-claimant record. Preserve PR #80's demolition fields and scan state instead of
PR #83's compact capture-only save keys. No competing shared reservation API is
needed.

## Required fix summary

Stop the actual surplus pair member, test the ActorID-correlated transition under
ordinary full-engine AI, and carry that Stop/release/cardinality transition through
PR #80's shared reservation and save model during cumulative integration.
