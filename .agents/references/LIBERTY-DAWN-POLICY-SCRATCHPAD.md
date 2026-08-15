Game B policy scratchpad — reusable

Evidence standard:
- Module records prove logged identity, routing, waiting, replanning, and
  completion events; do not treat them alone as proof of destruction,
  ownership, world state, or universal completion.
- Distinguish an acceptance-pattern pass from proof of the full terminal
  scenario. State exactly which identities reached recovery completion.
- Performance/load timing is not repair-queue latency unless a queue-specific
  metric is supplied.

Air repair / FIFO policy:
- A sound repair policy assigns damaged aircraft to owned active pads,
  preserves identity while pads are occupied, and uses safe bounded holding
  positions (including AA-aware routes) when capacity is unavailable.
- When a reserved destination becomes unusable, the aircraft must detect the
  stale destination, replan, and continue waiting or route to a surviving
  owned pad. Repeated exclusive use of a surviving pad is valid evidence of
  recovery rather than a stall.
- Recovery completion should be logged per aircraft, followed by explicit
  return/adoption into ordinary Air activity. Do not infer completion for
  identities absent from completion records.

Acceptance and follow-up:
- A configured run may pass when required routing, wait, stale-target,
  surviving-pad, and identity-rejoin patterns are present and no forbidden
  error/interruption occurs.
- For strong end-to-end claims, require direct provider-loss/destruction
  logging plus completion records for every queued aircraft. Missing evidence
  is a required follow-up unless the acceptance contract explicitly excludes
  those terminal assertions; it is not automatically a release blocker.
