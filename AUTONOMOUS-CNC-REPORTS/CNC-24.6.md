# CNC-24.6: Separate Headless Automated MAX from Graphical MAX

- Status: complete; GitHub Linux and Windows checks passed
- Cycles used: 14 of 30
- Branch: `agent/cnc24-6-headless-max`
Pull request: https://github.com/Realpra1/LibertyDawn/pull/46

## Plan and literal acceptance

Desired player-visible/automation outcome: an explicit automated headless launch starts a real local CNC skirmish with ordinary bots, no visible game window or game-frame rendering, advances at MAX speed, records logs/benchmark/replay evidence, and exits naturally at game over. Selecting MAX in the ordinary lobby remains a visible, rendered, responsive game.

Forbidden outcomes: headless activation from a lobby speed selection alone; headless remote/network or replay sessions; changed simulation timestep, RNG, orders, AI intervals, checksums, save state, or ordinary Fastest/MAX behavior; idle/missing bots; a hidden process that never exits; or removal of fatal/progress diagnostics.

Implementation plan:

- Add an explicit validated launch argument, hidden platform-window startup, and a headless automation lifecycle independent of the declarative MAX game-speed flag.
- Keep the renderer/world-renderer objects required by this engine generation, but suppress game-frame rendering and render-only world ticks after launch. Continue bounded event pumping and existing no-progress diagnostics.
- Exit headless automation naturally on game over, retaining benchmark/replay disposal and automated save/load support.
- Extend the CNC debug launcher with an explicit headless-MAX form; keep its existing graphical `max` form unchanged.
- Add launch-policy unit tests and diagnostic logging for requested/activated/rejected/completed headless runs.

## Required integrated evidence

- Two independent fully enabled Fastest controls with identical explicit seed/setup; compare sync/replay/final outcomes before using determinism as an equivalence gate.
- One graphical MAX full real-AI match and one headless MAX full real-AI match using the same seeded setup, plus elapsed throughput comparison.
- Save/load, stop/exit, invalid remote/replay request, and high-unit-count contention tests.
- At least three clean post-acceptance adversarial engine cycles, including one complete fastest-speed real match.

## Competing systems to exercise

- Renderer/window initialization, UI/load screen, world `TickRender`, input/event pumping, sound, local order server, replay writer, benchmark completion, automated save/load, game-over exit, network/replay guards, and the graphical lobby speed selector.

## Pause state

The first implementation pass is uncommitted on the task branch. Release compilation passed with zero warnings/errors and the full unit suite passed 273/273. Cycle 1 loaded Empire Earth4 with two Skynets and three Brutalis, hid the SDL window, activated headless MAX, and emitted the expected bot strategy logs. It then remained at world tick 0 (`local=0`, `net=1`, one queued order) until stopped for the user-requested pause. Root cause is already bounded: `renderBeforeNextTick` may be set before headless activation, while headless rendering suppression never clears it, so the logic-tick branch remains gated. Fix this before the next cycle. Preserved evidence: `AUTONOMOUS-CNC-LOGS/cnc246-cycle1-headless-smoke-20260804-114507/`.

Cycle 2 cleared a pre-activation `renderBeforeNextTick` gate inside the headless loop. The same seeded five-bot Empire Earth4 smoke then advanced normally to tick 15,374, wrote benchmark CSVs and a replay, produced a natural three-Brutalis victory over two Skynets, and exited in 98 seconds without a visible game window. All five ordinary AIs built and fought. The full 273-test suite passed. Evidence: `AUTONOMOUS-CNC-LOGS/cnc246-cycle2-headless-complete-20260804/`.

Cycles 3-4 ran the current cumulative head against graphical and headless MAX. Both five-bot Empire Earth4 games completed naturally with the expected Brutalis victory. Headless reached tick 10,000 in roughly half the graphical wall time, but the graphical game ended at tick 11,419 while headless ended at tick 26,674. This is not yet classified as a headless regression: the required pair of identical Fastest controls has not established that independent matches reproduce in this engine. The next cycles use a smaller exact-seed setup for those controls, followed by graphical/headless comparison only if the controls match.

## Acceptance and adversarial result

Status: complete. Local completion gates passed and PR #46 is green on Linux and Windows at commit `1b7398c288`.

- A stale-binary setup mistake invalidated cycles 3-5 after the cumulative CNC-25 merge; it was detected from the old heavy-drop log signature, counted, stopped, rebuilt, and never used as current-head proof.
- Fastest controls on the exact same current commit, map, seed, slots, factions, teams, cash, and orders did not reproduce: control A ended naturally at tick 2,455 in 58 seconds, while control B remained active beyond tick 25,431 after ten minutes. Exact cross-run replay equality is therefore an impossible gate for this engine state; later MAX tests used behavior, valid replay metadata, OOS health, natural exit, and throughput evidence.
- Current-head graphical MAX ended naturally at tick 3,409 in six seconds. Headless MAX ended naturally at tick 4,261 in seven seconds. Both ran ordinary Easy/Brutalis modules and produced Brutalis wins and valid replays. The larger five-bot scenario showed the material rendering benefit more clearly: headless reached tick 10,000 in about half the graphical wall time.
- Adversarial single-exit cycle proved exactly one natural-game-over log and one exit request.
- Save/load cycle created a headless save at tick 1,000, loaded it directly without UI, resumed MAX at tick 1,003, and advanced with active bots beyond tick 46,000.
- High-load cycle ran two Skynets against three Brutalis on Empire Earth4, reached roughly 500 live mobile units, completed naturally at tick 16,821, wrote benchmarks/replay, and exited cleanly.
- Direct engine launches rejected headless remote-connect and replay requests with the intended validation errors before connecting or playback.
- Final original-layout headless regression completed naturally at tick 3,361 with exactly one completion log and the expected Brutalis win.
- Strict Debug build passed with warnings as errors; 277/277 unit tests, interface checks, and complete CNC YAML/map validation passed.

Evidence is under `AUTONOMOUS-CNC-LOGS/cnc246-cycle1-*` through `cnc246-cycle14-*`.
