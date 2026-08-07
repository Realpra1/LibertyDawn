# PR #79 review — CNC-43A Flame Tank balance

- Verdict: `ready with one fix`
- Required fix: complete the natural-conclusion acceptance record with a named outcome and terminal state, then obtain fresh Commenter and Policy Reviewer judgments on that corrected evidence.
- Reviewed PR head: `f584f56f12915d650bb3739cb39bfd31ee8a373a`
- Product head: `6f3a33ea165e0b4b90d0e4a9c974b70a12f78a12`
- Control: `09ccdac3c1ecb5134a4751f2bcbd8a7970dfe6bf`

## Finding

### Major — the natural-match evidence omits the required outcome and terminal army/economy state

Location: `COORDINATED-CNC-ROUNDS/20260806-bug-polish-01/WORKER-4-CNC-43A/REPORT.md:96` (especially lines 100–103 and 127–129); affected contract: `WORKER-4-CNC-43A/STATE.md:428-435` and review-role clauses 7, 10, and 12.

The report says both games reached natural game over, but explicitly says the staged artifacts do not identify the winning side and defers winner/defeat-tick and terminal economy/force/scoreboard evidence. The independent narrative confirms that natural termination is known but the winner, defeat event, final actor/value tables, and final economic state are unknown (`analysis/worker-4-cnc-43a/natural-v1/commenter/NARRATIVE.md:5-13`). Its Policy Reviewer consequently returned `insufficient evidence` with high confidence and made terminal outcome/state capture its highest-priority recommendation (`analysis/worker-4-cnc-43a/natural-v1/policy/POLICY-REVIEW.md:3-8,35-40`).

This is not optional policy telemetry under the worker contract: the natural-conclusion clause explicitly requires the outcome and army/economy values. Without them, the changed run's 203 FTNK creations, 162 losses, and much longer duration cannot distinguish a successful sustained assault from a loss, stalemate, or harmful replacement stream. The focused literal and counter harnesses establish the requested numeric change and preserved counters, but they do not fill this ordinary-match acceptance gap.

Smallest safe correction: extract and verify the named winner/loser, defeat tick, and terminal army/economy/force values from the existing matched changed/control logs or replays if those artifacts contain them; otherwise rerun one matched natural-conclusion pair with bounded terminal capture. Keep the existing seed/map/bot/content identities (or record replacements), require natural FTNK production/contact, stage the corrected evidence to a fresh Terra Commenter and fresh Policy Reviewer, and update the report/state with their adopted or rejected conclusions. No product-code change is indicated.

## Other verification

No additional release-blocking defect was found. The PR product diff is restricted to FTNK HP `30000 -> 36000` and seven BigFlamer-local Heavy modifiers `20 -> 22`. Saved final resolved output differs from control only in those values; `^FlametankExplode`, Flamethrower, Chemspray, BigChem, and Napalm remain byte-identical. The final literal pair records 36,000 HP, 5,544 versus 5,040 Heavy burst damage, exact non-Heavy and death-explosion parity, matched timing, and clean exits. True-tank and defense/air evidence preserves the required counters. The change adds no simulation hot-path work, `git diff --check` passes, the recorded final `make test` is green with zero warnings/errors, and GitHub reports the PR mergeable/clean with no configured check rollup.
