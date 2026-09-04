# Liberty Dawn stealth-tank lifecycle

This is the authoritative policy lifecycle for AI-controlled stealth tanks. It
supplements `LIBERTY-DAWN-DESIGN.md`; neither reference replaces the other.

Implement every phase as a short, modular, self-contained owner. Only the
current phase may retain control or explicitly yield to another phase. Random
timers and outside events must not bypass the active owner.

## 1. Start

A stealth tank enters the lifecycle when it is built or finishes repair.

## 2. Squad construction

Assign an unassigned tank to a squad and route reinforcements safely. Exclude a
reinforcement from the active squad-center calculation until it arrives at or
next to the squad's current strategic cell. When no squad exists, the tank may
become a new squad center and remake one.

## 3. Target acquisition

Scan outward toward enemies from a weighted average of the active squad center
and the squad's assigned map-corner bias. Use the directed A*-like strategic
cache search until ten unique strategic target-cell options have been found.
The squad position has weight three and the corner has weight one. Each squad
has a stable bias corner to encourage separation.

Always retain the incumbent target among the options. Bound discovery to about
30 seconds of estimated squad travel and a CPU-safe operation limit. Fewer than
ten usable targets are sufficient for phase 4. If no usable target is within the
bounded search, move closer to an enemy using the strategic cache and scan again.

## 4. Target choosing

### 4A. Value

Sort target cells by strategic value and remaining HP, preserving the proven
configured priority/value/HP semantics. Retain the higher-value half, rounding
up so that a single option is never rejected.

### 4B. Threat

From the remaining options, retain the lower-threat/crossover half, again
rounding up. Use the standard threat calculator.

### 4C. Distance

From the remaining quarter, choose the least-threat-closest option using the
existing threat-weighted distance and stealth-detection route costs.

### 4D. Movement ownership

Do not constantly reissue move orders. Let the engine's default pathing execute
an issued route intent until movement completes or the active phase yields.

## 5. Approach and engagement

### 5A. Approach

Route from the active squad center to the selected strategic cell using A*
weighted by distance and strategic-cache threat.

### 5B. Local engagement

After arrival, treat the selected strategic cell as the current mission. Return
to phases 3-4 after arrival without a valid local mission, target death, an empty
strategic cell, mission completion, or an explicit active-owner yield. Do not
interrupt local combat with periodic strategic replanning.

- Undefended: attack the highest-priority live actor first.
- Defended: kite eligible high-threat targets, then crush infantry.
- If neither action is safe, begin a mass attack only when crossover is greater
  than 2. Continue the committed mass attack until the mission is complete or
  crossover reaches 1.
- If mass attack is unavailable at crossover 2 or less, recalculate strategically.
- During mass attack, target the highest live threat first.
- During kiting, choose the closest eligible live actor to the squad. An eligible
  target must be an actual threat or meet the configured economic-priority floor;
  low-priority non-threats are excluded. Fire only from a safe live cell outside
  current enemy range.

All engagement work uses current actor positions and standard live threat
calculation, never the strategic cache. Planned attacks and decloaking must be
evaluated as exposed actions. Detectors reject crushing; cloaked movement past
an Obelisk remains legal, but decloaking unsafely near one does not.

Use one shared squad action, based on the live squad center, while checking each
member's safety independently. A one-tank squad should behave exactly; larger
squads use the same order and may absorb formation error. Do not pre-plan
tactical phase sequences.

## 6. Damage

Route damaged tanks safely to a repair option from any active phase, including
Approach. If no safe repair option exists, resume the phase that yielded to
repair. After repair completes, return to step 1.

## Preservation boundary

Save squad membership/allocation only. On load, preserve those allocations and
restart each squad at TargetAcquisition; do not serialize tactical state,
fingerprints, timers, plans, or pending orders.

Keep public configuration, watchdog output, target priorities, cloak and
detector safety, and established balance unchanged. Keep each lifecycle owner
below 400 lines when practical and every supporting class below 500 lines.
