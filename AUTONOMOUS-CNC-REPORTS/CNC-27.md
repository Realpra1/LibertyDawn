# CNC-27: Stealth-Tank Squads

- Status: complete
- Cycles used: 15 of 30
- Branch: `agent/cnc27-stealth-squads`
- Base: `origin/agent/cnc26b-branch-config`
- Draft PR: https://github.com/Realpra1/LibertyDawn/pull/54

## Literal acceptance

When an AI owns at least two stealth tanks, a deterministic specialist manager must reserve roughly half for no more than three small squads and leave the remainder available to the existing ground armies. Harassment squads prioritize exposed harvesters and advanced power, safely crush isolated infantry, avoid live weapons and stealth detectors, and unlock high construction-yard priority only after growing beyond their opening pair. A cooperative attack pair prioritizes and out-ranges isolated tanks. Dead, captured, transported, and otherwise ineligible units must be released without conflicting orders or a stalled manager.

Forbidden outcomes include treating stealth tanks as aircraft, reserving every tank, importing aircraft ammo/repair assumptions, repeatedly scanning the world per tank or per tick, crossing a known live threat for a juicy target, ignoring late-built defenses, making a nonexistent weapon/detector dangerous through its safety margin, changing native air behavior, or leaving ordinary and specialist managers fighting over the same actor.

## Implementation

- Added a separate configurable `StealthTankSquadBotModule`; no aircraft state or routing code was overloaded.
- Added the generic `IBotUnitReservations` seam to the ordinary squad manager. Reserved actors are removed from existing squads and excluded from idle/new-unit recruitment, while released actors become eligible again.
- Reserve zero of one tank, both of the first pair, then approximately half. Up to two harassment groups are formed before the final two reserved tanks become the cooperative anti-tank group. Initial reservation occurs on the first bot tick.
- Harassment uses configured actor priorities, distance and economic value. Harvesters and advanced power are primary; construction yards use a separate late-group priority. Isolated infantry receives grouped crush movement.
- The attack group considers only tank targets. It can kite a target whose live weapon range is shorter than the stealth tank's range, and otherwise requires the configured five-times local economic overmatch before a defended attack is allowed.
- One shared enemy/threat snapshot is built per bot every 75 ticks. Every live armed actor and detector participates, including actors built late in the match. Harassment rejection short-circuits on its first real threat to control cost; the attack group sums defenders for its overmatch decision.
- Range margins apply only when the underlying weapon or detector exists. This preserves kiting and avoids inventing a detector on every armed actor.
- Added rate-limited reservation, target, and blocked-decision logging, including the rejected target and strongest blocker.
- Removed the pre-existing erroneous `stnk` entries from Brutalis and Wavemaker aircraft-type lists so their unreserved halves join ordinary ground armies.
- Added pure policy coverage for reservation counts, three-group allocation, scoring, overmatch, and zero-capability range buffering.

## Cycles

1. Initial implementation reached a strict zero-warning build after correcting three precedence/style diagnostics; policy tests and YAML passed.
2. A natural VIKI-versus-Brutalis match ended before VIKI naturally produced stealth tanks, so it was retained as supporting but non-exercising evidence.
3. The first valid injected fixture exposed that weapons were tested against only `Vehicle`, while normal ground weapons declare `Ground`; obelisks and tanks appeared harmless. The raw failed log is retained.
4. The ground-target fix correctly rejected every candidate, revealing that the fixture's 16-cell mobile-HQ detector also covered the supposedly exposed targets. The harness was corrected and no product conclusion was drawn.
5. Corrected fixture proved the 6/6 specialist/ordinary split and guarded-target rejection. It also exposed premature long-range fact selection and an anti-tank fixture whose two targets mutually covered one another.
6. Late construction-yard priority and separated targets improved selection. Review then found the randomized first reservation left a 75-tick ordinary-manager contention window.
7. Immediate reservation and a focused natural match proved harassment, but the anti-tank pair still rejected isolated shorter-range tanks.
8. Blocker diagnostics identified the target tank itself. The cause was an unconditional detector margin that converted raw zero range into a fake two-cell detector.
9. Clean adversarial natural match after the zero-capability fix: the pair killed medium then light armor, while harassment cleared exposed economic targets.
10. Clean guarded-bait natural match: live mobile-HQ and gun coverage blocked both juicy harvesters, and one-tank/two-tank production transitions recovered correctly.
11. Clean seven-minute lifecycle stress: one tank stayed ordinary, two formed a pair, capture released the survivor, reinforcements formed two specialists plus one ordinary, and death recruited the remaining pair. The surrounding match stayed stable through 50,000 ticks.
12. Final review found that creation-order truncation could omit late defenses. The first strict rebuild rejected only a missing blank line before a comment; both issues were corrected before behavior testing resumed.
13. First clean post-fix adversarial pass: all-threat guarded-bait match explicitly blocked on a late-created obelisk and ended naturally at tick 40,000.
14. Second clean post-fix adversarial pass: isolated medium/light tanks were killed in sequence, harassment stayed separate, and the match ended naturally.
15. Third clean post-fix adversarial pass: the full capture/death/reinforcement reservation sequence repeated exactly with no fatal, Lua, or order-contention error.

Ignored raw evidence and fixture maps are under `AUTONOMOUS-CNC-LOGS/CNC-27/`.

## Validation

- Strict Debug solution build: passed with zero warnings and errors.
- Unit tests: 314/314 passed.
- Explicit and conditional trait-interface checks: passed.
- Full CNC YAML/map validator: passed in 20.7 seconds.
- Final adversarial gate: three consecutive post-fix passes (cycles 13-15), including two natural game completions and one focused lifecycle regression.
- Real-game errors: no new fatal, unhandled, or Lua error in the final clean cycles. The recurring map-cache warning names the user's pre-existing invalid `TibTest.oramap`, not the loaded test fixture.
- GitHub implementation head `bab0eb56e4`: Linux passed in 3m17s and Windows passed in 3m59s; PR #54 is mergeable.

## Deferred boundary

This task deliberately does not implement a shared general ground-squad framework or a cached unit-versus-unit damage calculator. Those are separately specified future tasks. Threat evaluation here is periodic and local to stealth behavior so CNC-27 does not pre-empt those broader designs.
