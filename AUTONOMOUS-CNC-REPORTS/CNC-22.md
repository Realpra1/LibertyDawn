# CNC-22: Supply-Truck Stall Recovery

- Status: complete; GitHub Linux and Windows checks passed
- Cycles used: 8 of 30
- Branch: `agent/cnc22-supply-truck`
Pull request: https://github.com/Realpra1/LibertyDawn/pull/41

## Behavior

AI supply trucks still use the normal `DeliverCash` player order and prefer the nearest compatible owned building. The AI now records the assigned target, closest observed distance, and last progress tick. It cancels and recalculates immediately if the target disappears or the order ends without delivery, and after 250 ticks (ten seconds) if the truck has not moved closer.

Recovery deterministically prefers a different eligible building before retrying the same one. When no eligible building exists the truck is stopped and released so a later scan can reconsider it. Debug logging identifies new assignments and the specific recovery reason.

## Design choices

- Used decreasing squared distance as the progress signal. This is cheap, deterministic, and does not let side-to-side bouncing reset the timer.
- Kept the five-second bounded module scan and made the ten-second retry interval configurable in CNC rules.
- Preserved normal order resolution and delivery traits instead of introducing a separate movement path.
- Sorted equal-distance targets by actor ID to preserve deterministic behavior.

## Validation

- Strict Debug build: passed with zero warnings and zero errors.
- Unit tests: 243/243 passed, including closer-only progress and idle/invalid/timeout recovery policy.
- Explicit-interface and conditional-trait-interface checks: passed.
- CNC MiniYAML, sequences, rules, and all map validation: passed.
- Wall-blocked nearest destination: retargeted to the farther accessible building after ten seconds.
- All destinations blocked or inaccessible: assignments continued to rotate/retry rather than permanently stalling.
- Destroyed assigned destination: retargeted on the next review with `target unavailable` evidence.
- Normal Empire Earth match: two SkyNet and three Brutalis AIs loaded, deployed, constructed, and produced units without errors.

## Failed cycles and corrections

- Two destroyed-target harness attempts failed because the temporary skirmish Lua fixture assumed a named player/actor export. The fixture was corrected to discover the target through live players; the production implementation was unaffected.
- One launch initially omitted quoting around lobby commands, so bots were not created. The launch was rejected as invalid evidence and rerun with verified bot activity.

## Remaining risks

- If every compatible building is permanently unreachable, delivery still depends on a later world change; periodic retargeting prevents permanent AI ownership of the truck.
