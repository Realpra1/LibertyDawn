# Round 06 resume integration cycle 1 receipt

## Candidate and checks

- Branch: `agent/round-20260815-bug-polish-06-resume`
- Exact tested head: `5e74c362ba95986b422516bda074128262355081`
- Common base and all seven supplied reviewed heads: verified ancestors.
- `git diff --check`: pass.
- Protected `make check`: pass, 0 warnings and 0 errors; interface and conditional
  trait-override gates passed.
- Product/balance changes: none.

## Full-engine receipt

Exactly two successful games count. Both used deliberately constructed custom CNC
maps, ordinary Brutalis/IronReaper AIs, unrestricted tech and all normal modules,
the canonical launcher with the installed-content argument, isolated support and
artifact directories, and verified `SupportDir/Content` links. Both were capped
below 120 seconds.

1. **Combined build-policy pressure** — seed `606101`, map SHA-256
   `e4164c7e8a8746db47c3896521bbc7cf1caba376ce40a4f9cdc8896d3cb4f8c7`.
   Passed: exit 0, 6,500 valid ticks, 44.037 seconds. Field/Resonator progress,
   queue/power contention, radar loss/reservation/production/restoration, and
   continued AI activity were observed without fatal error or desync.
2. **HeavyDrop lifecycle and threat pressure** — seed `606102`, map SHA-256
   `64c62c738a120f38f9f5a04e2de1906de07ec1c741c1fabbfb70ed14054385b3`.
   Passed: exit 0, 3,000 valid ticks, 8.040 seconds. Invalid-pair cleanup,
   threat-aware safe routing/replanning, wave release, and later transport rescue
   behavior were observed without fatal error or desync.

An earlier Game-1 attempt exited 0 at tick 6,500 but failed two fragile harness
log expectations. It is invalid and uncounted; the expectations were repaired and
the scenario was rerun cleanly. A first protected-check command also failed before
starting because it selected an older helper interface; it consumed no check.

Raw maps, logs, benchmarks, replays, and analysis remain untracked under
`analysis/20260815-round06-integration/cycle-1/`. The concise durable state records
their exact paths and hashes.

## Independent analysis receipt

- Game 1 Luna narrator:
  `analysis/20260815-round06-integration/cycle-1/reviews/game1-narrator/NARRATIVE.md`
- Game 1 separate Luna policy review:
  `analysis/20260815-round06-integration/cycle-1/reviews/game1-policy/POLICY-REVIEW.md`
- Game 2 Luna narrator:
  `analysis/20260815-round06-integration/cycle-1/reviews/game2-narrator/NARRATIVE.md`
- Game 2 separate Luna policy review:
  `analysis/20260815-round06-integration/cycle-1/reviews/game2-policy/POLICY-REVIEW.md`

Both policy verdicts were `insufficient evidence`, reflecting absent matched
controls/natural outcomes rather than a launch or release blocker. Both explicitly
found no narrow release-blocking repair required now.

Recommendations are explicitly carried forward to cycle 2:

- matched field-placement/radar-loss queue contention with actor-level outcomes;
- matched per-pair HeavyDrop lifecycle timestamps and transition counts; and
- a forced air-squad traversal across layered threat geometry for direct CNC-84
  coverage.

No recommendation was used to change balance or broaden task policy.

## Handoff

Integration cycle 1 is complete with exactly two counted games. No repair branch
was needed. Stop here: do not infer that cycle 2 has begun, and do not push or
merge `bleed`.
