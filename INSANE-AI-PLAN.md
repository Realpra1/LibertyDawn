# Insane AI Overhaul Plan

This branch is intentionally stacked on `agent/adaptive-air-risk` until PR #27 is merged. It preserves LibertyDawn content and balance unless a listed task explicitly changes behavior.

## Engineering choices

- Port selected upstream OpenRA correctness and performance fixes instead of performing a blind whole-engine rebase. Every imported change must be reviewed against LibertyDawn resource growth, harvester behavior, maps, and balance.
- Keep strategies opt-in through bot YAML configuration. General safety and bug fixes may be enabled broadly; specialized APC, heavy airlift, stealth harassment, technology response, and red-Tiberium attacks remain configurable.
- Reuse real player orders and existing activities for capture, C4, loading, unloading, delivery, crate pickup, and movement.
- Centralize transport allocation so rescue lifts, APC assaults, long-distance reinforcement, and mammoth drops cannot reserve the same carrier or exceed configured limits.
- Keep autonomous testing bounded to smoke matches and selected longer integration matches on Empire Earth. Diagnose fresh logs after each integration milestone.
- Restrict the normal LibertyDawn build, lint, test, and packaging paths to CNC and the shared engine components required by CNC.

## Workstreams

1. **Upstream correctness/performance port** — identify the matching upstream era, port the no-path/pathfinder freeze fix and other low-risk engine improvements, then verify LibertyDawn-specific economy and harvester behavior remains present.
2. **Transport coordination** — rescue persistently unroutable ordered units with up to ten Chinooks; configurable APC infantry assaults; long-distance reinforcement transport; configurable mammoth air-drop strategy; emergency unload when attacked.
3. **Existing-order special units** — engineer capture of husks/buildings, commando C4, own supply-truck delivery, allied economic rescue trucks, and visible crate collection with last-resort recovery behavior.
4. **Strategic AI policies** — IronReaper delayed counter-tech switching, configurable stealth-tank attack/harassment squads, red-Tiberium harvester attacks, refinery/silo congestion relief, excess-cash expansion, and configurable opening build order.
5. **Ground squad generalization** — extract reusable strategic-cell scoring/routing from air squads and add cohesive mixed ground attack squads while preserving existing defensive squads.
6. **Scope/performance/refactoring** — make chem tanks inherently stealthed; remove RA/D2K from LibertyDawn build/test/package paths; profile autonomous matches; refactor only concrete duplication or measured hot paths.

## Integration gates

- Each workstream lands as focused commits from an isolated branch/worktree.
- Run focused unit tests before integration and CNC lint/map validation after integration.
- Run at least one bounded Empire Earth autonomous smoke match after each behavior milestone and inspect fresh debug, warning, error, exception, server, and performance logs.
- Do not silently change unit costs, damage, health, armor, build times, resource yields, or LibertyDawn resource-growth behavior.
