# CNC-30.1: Bounded Map-Wide Exploration

- Status: complete
- Cycles used: 20 of 30
- Branch: `agent/cnc30-1-bounded-exploration`
- Base: `origin/agent/cnc33a3-no-mutants` at `52c2514939`

## Literal acceptance

In an ordinary real-bot match with money and an MCV, no more than ten simultaneous exploration assignments may exist. Only Chemical Warriors (`e5`), Flamethrowers (`e4`), and Minigunners (`e1`) may receive those assignments, in that preference order when idle eligible units are available. Ten explorers must spread deterministically across stale map regions, including every corner over bounded scans instead of repeatedly favoring the top. At zero spendable cash or with no MCV/construction yard, the existing urgent crate-recovery mode must still use every otherwise suitable mobile collector without the ordinary type or count limits.

Forbidden outcomes are normal-mode vehicle/aircraft/other-infantry explorers, more than ten normal scout assignments, multiple scouts selecting one region, starvation of visible-crate collection, top-only equal-age selection, unreachable regions permanently hiding later candidates, emergency limits that weaken critical recovery, or assignment loss/duplication across save/load.

## Plan and design

- Keep visible-crate collection behavior separate and unchanged; it retains first priority during an emergency.
- Add configurable normal scout type priorities (`e5` > `e4` > `e1`) and a normal simultaneous limit of ten. Use existing idle owned units only; this task does not create production demand.
- Preserve unrestricted suitable-unit exploration in urgent mode. When urgency ends, retain only the best eligible normal assignments up to the cap and release the rest cleanly.
- Replace row-major equal-age ranking with a deterministic, spatially distributed coverage order over actual coarse regions, plus a persisted cursor. Preserve unseen/oldest-first semantics, consider only a bounded number per actor, and advance through blocked candidates so asymmetric or disconnected maps cannot pin every scan to the same region set.
- Add bounded debug evidence for mode, type priority, assignment count, coverage rank/cursor, rejection/contention, release, and reached destinations; keep release configuration quiet.

## Contention inventory

The same infantry can be recruited by `SquadManagerBotModule`, reserved as transport passengers or carriers by `TransportManagerBotModule`, used by specialist stealth/chemical/red-Tiberium modules where eligible, ordered for repair, occupy cargo through `Passenger`, or be unavailable because of health, death, capture, or other `IBotUnitReservations`. Visible crate assignments share this module's reservation table with scouts. Infantry production and build queues are deliberately not consumed by normal exploration. Tests must exercise ordinary squad recruitment, transport/specialist reservation rejection, visible-crate priority, health/cargo state, emergency transition, and save/load restoration.

## Test matrix

Focused policy/unit checks cover exact emergency boundaries; zero, fewer, exactly, and more than ten eligible candidates; type preference and missing types; deterministic spatial coverage including all corners; stale-history ordering; cursor wrap; assigned-region exclusion; and asymmetric region sets. Full-engine cycles cover ordinary and urgent real AIs, connected and blocked maps, visible crates, competing reservations, transition in both directions, save/load, and a natural headless MAX full match. After acceptance, at least three distinct clean adversarial games and a final literal regression are required.

## Implementation result

- CNC config sets a ten-scout ordinary limit and `e5: 300`, `e4: 200`, `e1: 100`; other types remain available only to urgent recovery. Ordinary recruitment uses existing idle actors and consumes no production queue.
- The manager trims an urgent assignment set when ordinary conditions return, first releasing disallowed types and then retaining at most ten assignments by configured priority and actor id. Crate collection remains separate and first-priority.
- Exploration regions now contain only cells within playable map bounds. A deterministic farthest-point coverage order, persisted rotating cursor, assigned-region exclusion, and bounded reachability attempts distribute equal-age choices across the whole playable map without letting an unreachable prefix pin selection.
- Debug-only evidence records the mode, cap, cursor, coverage rank, contention/cargo counts, assignment, rejection, transition, and release. Release rules leave crate debug logging disabled.

## Engine evidence

- Cycles 3-4 exposed and fixed the directional root cause: the original region grid included the map package's full backing dimensions instead of playable bounds. The corrected 64x32 fixture produced exactly 72 regions/2,048 cells and distributed ten ordinary Chemical Warriors across all extremes, including the bottom-right.
- Cycles 5-7 exercised emergency expansion and live recovery. The clean zero-cash run expanded from ten preferred ordinary scouts to 37 suitable collectors including other infantry, then released non-normal/excess assignments and returned to exactly `10/10` as soon as cash returned.
- Cycles 8-10 saved 37 live urgent assignments at tick 150, rejected one invalid external-map load setup, then loaded the correctly staged save at tick 151. The persisted cursor/assignments continued without duplication and recovered to exactly ten preferred ordinary scouts at tick 202.
- Cycles 11-17 were deliberately retained failed/inconclusive transport-contention fixture attempts: vehicle-domain and target reachability prevented the transport mission rather than exposing a crate reservation defect. Cycle 18 corrected the fixture and forced VIKI's ordinary assault-transport manager to reserve an APC and passenger. Crate scans reported the reservations/cargo and did not commandeer either actor through tick 1,000.
- Cycle 19 was a natural VIKI-versus-Brutalis Empire Earth match at seed 30119. Headless MAX ran to natural game-over beyond tick 40,000 in 746.7 seconds and flushed replay plus benchmarks. Across 281 ordinary scans, the cap ranged from zero to ten and every one of 271 ordinary assignments used only `e1` or `e5`; 161 distinct destinations reached x=2..199 and y=3..199. Natural critical-loss recovery expanded to 84 scouts across unrestricted types, then trimmed on recovery. No desync, fatal Lua, rules-load, or unhandled-exception marker appeared.
- Cycle 20 final regression used ordinary SkyNet and Brutalis at seed 30120. It held ten `e4`/`e5` ordinary scouts, expanded to 38 scouts including `e2` at forced zero cash, released the non-normal assignments at tick 202 after cash restoration, and finished back at exactly `10/10`. It reached tick 500 under headless MAX and flushed replay/benchmarks without forbidden errors.

The three clean post-acceptance adversarial cases are the live emergency/recovery transition (cycle 7), correctly staged mid-emergency save/load (cycle 10), and real assault-transport reservation/cargo contention (cycle 18). Cycle 19 supplies the required natural ordinary full match, and cycle 20 is the post-adversarial literal regression. Raw artifacts are ignored under `.build/cnc30.1/evidence/`.

## Validation and publication

- `make check`: strict Debug build passed with zero warnings/errors; both explicit-interface validators passed.
- `dotnet test OpenRA.Test/OpenRA.Test.csproj --no-restore`: 394/394 passed, including 10 focused crate-policy cases.
- `make test`: Release build passed with zero warnings/errors and exhaustive CNC sequences, rules, scripts, and maps passed MiniYAML validation.
- `git diff --check` passed. Draft PR #70 is mergeable; its implementation head passed Linux CI in 2m09s and Windows CI in 3m32s. PR: https://github.com/Realpra1/LibertyDawn/pull/70
