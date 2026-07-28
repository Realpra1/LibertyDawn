# LibertyDawn — Spam Fork Plan

**Goal:** Make a fork where we don't have to think. We play the economy branch (already fully
unlocked from turn one), spam construction yards and units across a many-spawn map, and fight a
SkyNet AI that is actually competent enough to make that fun.

**Status legend:** `TODO` · `IN PROGRESS` · `BLOCKED` · `DONE`

---

## Ground rules

| Rule | Detail |
|---|---|
| **Do not touch the upgrade system** | No changes to `upgrade.*` / `downgrade.*` actors, no changes to `Prerequisites` lines that reference them, no changes to how the tech branches gate units. We sidestep it by playing econ. This is a hard boundary. |
| **CNC only** | `mods/cnc/` and shared engine code. `ra`, `ts`, `d2k`, `modcontent` are off limits. |
| **Git = storage, local only** | Branch per workstream. Commit freely. **No remote, no push** until we decide otherwise. |
| **One worktree per subagent** | Each workstream gets its own `git worktree` so agents can run concurrently without stepping on each other. Merge into `spam-fork` at the end of each. |
| **Every change must pass** | `make all` (0 errors) **and** `./utility.sh cnc --check-yaml` (exit 0). No exceptions. |

### Branch / worktree layout

```
bleed                    (upstream fork, untouched)
└── spam-fork            (integration branch — everything merges here)
    ├── ai/air-targeting     → worktree ../ld-air
    ├── ai/walls             → worktree ../ld-walls
    └── unit/sheep           → worktree ../ld-sheep
```

---

## Workstream 1 — SkyNet air units hunt soft targets

**Status:** `IN PROGRESS` · **Branch:** `ai/air-targeting` · **Worktree:** `../ld-air`

Right now SkyNet's orcas and helis (`AirUnitsTypes: heli, orca`) get folded into generic attack
squads and fly at whatever the squad is fighting — usually straight into SAM sites.

**Recon correction (step 1):** the premise above was wrong. `AirUnitsTypes: heli, orca` *is* set on
`SquadManagerBotModule@skynet`, and `SquadManagerBotModule.FindNewUnits` already routes those units
into a dedicated `SquadType.Air` squad — they are never folded into the assault squad. The real
causes of suicide runs were:

- `AirStateBase.FindSafePlace` picked the **first** shuffled map grid cell that was "safe" and then
  a **random** enemy from it. No preference for harvesters or production, no scoring at all.
- `AirAttackState` fell back to `SquadManagerBotModule.FindClosestEnemy` (unbounded, whole-`World.Actors`
  scan) the moment its target died — the closest enemy is almost always the defended base, so every
  air squad ended its life flying into SAMs.
- The grid scan was `O(map area / DangerScanRadius²)` `FindActorsInCircle` calls — 441 on a 202×202
  map, per air squad, per bot.

Note on determinism: bot logic runs **host-only** (`OpenRA.Game/Player.cs:215`, `if (IsBot && Game.IsHost)`)
inside `Sync.RunUnsynced`. Touching `World.SharedRandom` from bot code would advance the shared RNG
on the host alone and *cause* a desync. All bot code here uses `World.LocalRandom` (`Squad.Random`),
and the new code does the same.

**We want:** air units that pick off *undefended* things. Lone tanks out of AA cover, harvesters at
the tiberium field, production buildings on the unprotected side of a base.

### Steps

1. `DONE` — **Recon.** See the correction above. There is no `AirStateBase.cs` in this fork;
   `AirStateBase` lives at the top of `AirStates.cs`.
2. `DONE` — Target *scoring* replaces first-match. `AirStateBase.FindBestAirTarget` samples a
   bounded number of grid cells, classifies every enemy actor it finds
   (harvester / production+refinery / other building / unit) and picks the highest scorer after
   subtracting an anti-air penalty and a per-cell distance penalty. Candidates below
   `AirTargetMinimumScore` are rejected, so the squad stays home rather than suiciding.
   `AirAttackState`'s `FindClosestEnemy` fallback now re-runs the scored scan instead.
   Damage level is *not* part of the score — dropped as scope creep.
3. `DONE` — Eight new yaml fields on `SquadManagerBotModuleInfo`: `AirTargetHarvesterValue` (500),
   `AirTargetProductionValue` (350), `AirTargetBuildingValue` (150), `AirTargetUnitValue` (100),
   `AirTargetAntiAirPenalty` (300), `AirTargetDistancePenalty` (1/cell), `AirTargetMinimumScore` (1),
   `AirTargetScanSamples` (24).
4. `DONE` — Wired into `SquadManagerBotModule@skynet` in `mods/cnc/rules/ai.yaml`
   (`AirTargetScanSamples: 40` there; the rest at their defaults, written out explicitly for tuning).
5. `TODO` — **Needs human playtest.** Build (`make all`, 0 errors) and `./utility.sh cnc --check-yaml`
   (exit 0) both pass. Nobody has yet run a skirmish with an exposed harvester to confirm the orcas
   go for it, or checked the framerate impact on Empire Earth with 36 bots.

**Risk:** bot code runs inside the deterministic simulation. Anything non-deterministic here causes
multiplayer desyncs. Use `World.SharedRandom` only — never `Math.Random`.

---

## Workstream 2 — SkyNet walls its base and towers

**Status:** `IN PROGRESS` (code done, awaiting playtest) · **Branch:** `ai/walls` · **Worktree:** `../ld-walls`

SkyNet builds no walls at all. `sbag` / `cycl` / `brik` appear nowhere in its `BuildingLimits`,
`BuildingFractions`, or `DefenseQueues`.

**We want:** concrete walls (`brik`) ringing its defensive towers and the base perimeter.

### Steps

1. `DONE` — **Recon.** `brik` needs `upgrade.recon2` (which SkyNet already buys via
   `UnitsToBuild`) and lives in the `Defence.GDI` / `Defence.Nod` queues that
   `BaseBuilderBotModule@skynet` already drives. Placement went through
   `BaseBuilderQueueManager.ChooseBuildLocation` — a single random cell, i.e. confetti.
2. `DONE` — `BaseBuilderBotModule` could not place walls usefully. Walls are `LineBuild` actors:
   the engine fills the cells between two anchors for free when the order string is `LineBuild`
   rather than `PlaceBuilding`. New C#: `WallTypes` / `WallRingTypes` config plus
   `BaseBuilderWallPlanner` + `BotWallGeometry`.
3. `DONE` — Rings each defensive tower with 5x5 wall lines, anchored corner to corner so LineBuild
   fills them. Capped by `MaximumWallSegments` (60) and by `BuildingLimits: brik` / the
   `BuildingFractions` share, which in practice settles around 25 segments.
4. `DONE` — **Gates.** Two independent guarantees:
   - Structural: `OrderRingSides` clamps to **3 of 4 sides**, so a ring can never be closed, and
     the side dropped first is always the one facing our own base centre — a 3-cell gap on the
     side our units actually need.
   - Behavioural: before a ring is accepted the area reachable from the construction yard is
     flood filled twice, with and without the planned walls. If the second flood loses tiberium
     access, loses the ability to get 20 cells clear of the yard, or loses >10% of its reachable
     area, the ring is discarded and the next candidate is tried.
   Covered by 8 unit tests in `OpenRA.Test/OpenRA.Mods.Common/BotWallGeometryTest.cs`, including
   a case where a wall closes the last gap in a cliff line and is correctly rejected.
5. `DONE` — `brik` added to `BaseBuilderBotModule@skynet`: limit 60, fraction 25, delay 6000.
6. `DONE` — `gtwr` and `gun` raised from limit 1 / fraction 1 to **limit 25 / fraction 20**.
7. `IN PROGRESS` — `make all` 0 errors, `./utility.sh cnc --check-yaml` exit 0, 63/63 unit tests
   pass. **Still needs a human skirmish on Empire Earth**: confirm the walls form lines, that
   enough of them get placed at all (the `Adjacent: 5` buildable-area rule may reject ring cells
   around isolated towers), and watch for `brik`'s `BlocksProjectiles` stopping the ringed tower's
   own shots — if that bites, drop `WallRingSides` to 1 or 2 in `ai.yaml`.

**Risks:** an AI that spends its whole income on walls is a worse opponent, not a better one — the
budget cap is not optional. And an AI that walls itself in is a *dead* opponent. Gaps are a hard
requirement, not a nice-to-have.

---

## Workstream 3 — The Sheep

**Status:** `DONE` (pending playtest) · **Branch:** `unit/sheep` · **Worktree:** `../ld-sheep`

One deliberately stupid unit. Agreed spec:

```
SHEEP — "Sheep"
  Cost:      50
  Speed:     120
  Vision:    10 cells (best in game)
  Unarmed.   HP: 60
  Built from Barracks (pyle / hand), no prerequisites
  On death:  small non-damaging "steak" effect
```

Cheap, fast, blind-spot-free scout that cannot fight. Spam forty, reveal the map, lose them all.
Explicitly **not** game-breaking — it has no weapon and dies to everything.

### Steps

1. `DONE` — **Art decision:** option (b), the CNC Funpark **triceratops** (`tric.shp`). It ships in
   the base game content (verified with `--extract`), is a pale, player-remappable quadruped that
   reads as a woolly animal at gameplay zoom, and a cameo (`bits/tricicnh.shp`) is already in the
   repo. Zero new art.
2. `DONE` — Actor `SHEEP` in `mods/cnc/rules/infantry.yaml` (inherits `^Soldier`, no `Armament`,
   `-AttackFrontal`, `-TakeCover`, `-MustBeDestroyed`, `-SpawnActorOnDeath`).
3. `DONE` — `sheep:` sequence appended to `mods/cnc/sequences/infantry.yaml`, sourcing `tric` frames.
4. `DONE` — `SteakSplat` in `mods/cnc/weapons/explosions.yaml` — `CreateEffect` + `LeaveSmudge` only,
   no `SpreadDamage` warhead at all.
5. `DONE` — `Buildable: Queue: Infantry.GDI, Infantry.Nod` with the `Prerequisites` line omitted.
6. `DONE` — `ai.yaml` untouched; `UnitBuilderBotModule` only builds names in `UnitsToBuild`.
7. `DONE` (partial) — `make all` 0 errors, `--check-yaml` exit 0, `--dump-sequence-sheets` loads every
   sheep frame. **In-game render/move/scout/death still unplayed.**

**Naming note:** it's `SHEEP` in the files. Renaming an actor is a find-and-replace across four
files if you change your mind later.

---

## Out of scope

- **The upgrade system.** Untouched. See ground rules.
- **Any mod other than `cnc`.**
- **Rebasing onto modern OpenRA.** The fork is 3.5 years behind. Not happening.

## Test map

**`mods/cnc/maps/Empire-Earth.oramap`** — "Empire Earth4", **36 spawn points**, 202×202. This is the
map. All playtesting happens here.

Consequences worth remembering while working:

- 36 players' worth of bases and unit spam on a 200×200 field. Anything we add to the AI runs
  **once per bot per tick**. Sloppy `FindActorsInCircle` calls in the wall or air code will tank the
  framerate long before they cause a gameplay problem. Keep scans throttled and radius-bounded.
- SkyNet **does** expand — `McvManagerBotModule@b` covers `enable-skynet-ai` with
  `RestrictMCVDeploymentFallbackToBase: false` and a 20–49 base radius. (The `mcv: 0` entry in
  `UnitBuilderBotModule@skynet` is a *weight* in a different subsystem and does not gate expansion.)
  Wall placement therefore has to cope with a base that grows and relocates — don't cache the
  perimeter once at first build.

## Verification checklist (run for every workstream before merge)

```bash
make all                          # must be 0 errors
./utility.sh cnc --check-yaml     # must be exit 0
./launch-game.sh Game.Mod=cnc     # skirmish vs SkyNet, eyeball the change
```
