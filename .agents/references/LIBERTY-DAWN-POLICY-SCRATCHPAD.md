# Game B policy scratchpad — replacement

Evidence standard:
- Authoritative module records establish logged identity, orders, routing,
  waiting, replanning, release, and recovery events only.
- They do not by themselves prove destruction, ownership, damage totals,
  continuous capability, universal completion, or a winner.
- Headless exit and absent Media.Debug output are not battlefield or outcome
  claims.

Observed acceptance evidence:
- Both ordinary Brutalis and IronReaper AIs reached tick 9000 with no logged
  desync, exception, or fatal error.
- Opening construction and smart-economy goals progressed with paid progress;
  five eligible stalled entries were canceled/refunded and harvester progress
  continued through exit.
- Resource-field planning was recorded for both players, but waiting for queue
  or placement is not proof of construction. Harvester counts and replacement
  requests are reported only as sampled snapshots.
- Radar retry/release recovered to a completed goal and restored provider at
  tick 8327 with HQ operational and power 558/435; earlier continuity is not
  proven.
- HeavyDrop released invalid lifecycle pairs, committed safe/assault unload
  orders, completed valid pairs, and restored passengers to ordinary squads.
  Wave 6 completed 10/10; wave 7 completed nine surviving pairs after safe
  holds/returns, while its final 10/10-passenger and 9/10-carrier state did not
  establish another full launch.
- Orcas 47 and 48 logged repair recovery; ordinary air logged threat-aware
  routing, target selection, attack/return, reinforcement, and idle transitions.
  SquadManager and AA-clear/defended decisions were active.

Policy:
- Preserve identity through queueing, retry, safe hold, unload, rejoin, and
  recovery transitions.
- Treat unsafe or unavailable destinations as bounded waits/replans, not silent
  stalls; release invalid lifecycle pairs and refund where logged.
- Report planning, activity, and recovery without converting them into world
  state, damage, ownership, or match-result claims.

Acceptance status:
- Bounded cumulative systems-regression pattern passes with high confidence;
  no release blocker is present. Release readiness does not depend on a natural
  winner. Terminal HeavyDrop coverage remains incomplete and is follow-up
  evidence, not a failure of this contract.

Highest-priority follow-up: add an authoritative terminal per-pair HeavyDrop
disposition record (carrier, passenger, and lifecycle outcome) at exit.
