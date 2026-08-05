# CNC-28: Stealth Chemical Tanks

- Status: complete
- Cycles used: 10 of 30
- Branch: `agent/cnc28-stealth-chem`
- Base: `origin/agent/cnc27-stealth-squads`
- Draft PR: https://github.com/Realpra1/LibertyDawn/pull/55

## Literal acceptance

Every `ctnk` chemical tank must intrinsically use the same cloak timing and detection model as `stnk`: it cloaks after the configured initial/recloak delay without a crate or stealth generator, attack/damage events reveal it through the shared `UncloakOn` configuration, critical health pauses cloak, and enemy detectors reveal it through the ordinary cloak-type mechanism. Existing health, armor, speed, weapons, prerequisites, target types, death/husk behavior, AI weights, and squad ownership remain unchanged.

Forbidden outcomes include engine or AI behavior for this config-only task, duplicate active cloak traits, external-source dependence, detector immunity, firing while cloaked, stealth-tank changes, chemical balance changes, shared-default changes, or new nondeterministic/save state.

## Implementation

- Replaced `CTNK`'s crate-only cloak inheritance with the same actor-local `^Cloakable` inheritance used by `STNK`.
- Removed only the inherited `Cloak.RequiresCondition` and stealth-generator receiver, making cloak intrinsic.
- Reused the shared 75-tick initial/recloak timing, `Attack, Unload, Damage` reveal events, critical-health pause, sounds, targetability, and detector cloak type.
- Made no engine, AI, squad, weapon, health, armor, economic, build, repair, death, or husk changes.

## Cycles

1. Audited resolved inheritance and contention, applied the four-line actor-local config, restored the worktree, and passed strict build plus full CNC YAML validation.
2. Live lifecycle fixture proved delayed intrinsic cloak, reveal on firing, recloak, reveal on damage, and detector-enabled damage.
3. Strengthened detector control proved zero hits while cloaked without a detector, hits after adding a valid detector, and captured-husk restoration to a cloaked `ctnk`.
4. First clean adversarial pass: destroying a nearby stealth generator did not affect intrinsic cloak; critical health held cloak off and healing restored it after the normal delay.
5. Golden-rule fixture failed before behavior testing because its map-local control subtype lacked an explicit sprite image; the harness was corrected without product changes.
6. Corrected equal-force fixture produced an 8-to-0 updated victory.
7. Repeated the comparison at immediate sight distance to remove long-range fog as a confounder; updated tanks again won 8-to-0.
8. Added direct cloak-state instrumentation, but identical-title map package caching made that new line ambiguous; existing combat result remained 8-to-0 and was not used as final state evidence.
9. Second clean adversarial pass: a full VIKI-versus-Brutalis headless MAX match ran to natural game over. Normal AI, large late-game production, chemical-tank combat/death husks, and engineer recapture all remained active with no new fatal/Lua/unhandled error.
10. Third clean adversarial pass under a unique map identity explicitly recorded `updated-cloaked=true` and `visible-control-cloaked=false`; the updated side ended 7-to-0 after starting with eight tanks each.

Ignored raw evidence and fixture packages are under `AUTONOMOUS-CNC-LOGS/CNC-28/`.

## Validation

- Strict Debug solution build: passed with zero warnings and errors.
- Unit tests: 314/314 passed.
- Full CNC YAML/map validator: passed.
- Live cloak lifecycle: intrinsic delay, attack/damage reveal, recloak, critical pause/recovery, external-generator independence, detector control, and husk restoration passed.
- Golden rule: updated behavior decisively defeated an equal old-behavior control, 7 survivors to 0.
- Full real match: natural MAX game completion with normal AIs and late-game chemical-husk lifecycle; no new fatal, unhandled, or Lua error.
- The recurring map-cache warning names the user's pre-existing invalid `TibTest.oramap`; it is not the loaded CNC-28 fixture.
- GitHub implementation/report head `34009d7c79`: Linux passed in 3m16s and Windows passed in 4m25s; PR #55 is mergeable.

## Deferred boundary

CNC-28 deliberately leaves chemical tanks in ordinary ground armies. The separately specified CNC-29 task owns chemical harassment squads; general threat caching and unit-versus-unit calculations also remain separate future tasks.
