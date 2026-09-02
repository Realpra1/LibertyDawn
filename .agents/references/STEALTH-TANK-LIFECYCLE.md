# Liberty Dawn stealth-tank lifecycle

This is the authoritative policy lifecycle for AI-controlled stealth tanks. It
supplements `LIBERTY-DAWN-DESIGN.md`; neither reference replaces the other.

## 1. Start

A stealth tank enters the lifecycle when it is built or finishes repair.
Preserve the established start behavior.

## 2. Squad construction

Assign an unassigned tank to a squad and route reinforcements safely. A
reinforcement is excluded from the active squad-center calculation until it
arrives at or next to the current strategic cell. When there is no squad, the
tank may become the new center and remake one.

## 3. Target acquisition

From the active squad center, scan outward toward enemies with the existing
A*-like directed strategic-cache search until ten unique target cells have been
found. Keep the incumbent target among the options. Bound the scan by about 30
seconds of estimated squad travel/search cost, or by another CPU-safe limit.

Preserve the proven scan. If fewer than ten cells are found, including when the
squad begins far from enemies or at its base, move the squad closer to enemies
and scan again.

## 4. Target choosing

1. Preserve the existing priority, value, and remaining-HP semantics,
   approximately `priority * value / HP%`. Sort target cells by that value and
   reject the lower half. Handle a single option without rejecting it.
2. From the remaining cells, reject the higher-threat/crossover half using the
   existing threat functions.
3. Rank the survivors with the existing threat-weighted distance and detection
   rules. Prefer separation from other stealth squads to encourage multi-angle
   harassment and shorter opportunities, and choose the least-close surviving
   option.

4D. Do not reissue move orders constantly, but let the engine's default pathing
work.

Target-cell filtering selects where to engage; it must not alter routing.

## 5. Engagement

Engagement is live local work. It must use current actor positions and current
threat facts, never the strategic cache. The active behavior owns the squad
until that behavior explicitly completes or hands control to another lifecycle
stage. Outside timers and events must not bypass the active owner.

- At an undefended cell, attack the highest-priority actors first.
- At a defended cell, crush only when infantry and detection safety permit;
  otherwise kite.
- If neither is safe, mass attack only while crossover is greater than 2, until
  the package is cleared or crossover reaches 1. If mass attack is unavailable
  and crossover is below 2, recalculate strategically.
- A mass attack targets the highest threat first.
- Kiting targets the actor closest to the squad first and fires from a safe cell
  outside enemy range.
- Detectors are route dangers before decloak unless unguarded. Every planned
  decloak or attack must pass safety and crossover approval.
- Detection rejects crushing in kite and mass-attack modes.
- Obelisks remain passable while the squad is cloaked. Never decloak or attack
  unsafely near an Obelisk.
- Once live combat decides to flee, use the cached strategic threat map to move
  roughly two strategic cells along the safest outward route. Flee routing is
  strategic work; do not build a large live tactical candidate grid.

Use one shared squad action. Design the decision so a one-tank squad behaves
exactly; larger squads use the same order and may absorb formation error. Do not
pre-plan tactical phase sequences. Each owner chooses its current safe action
from one current live snapshot, retains only the minimum intent needed to avoid
order churn, and yields explicitly when its job is done.

Normal strategic recalculation may occur after arrival, a kill, an emptied cell,
or mission completion. It must not continuously interrupt active local combat.

## 6. Damage

Route damaged tanks safely to repair options. If no repair option is safe, keep
them active in the fight. After repair, return to step 1.

## Preservation boundary

Save squad membership/allocation only. On load, preserve those allocations and
restart each squad at TargetAcquisition; tactical behavior state, phase plans,
fingerprints, timers, and pending orders are deliberately not serialized.

Keep public configuration, diagnostic-only cadence output, watchdogs, target
priorities, cloak and detector safety, and existing balance unchanged. Keep each
lifecycle owner short, modular, and independently testable. A behavior class
should remain below 400 lines; a supporting class must not exceed 500 lines.
