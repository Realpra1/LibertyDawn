# Stealth lifecycle policy scratchpad

Evidence standard:
- Planner and watchdog records establish lifecycle decisions, routing, waiting,
  rejection, reacquisition, and recovery events; they do not alone prove that a
  death was caused by an unsafe issued order or establish a match winner.

Policy:
- With a sparse frontier, bounded adjacent-safe movement toward enemies is
  sensible while fewer than ten target cells are available.
- Repeated fallback must yield when target depletion or a route change produces a
  valid safe mission; retain that route while its target and safety remain valid.
- Detector danger remains a route and decloak gate. A `no-safe-firing-cell`
  rejection is evidence of lifecycle conformance, not evidence that all nearby
  combat outcomes are safe.
- Stationary deaths under detector pressure are a durable follow-up signal, but
  classify them as policy weakness (B) unless an order, decloak, or route trace
  proves a lifecycle safety violation (A).
- Explicit ordinary approvals with detector-safe firing/post-attack routes and
  negative-overmatch crossover rejections are strong evidence that attack gates
  are being applied; a split-angle test is still needed to show they do not hide
  a reachable safe flank.

Validation limit:
- Distinguish B from A with adversarial detector-pressure games logging each
  attack/decloak, safe cell, route, crossover, and detector state.
- A targetless cadence miss or Resupply:Active stationary watchdog is B while
  target=none and reachable-enemy=false, absent an unsafe order or causal death.
  Reclassify only when a reachable safe mission is missed or a lifecycle gate is
  violated. After target depletion, bounded adjacent-safe movement and rescanning
  are sensible, but eventual recovery still needs observation.
