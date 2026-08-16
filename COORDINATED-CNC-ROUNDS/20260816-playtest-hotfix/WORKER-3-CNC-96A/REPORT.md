# CNC-96A cycle report

## Outcome

- Proposed status: Complete - testing
- Product change: configure the CNC Stealth Tank specialist to claim every
  eligible owned `stnk`, make dynamic reservation cover newly produced and
  ownership-transferred tanks before the next strategic scan, select all
  eligible tanks during rebalance, and reject configurations exceeding four
  specialist groups.
- Preserved behavior: existing deterministic group routing, targeting, repair,
  no-repair fallback, partner-loss retention, replacement/reformation, and
  failsafe release paths are unchanged.
- Balance remains frozen. No costs, unit statistics, production policy, target
  scoring, or squad sizing was changed.

## Diagnosis

- The reviewed manager intentionally selected only roughly half of eligible
  Stealth Tanks, leaving the rest for ordinary armies. `IBotUnitReservations`
  also recognized only actors already present in the manager's `reserved` set,
  so a newly produced or ownership-transferred tank could be observed first by
  another bot owner before the 75-tick strategic rebalance.
- The fix keeps the old half-preserving policy as the default for other
  specialist profiles and enables `ClaimAllEligible` only for CNC's Stealth Tank
  manager. Dynamic eligibility reserves a new owned live `stnk` immediately;
  rebalance then claims every distinct eligible actor into one deterministic
  group. The configured number of harassment plus optional attack groups is
  ruleset-validated against the hard ceiling of four.

## Checks

- `dotnet test OpenRA.Test/OpenRA.Test.csproj --no-restore --filter
  FullyQualifiedName~StealthTankSquadPolicyTest`: 87/87 passed.
- Relevant passing cases include claim-all distinct selection/new discovery,
  four-squad topology, repair/no-repair disposition, partner death retention,
  compatible replacement, duplicate-free reformation, and save/load rebalance.
- `make test`: Release build succeeded with 0 warnings/errors; full CNC MiniYAML,
  sequences, and map validation passed.
- Both custom maps passed `utility.sh cnc --check-yaml` before launch.
- `git diff --check`: clean.

## Full-engine games

Exactly two valid ordinary-AI/all-module custom games were run. Earlier fixture
attempts are preserved under `analysis/games/batch` and `analysis/games/valid`;
they are invalid/uncounted because fixture-only Lua failures prevented a valid
bounded result.

1. Produced claim/repair pressure, seed 96031, Brutalis versus SkyNet:
   `/root/github/.build/coordinated-cnc/20260816-playtest-hotfix/WORKER-3-CNC-96A/analysis/games/valid2/produced-claim`.
   The engine exited 0 at configured world tick 2500 with no OOS/exception.
   Twelve `stnk` were factory-produced by tick 577. The specialist snapshot
   reports `total=12 reserved=12 groups=5/5/2 ordinary=0`, repeated hazard-routed
   harass/attack targeting, and no recipient ordinary adoption/recruitment of an
   `stnk`.
2. Captured/ownership-transfer four-squad pressure, seed 96032, Brutalis versus
   SkyNet:
   `/root/github/.build/coordinated-cnc/20260816-playtest-hotfix/WORKER-3-CNC-96A/analysis/games/valid2/captured-four-squads`.
   The engine exited 0 at configured world tick 2500 with no OOS/exception.
   Twelve donor-owned `stnk` transferred individually by tick 446; at tick 701
   all twelve were live and the recipient specialist snapshot reports
   `total=12 reserved=12 groups=4/3/3/2 ordinary=0`. Donor-side ordinary adoption
   preceded transfer; no recipient-side ordinary adoption/recruitment contains
   an `stnk`. Four groups route and target under pressure.

The launcher summaries label both valid runs `failed` only because fixture
assertions expected tick 2200 instead of observed tick 2201 and explicit repair
log markers that did not occur. Engine completion and the claim/group assertions
passed. No third game was run because the assignment authorizes exactly two.

## Fresh narration and policy review

- Game 1 narrative:
  `/root/github/.build/coordinated-cnc/20260816-playtest-hotfix/WORKER-3-CNC-96A/analysis/reviews/game1-commenter/NARRATIVE.md`
- Game 1 policy:
  `/root/github/.build/coordinated-cnc/20260816-playtest-hotfix/WORKER-3-CNC-96A/analysis/reviews/game1-policy/POLICY-REVIEW.md`
- Game 2 narrative:
  `/root/github/.build/coordinated-cnc/20260816-playtest-hotfix/WORKER-3-CNC-96A/analysis/reviews/game2-commenter/NARRATIVE.md`
- Game 2 policy:
  `/root/github/.build/coordinated-cnc/20260816-playtest-hotfix/WORKER-3-CNC-96A/analysis/reviews/game2-policy/POLICY-REVIEW.md`

Both policy reviewers classify missing explicit repair/reinforcement/no-repair
game events as required evidence follow-up, not a demonstrated regression or
balance issue. Disposition: no third game is allowed in this one-cycle exact-two
contract. The changed code does not touch these behaviors, and all focused
repair/no-repair and reinforcement/reformation lifecycle tests pass; the final
Terra review is asked to gate this explicit scope/evidence disposition.

## Review

- Fresh Terra-medium final review: pending.
