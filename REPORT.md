# CNC-96A release-candidate report

The lifecycle is now a KISS state machine with one short owner per authority
stage. Strategic acquisition/approach use the cache; all local combat and escape
decisions use current actor positions and the standard threat calculator.
Tactical save persistence and speculative Kite/MassAttack plans were removed.

Validation is green: 865/865 full .NET tests, 14/14 runner tests, the bounded
four-squad scenario, and five of five natural VIKI-versus-two-Brutalis games.
No final game emitted a 30-second stall or Obelisk-death failure. Every active
terminal squad passed cadence in the natural games.

Final natural primary efficiency mean/median: 1923.69 / 1955.57. Final
damage-adjusted median: 0.589. The special scenario won at tick 6187 with all
eight tanks alive, primary 1101.91, damage-adjusted 5.754.

Detailed scope and durable evidence: [CNC-96A-STABLE-HOTFIX.md](CNC-96A-STABLE-HOTFIX.md).
