# Policy review scratchpad

- Engineer recovery work should preserve valid assignments across save/load,
  honor the existing missing-activity grace, and release only genuinely stale
  or removed-target work for deterministic replanning. This supports Engineers'
  strategic roles without needless incumbent loss. (Evidence-limited to the
  reported recovery scenarios; advisory.)

- Resonators are controlled ecological investments, not automatic economic
  upgrades. Favor a defended, legal site near reachable Tiberium while avoiding
  infantry corridors and dangerous base geometry; the right choice is
  faction-, map-, and situation-dependent. (Design-reference rule; advisory.)

- For rare structure placement, random-cell discovery can create starvation even
  when legal sites exist. Prefer a bounded deterministic or finite candidate
  scan with simple safety/adjacency filters and cooldown-based retries. Confirm
  demand, resources, prerequisites, and queue ownership separately before
  attributing failure to placement. (Hypothesis from an underspecified
  Resonator narrative; requires full-engine validation.)

- Keep balance frozen when diagnosing AI placement. Do not change costs, stats,
  prerequisites, resource values, or probabilities to mask a missing request,
  illegal-site diagnosis, or queue failure. (General guardrail.)
