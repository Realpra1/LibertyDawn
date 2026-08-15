# Round 06 resume integration cycle 5 receipt

## Candidate and decision

- Branch: `agent/round-20260815-bug-polish-06-resume`.
- Assigned clean base/product candidate: `d25bb59ea1d981f2f2948aad84bada4b2e602a6a`.
- Exactly two successful full-engine games count. No concrete product defect was established and no product source changed.
- Balance, policy, tuning, strategy, geometry, thresholds, and retry values remained frozen.
- Cycle 5 is complete; do not start another integration cycle.

## Counted full-engine games

1. **Pad destruction and all-four bounded air-repair disposition** — exact cycle-4 map SHA-256 `18d0efff0b6db0418c0b9ccbd16c94ab15761c3d4ad1c8ab96b351c7e560fa32`, seed `606402`. Passed: exit 0, 8,000 ticks, 20.022 seconds. The setup unconditionally destroyed named pad A. Orca 45 authoritatively detected the unavailable destination, replanned, completed, and rejoined; Orca 46 moved from wait to completion and attack rejoin; Orca 44 claimed the surviving pad and resumed ordinary target routes; Orca 47 remained in an identity-linked safe wait/retry state. Full drain is not claimed.
2. **Final cumulative sustained regression** — map SHA-256 `bc855850c4c6333d59295d325442886b883ccb836f225c4b59cc7c42b6a6606b`, seed `606502`. Passed: exit 0, 9,000 ticks, 22.036 seconds. Authoritative module logs covered protected opening, smart economy, paid queue progress/cancel-refund recovery, field planning, radar restoration, specialist/combat activity, Orca repair completion, repeated exact HeavyDrop unload/completion, invalid lifecycle release, and nine-survivor ordinary handoff. The final newly assembling wave remained explicitly pending; no winner is claimed.

Both used ordinary Brutalis/IronReaper AIs, all normal modules, unrestricted tech, the canonical launcher with installed CNC content, canonical global game slots, isolated support/artifact directories, and completed below 120 seconds without fatal, exception, or desync signals.

## Independent analysis and recommendation dispositions

- Game A narrator/policy: `analysis/20260815-round06-integration/cycle-5/reviews/game-a-narrator/NARRATIVE.md` and `game-a-policy/POLICY-REVIEW.md` — high-confidence acceptance-pattern pass, no blocker. Extra provider-loss/terminal instrumentation is rejected as a code change and accepted as a reporting boundary; no universal recovery claim is made.
- Game B narrator/policy: `analysis/20260815-round06-integration/cycle-5/reviews/game-b-narrator/NARRATIVE.md` and `game-b-policy/POLICY-REVIEW.md` — high-confidence qualified pass, no blocker. Full terminal disposition for a newly assembling final wave is advisory, not a bounded-release requirement; completed and invalid prior pairs are already identity linked.

All four roles were fresh native `gpt-5.6-luna` medium agents. Scratchpad replacements were serialized, bounded, validated, and promoted.

## Invalid launches and checks

- Uncounted: one pre-start lock-namespace rejection; six exit-0 Game-A harness iterations; two exit-0 Game-B harness iterations. They were rejected for missing literal evidence and revealed no product defect.
- Focused AirThreatGeometry/AirRepairCapacity/TiberiumField/RadarRecovery tests: 109 passed, 0 failed, 0 skipped.
- Protected `make check`: exit 0, 0 warnings, 0 errors, interface checks passed.
- `git diff --check`: pass before receipt update.

Cycle 5 passes. Commit this receipt once, obtain a fresh native Terra-medium final release review, and publish only if ready. Never merge `bleed`.
