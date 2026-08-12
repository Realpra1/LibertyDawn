---
name: comment-cnc-match
description: Convert one Liberty Dawn full-engine game's logs, manifest, summary, and replay-derived metrics into a precise chronological narrative, including control differences, win/loss causes, early build and troop actions, late purchases, and individual-unit stories. Use after each CNC test game before policy review.
---

# Comment on a CNC Match

Use a fresh Luna 5.6 medium session after every judged game. One game receives one
narrative; never combine games into a batch narrative. Remain factual; the Policy
Reviewer judges whether behavior makes strategic sense.

## Inputs and isolation

The launcher job is a validated JSON path envelope with only `artifacts`, optional
`design_reference`, and `output`. Artifacts are staged regular-file copies beneath
the role directory's `inputs/` subtree. Read only:

- The role instructions and path-only job envelope.
- Assigned current/control logs, launch manifests, batch summaries, benchmark
  output, and replay-derived statistics.
- `.agents/references/LIBERTY-DAWN-DESIGN.md` when game terminology or intended
  roles help explain an observed event.

Do not read source code, task sheets, worker specs, implementation notes, or policy
review output. Never infer an event merely because code was intended to produce it.

## Evidence discipline

1. Verify map, seed, factions, bots, alliances, starting conditions, options,
   content/commit identity, headless MAX activation, world-tick progress, exit
   state, and fatal/desync indicators. Mark comparisons invalid or limited when
   supposedly matched inputs differ materially.
2. Attribute factual statements to ticks/timestamps, counters, outcomes, or exact
   artifact paths. Label a causal interpretation as `Inference` and give its
   evidence. Label missing evidence as `Unknown`; never fill a log gap with a
   plausible story.
3. Reconstruct phases: opening, first contact, economic/technology development,
   major attacks and recoveries, turning point, and finish/stop condition.
   Explicitly reconstruct the early build order and early troop actions, including
   idle, scouting, rushing, defending, gathering, and first-contact orders.
4. Describe exactly what the changed AI and old-behavior control did differently:
   build/tech timing, spending, income/storage, unit mix, target selection,
   movement, engagements avoided/taken, losses, idle/stalled resources, recovery,
   and objective/match outcome. Separate policy differences from map position,
   opponent pressure, RNG/nondeterminism, or invalid setup.
5. Explain the most evidence-supported causal chain for why each AI won or lost.
   Do not reduce every result to the final army-value number.
6. Always identify what each AI did well, especially a losing AI. Distinguish a
   sensible decision overwhelmed by circumstances from a true blunder, without
   turning this factual narrative into the policy verdict.
7. If an AI went broke or lost, identify the last three structures or units it
   completed beforehand, in order, with timing/cost when available and whether
   each purchase plausibly helped, delayed recovery, or exposed a queue stall.
8. When stable actor IDs and sufficient events exist, follow two or three
   representative individual units in detail rather than relying only on global
   totals. Prefer a changed-feature unit that succeeded, one that failed or was
   lost, and optionally an effective losing-AI unit. Reconstruct creation, mission
   or assignment, map region/position and route over time, orders/replans, time to
   first useful action, contacts and targets, damage, retreat/repair/unload, kills
   or other useful effects, idle/stall duration, lifespan, how quickly/why it died,
   and final outcome. Compare the
   same role in control when evidence permits. These short unit stories supplement
   the match-level causal account; they do not replace it. If supplied logs or
   replay metrics lack stable identity/event coverage, say exactly what is unknown
   and what bounded instrumentation would make the trace possible.

## Output

Write the requested `NARRATIVE.md` with:

```markdown
# Match narrative: <test/game>

## Evidence integrity and setup
## Outcome in one paragraph
## Chronological narrative
## Early build order
## Early troop actions
## Last three productions before insolvency or defeat
## Individual unit stories
## Changed AI versus old-behavior control
## Why the changed AI won or lost
## Why the control won or lost
## What the losing AI did well
## Other effective decisions
## Observed blunders, stalls, and missed opportunities
## Inferences, alternative explanations, and unknowns
## Questions supplied for policy review
## Source artifacts
```

Use concrete quantities and ticks where available. Return only the narrative path
and any evidence-integrity blocker.
