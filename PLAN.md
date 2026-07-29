# LibertyDawn — Spam Fork

**Handoff document.** Written at the end of session 1. If you are picking this up cold, read this
file top to bottom before touching anything. The round-by-round working log is preserved verbatim
in the Appendix at the bottom.

**Goal:** a fork where we don't have to think. Play the economy branch (already fully unlocked from
turn one), spam construction yards and units across a huge map, and fight a SkyNet AI competent
enough to make that fun.

**Status legend:** `TODO` · `IN PROGRESS` · `BLOCKED` · `DONE`

---

## 1. Where we are right now

| | |
|---|---|
| Integration branch | **`spam-fork`** — everything is merged here |
| Base branch | `bleed` (the untouched LibertyDawn fork) |
| Commits ahead of `bleed` | 23 |
| Diff vs `bleed` | 26 files, +3522 / −57 |
| Build | `make all` → **0 errors** |
| Rules lint | `./utility.sh cnc --check-yaml` → **exit 0** |
| Tests | `dotnet test OpenRA.Test/OpenRA.Test.csproj` → **105 / 105 pass** |
| Remote | **None.** Local git only. Nothing has ever been pushed. |
| Working tree | Clean |

Three rounds are complete. Round 4 (adaptive AI) is designed but not started.

### Branches and worktrees

```
bleed                    untouched fork base
└── spam-fork            integration branch — merge everything here
    ├── ai/air-targeting  → worktree .worktrees/ld-air
    ├── ai/walls          → worktree .worktrees/ld-walls
    └── unit/sheep        → worktree .worktrees/ld-sheep
```

All three feature branches are **merged into `spam-fork` and level with it**. The worktrees still
exist and are ready to reuse. `.worktrees/` and `vendor-libs/` are gitignored.

### How to resume

```bash
cd /Users/larsholdgaard/Dev/Prototypes/LibertyDawnGayGame
git checkout spam-fork

# sync a worktree before handing it to an agent
git -C .worktrees/ld-air merge --ff-only spam-fork

# ... agent works, commits to its own branch ...

git merge --no-edit ai/air-targeting
make all && ./utility.sh cnc --check-yaml && dotnet test OpenRA.Test/OpenRA.Test.csproj
```

### Working method that has worked

- One subagent per workstream, each in its own `git worktree`, all running concurrently.
- Every agent must pass `make all`, `--check-yaml` and `dotnet test` before handing back.
- Agents are told **not** to launch the GUI — the humans playtest after the merge.
- Playtest → feedback → next round. Three rounds so far, each driven by a real playtest.
- Agents are told to report honestly on what they could **not** verify. This has repeatedly caught
  real problems, including two features that would have silently done nothing.

---

## 2. Ground rules — do not violate these

| Rule | Detail |
|---|---|
| **Never touch the upgrade system** | No changes to `upgrade.*` / `downgrade.*` actors, nor to any `Prerequisites` line referencing them. The players sidestep it by playing econ, which starts fully unlocked. A hard boundary they set explicitly and repeated. |
| **CNC only** | `mods/cnc/` plus shared engine code. `ra`, `ts`, `d2k`, `modcontent` are off limits. |
| **Local git only** | No remote, no push, unless the players say otherwise. |
| **TABS in yaml** | The repo is tab-indented. A previous author left ~843 space-indented lines. Do not add more. |
| **Bot determinism** | Bot logic runs **host-only** (`OpenRA.Game/Player.cs:215`, `if (IsBot && Game.IsHost)`) inside `Sync.RunUnsynced`. Use `World.LocalRandom` / `Squad.Random`. **Never `World.SharedRandom`** — it advances the shared RNG on the host alone and *causes* the desync you were trying to avoid. Never `System.Random`, never `DateTime`, no unordered dictionary iteration. |
| **Performance** | Test map is Empire Earth: **202×202, 36 spawns**. Anything added to a bot runs once per bot per tick, up to 36 times over. Throttle by tick interval, bound every scan by radius, reuse buffers. Always state the new cost. |
| **Verify before blaming** | Several tempting "fork bugs" turned out to be upstream OpenRA (`failCount += failCount;`, the `configure-system-libraries.sh` early exit, `global mix database.dat`, the 100× HP scale). Diff against the fork point `74cced319c` (2022-01-29) before attributing anything. |

---

## 3. What we built (rounds 1–3)

### Round 1 — foundations
- **Sheep** created: 50 credits, unarmed scout, using the Funpark triceratops sprite (`tric.shp`)
  remapped to player colour — already in base content, reads as a woolly animal at gameplay zoom,
  and sits in the player-colour remap range.
- **Air targeting**: scoring replaced first-match selection. Crucially killed the
  `AirAttackState` → `FindClosestEnemy` fallback, an unbounded scan with no danger check that sent
  every air squad into the enemy's defended base the moment its target died.
- **Walls**: SkyNet learned to build `brik` at all. `gtwr`/`gun` limits raised 1 → 25.

### Round 2 — after playtest 1
- **Sheep armed** (players explicitly overruled "not game-breaking"): `SheepMG` + `SheepAA`
  simultaneously, still 50 credits. Plus **the 25-Sheep Rule** — exactly 25 living sheep reveals the
  map; 24 does nothing, 26 does nothing.
- **Air**: continuous AA checking independent of the state machine, per-squad threat memory, route
  threat penalty, directed retreat. Units scored above structures.
- **Walls**: turret-behind-wall siting and choke-point walling. **Both deleted in round 3.**

### Round 3 — after playtest 2
- **Sheep HP 2000 → 6000** (more than an E1 Minigunner's 5000, at 50 credits) and **immune to
  tiberium** via `-DamagedByTerrain:`.
- **Walls gutted.** Net **−864 lines**. Choke detection, turret siting, ring geometry and the
  elaborate flood fill all deleted; config 17 fields → 5. What remains is exactly the players'
  stated strategy: find a tower with no wall, take a 15-cell window 3 cells in front, place the
  **longest** contiguous run as two anchors so `LineBuild` fills the middle free.
- **Air harassment loop.** Squads capped at 5. Evade = hop 12 cells away + ≤5 jitter (~19 cells
  worst case, versus the old ~70). Re-targets every 10 ticks inside the scan it already runs for AA,
  at zero extra world queries. Threat memory life cut 900 → 300 ticks.

### Engine code we now own

**New files**

| File | Lines | Purpose |
|---|---|---|
| `.../BotModules/BotModuleLogic/AirThreatGeometry.cs` | 226 | Pure air threat / evade maths |
| `.../BotModuleLogic/BaseBuilderWallPlanner.cs` | 354 | Picks the tower and the wall line |
| `.../BotModuleLogic/BotWallGeometry.cs` | 159 | Pure wall geometry + escape flood |
| `.../Traits/Player/GrantConditionOnActorCount.cs` | 177 | Condition at an **exact** owned-actor count |
| `.../Traits/Player/RevealsMapOnCondition.cs` | 74 | Whole-map reveal while a condition holds |
| `OpenRA.Test/OpenRA.Mods.Common/AirThreatGeometryTest.cs` | 315 | |
| `OpenRA.Test/OpenRA.Mods.Common/BotWallGeometryTest.cs` | 185 | |
| `OpenRA.Test/OpenRA.Mods.Common/ExactCountTrackerTest.cs` | 144 | 24/25/26 boundary coverage |

**Modified:** `BaseBuilderBotModule.cs` (+20), `BaseBuilderQueueManager.cs` (+13),
`SquadManagerBotModule.cs` (+166), `Squad.cs` (+74), `AirStates.cs` (+485/−53).

`OpenRA.Test.csproj` gained `NUnit3TestAdapter` — the project had NUnit but no adapter, so
`dotnet test` previously discovered **zero** tests.

### Current tuning — `mods/cnc/rules/ai.yaml`

Air, on `SquadManagerBotModule@skynet`:

```
AirTargetHarvesterValue: 1000     AirSafetyCheckInterval: 10
AirTargetUnitValue: 450           AirThreatScanRadius: 16
AirTargetProductionValue: 300     AirThreatFleeMultiplier: 8
AirTargetBuildingValue: 100       AirThreatMemoryTicks: 300
AirTargetDefencelessBonus: 250    AirThreatMemorySize: 12
AirTargetAntiAirPenalty: 700      AirThreatMemoryMergeRadius: 3
AirTargetDistancePenalty: 3       AirRetreatOrderInterval: 50
AirTargetMinimumScore: 200        AirRouteThreatPenalty: 200
AirTargetScanSamples: 24          AirRouteThreatRadius: 8
AirSquadSize: 5                   AirEvadeDistance: 12
MaximumAirSquads: 2               AirEvadeJitter: 5
```

Walls, on `BaseBuilderBotModule@skynet` (`ai.yaml:809–813`):

```
WallTypes: brik
WalledDefenseTypes: gtwr, gun, atwr, obli, sam
WallDistanceFromTower: 3
MaximumWallSegments: 150
WallPathCheckLocomotor: wheeled
```

### Sheep, as shipped

```
SHEEP  "Sheep"   (mods/cnc/rules/infantry.yaml:314+)
  Cost 50 · HP 6000 · Speed 120 · RevealsShroud 16c0 · no prerequisites
  Armament@PRIMARY  SheepMG — range 8c0,  burst 3, 4000 dmg (weapons/smallcaliber.yaml)
  Armament@AA       SheepAA — range 10c0, burst 2, 6000 dmg, air only (weapons/missiles.yaml)
  -DamagedByTerrain (tiberium immune) · -TakeCover · -MustBeDestroyed · -SpawnActorOnDeath ×2
  Explodes: SteakSplat — cosmetic, no damage warhead at all
  Sprite: tric.shp via the `sheep:` sequence · VoiceSet SheepVoice
  Deliberately absent from every bot's UnitsToBuild — the AI cannot build sheep.
```

---

## 4. Round 4 — the adaptive AI (NEXT)

**Status:** `TODO` — designed, not started. Read **`ADAPTIVE-AI-DESIGN.md`** (repo root, 330 lines)
before doing anything.

### What the players asked for

> *"If the AI could track the value kill ratio of units and turrets, and then if the AI sees that
> something is underperforming, it will build less of that. And if something is performing really
> well, it will start to use that more."*

Later refined to: k/d ratio **and** economy ratio.

### How to work on it

1. **Read `ADAPTIVE-AI-DESIGN.md`.** 11 questions, each with 2–3 options and one marked
   *(Recommended)*, grounded in this codebase with file and line citations. It was produced
   deliberately as a design document with **zero code written**.
2. **Get the players to pick options** — especially Q1, which is a blocker. Do not start
   implementing before they have chosen. Rounds 2 and 3 both had work partly undone because
   implementation ran ahead of agreement; round 3 deleted 864 lines for exactly this reason.
3. Then implement in `.worktrees/ld-sheep` (currently idle) or a fresh worktree, under §2's rules.

### Question headlines

1. `IdleBaseUnitsMaximum: 999` makes the weights inert. What do we do about it? **(blocker)**
2. What exactly is the score?
3. How do we attribute a kill?
4. What does "economy ratio" mean concretely?
5. Which actor types are eligible for adaptation at all?
6. How far may a weight move?
7. Exploration vs exploitation — how do we avoid the death spiral?
8. Window and cold start
9. Per-match only, or persisted across matches?
10. How is this observable?
11. Do defensive structures adapt, and what is a turret's "value destroyed"?

### The three findings that constrain the design

**1. The build weights are dead code. This blocks the whole feature.**

`UnitBuilderBotModule.cs:90`:

```csharp
BuildUnit(bot, q, idleUnitCount < Info.IdleBaseUnitsMaximum);
//                └──────── this argument is the `buildRandom` flag ────────┘
```

When `buildRandom` is true, `BuildUnit` calls `ChooseRandomUnitToBuild` —
`buildableThings.Random(world.LocalRandom)`, a **uniform** pick. The weighted `ChooseUnitToBuild`
only runs when it is false.

The engine default for `IdleBaseUnitsMaximum` is **12**. This fork sets it to **999** on six bots
(`ai.yaml:585, 736, 916, 1070, 1226, 1354`), and the field appears **zero** times in upstream's
`ai.yaml`. So the condition is effectively always true and the weighted path never executes.

`UnitsToBuild` is then read at `:120` purely as a whitelist:

```csharp
if (Info.UnitsToBuild != null && !Info.UnitsToBuild.ContainsKey(name))
    return;
```

Only the **key** matters; the number is decoration. That is why `ctnk: 0`, `mlrs: 0` and `mcv: 0`
are still built. Adapting these weights as they stand would change literally nothing.

Note also that even on the weighted path a weight is a *share ceiling*
(`myUnits.Count(a => a == unit.Key) * 100 < unit.Value * myUnits.Count`, `:188`), not a share. So
`harv: 100` means "never capped" — raising it does nothing, lowering it bites hard. The scale is
asymmetric.

**Fixing this is one field per bot and it substantially changes how the AI plays.** That is a
gameplay decision for the players, not a technical one. It has been flagged to them and left open.

**2. Kill attribution is last-hit only, but the plumbing is nearly free.**

`AttackInfo` carries a single `Attacker` (`OpenRA.Game/Traits/TraitsInterfaces.cs:89-95`); there is
no damage ledger. But `UpdatesPlayerStatistics` (`PlayerStatistics.cs:210-251`) already credits
`KillsCost`/`DeathsCost` cross-player on `INotifyKilled`, is attached to `^ExistsInWorld`
(`defaults.yaml:3` — every unit, building and turret), prices everything via `ValuedInfo.Cost`, and
dedupes overkill (`Health.cs:162`). Bucketing that by `e.Attacker.Info.Name` is ~30 lines.

Use the **victim-side** hook, not `INotifyAppliedDamage` — the latter is skipped when the attacker
is already dead (`Health.cs:210`). Consequence: artillery and rocket support units will be
systematically undervalued under last-hit attribution.

**3. Economy is per-refinery, never per-harvester; and the Info dicts are shared.**

`INotifyResourceAccepted` reports the refinery, not the harvester (`Refinery.cs:127`,
`TraitsInterfaces.cs:163`), so `harv` cannot be compared against `sharv` without modifying
`Refinery.cs` — part of the resource simulation this fork already heavily rewrote.

Separately, `UnitsToBuild` / `BuildingFractions` are `readonly Dictionary` fields on the **shared**
`...BotModuleInfo`. Two SkyNet players in one match share the instance, and it persists
process-wide. Adapted weights must live in a per-module copy and be registered with
`IGameSaveTraitData` (`UnitBuilderBotModule.cs:215-242`), or a save/load will silently revert them.

---

## 5. Open risks and unverified work

None of the round-3 behaviour has been observed in a running game. It is static reasoning plus unit
tests.

| Risk | Where | Cheap fix if it bites |
|---|---|---|
| **Air squads oscillate** — after an evade hop the squad re-targets back toward the same AA and bounces | `AirStates.cs` | Raise `AirRetreatOrderInterval` above 50 |
| **Framerate with 36 bots.** Air cost went ~21 → ~30 `FindActorsInCircle`/tick — estimated from call counts, never profiled | `SquadManagerBotModule.cs` | `MaximumAirSquads: 2` → `1` |
| **Too few walls appear.** `brik` is `Adjacent: 5` and `^Wall` gives no buildable area, so towers far from regular buildings never get one | `BaseBuilderWallPlanner.cs` | Lower `WallDistanceFromTower` |
| **Orcas too timid** — if they never leave home the score floor is too high | `ai.yaml` | Lower `AirTargetMinimumScore: 200` |
| **Sheep attack animation** reuses triceratops frames 80–91; never seen on screen | `sequences/infantry.yaml` | — |
| **25-Sheep Rule** verified only at `ExactCountTracker` level; event wiring and shroud source never observed in play | `GrantConditionOnActorCount.cs` | — |

**`BlocksProjectiles` is resolved, not a risk.** `brik` blocks projectiles, but all five walled
defence types shoot through it: `HighV` (`gtwr`) and `TurretGun` (`gun`) set `Blockable: false`
explicitly; `atwr`/`sam` inherit `^MissileWeapon`, which sets it false; and `LaserZap.Blockable`
defaults to `false` in the engine (`LaserZap.cs:59`), with `Laser` never overriding it.

### Deferred features

- **Air "big push" doctrine** — mass aircraft against one high-value structure when no exposed
  targets exist. Agreed to be a distinct behaviour from harassment. Not started.
- **Fog of war.** `player.yaml:38` sets `ExploredMapCheckboxEnabled: True` (engine default is
  `false`), so every game starts with the map already revealed. This blunts both the Sheep's 16-cell
  vision and the 25-Sheep Rule, which can then only lift *fog*, not shroud. The players were told;
  unticking **"Initial map shroud is revealed"** in the lobby restores both. No code change made.

---

## 6. Build and run

```bash
make all                        # ~17s
./launch-game.sh Game.Mod=cnc
./utility.sh cnc --check-yaml   # lints every rule and every map
dotnet test OpenRA.Test/OpenRA.Test.csproj
```

Native library setup and the Lua 5.1 build are documented in **`CLAUDE.md`** — `make` does not wire
these up, and `configure-system-libraries.sh` bails on the first missing library so nothing gets
linked. The prebuilt `vendor-libs/liblua5.1.dylib` is gitignored but present on this machine.

**Test map:** `mods/cnc/maps/Empire-Earth.oramap` — "Empire Earth4", 36 spawns, 202×202, authored by
Realpra1. This is the map the players actually use.

---
---

# Appendix — original round-by-round working log

*Everything below is the working log as it was written during the session, preserved verbatim.
Section 3 above supersedes it where they disagree — in particular, the round-2 wall features
described below were deleted in round 3.*

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

**Risk:** see the determinism note above. Bot code must use `World.LocalRandom` / `Squad.Random` to
match the rest of `BotModules/` — never `System.Random`, never `DateTime`, and never
`World.SharedRandom` (which would desync, since bots run host-only).

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
   `BaseBuilderWallPlanner` + `BotWallGeometry`. (`WallRingTypes` was renamed to
   `ShieldedDefenseTypes` in round 2 — see *R2 · Walls*.)
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

---

# ROUND 2 — post-playtest feedback

First playtest done. Everything built and ran. Feedback below is from the players, and overrides
earlier specs where they conflict.

## R2 · Sheep — "sheep are the power of the universe"

**Status:** `DONE` · **Branch:** `unit/sheep`

The Sheep is underpowered. It stays at **50 credits** but becomes a genuinely strong unit.

1. `DONE` — Give it a **machine gun** (anti-ground) and a **separate anti-air weapon**. Both, on the
   same actor, at the same price.
   `SHEEP` now carries `Armament@PRIMARY: SheepMG` and `Armament@AA: SheepAA` (named `secondary`),
   with `AttackFrontal` restored and `^AutoTargetAllAssaultMove` so it engages air and ground on its
   own. `SheepMG` (`weapons/smallcaliber.yaml`, inherits `^HeavyMG`): 8c0 range, burst 3,
   `ReloadDelay: 12`, 4000 damage. `SheepAA` (`weapons/missiles.yaml`, inherits `AARockets`):
   10c0 range, burst 2, `ReloadDelay: 30`, 6000 damage, `ValidTargets: Air`. Cost 50, HP 2000,
   Speed 120, `RevealsShroud: 16c0` all unchanged. An `attack` sequence was added to the `sheep`
   sequence block (reusing the `tric` attack frames) so it animates while firing.
2. `DONE` — **The 25-Sheep Rule.** When a player has *exactly* **25** living sheep, the entire map
   is revealed to them. At 24 it does not work. At 26 it does not work. Exactly 25, or nothing.
   This is deliberate and is not to be "fixed" into a threshold.
   Two new general-purpose player traits, both wired up in `rules/player.yaml`:
   - `GrantConditionOnActorCount` (`Traits/Player/GrantConditionOnActorCount.cs`) — counts owned,
     in-world actors of the configured type(s) and grants a condition while the count is *exactly*
     equal to `Count`. The count is maintained incrementally from `World.ActorAdded` /
     `World.ActorRemoved` (which also cover ownership changes, since `ChangeOwnerSync` re-adds the
     actor), so nothing is ever rescanned per tick.
   - `RevealsMapOnCondition` (`Traits/Player/RevealsMapOnCondition.cs`) — registers **one** shroud
     source over `Map.ProjectedCells` when its condition is enabled and drops it again when it is
     not, so the whole-map reveal costs one pass per on/off transition regardless of how many
     sheep caused it.
   The exact-count logic is factored into `ExactCountTracker`; 18 boundary tests (24/25/26 and
   repeated flips) live in `OpenRA.Test/OpenRA.Mods.Common/ExactCountTrackerTest.cs`.

> Balance note, recorded and overruled: a 50-credit unit with 2000 HP, a machine gun and AA is
> strictly better than an E1 Minigunner at 120 credits. This was raised and the players want it
> anyway. Build it as specified.

> Known limitation of the reveal: map *exploration* is permanent in OpenRA, so dropping off 25
> restores fog of war over the whole map but does not re-shroud terrain that was uncovered while
> the rule was active. With this mod's default `ExploredMapCheckboxEnabled: True` the map already
> starts explored anyway, so in practice the observable effect is full fog removal on and off.

## R2 · Walls — turrets behind the walls, walls at chokes

**Status:** `IN PROGRESS` (code done, awaiting playtest) · **Branch:** `ai/walls` · **Worktree:** `../ld-walls`

Walls are being built reasonably. Two changes:

1. `DONE` — **Invert the relationship.** Round 1's "ring an existing tower" pass is **superseded**,
   not kept alongside. `BaseBuilderWallPlanner` now picks a *site* on the enemy-facing side of the
   base precisely because a wall ring fits around it, queues the ring first, and **reserves the
   centre cell** for the next defensive structure the build queue produces.
   `BaseBuilderQueueManager` asks `WallPlanner.TakeDefenseCell()` before falling back to the stock
   `ChooseBuildLocation`. A reserved cell only unlocks once every anchor of the wall in front of it
   has been ordered, so the concrete is always queued before the thing it protects.
   `WallRingTypes` was renamed `ShieldedDefenseTypes` to match (same value: `gtwr, gun, atwr, obli, sam`).
2. `DONE` — **Choke points.** `BotWallGeometry.TryFindChoke` recognises a choke as a short pinch of
   passable ground between two blockers sitting on a corridor that is open in the perpendicular
   direction (dead-end pockets are rejected). `BaseBuilderWallPlanner.ScanChokes` runs the detection
   **once per base location** — cached, invalidated only when the base centre moves more than
   `ChokeRescanDistance` cells — over a bounded annulus, capped at `ChokeScanMaxCells` cells and
   stopping early at `MaximumCachedChokes` finds. Upper bound with the shipped numbers: ~1200 cells
   × ~40 `Locomotor.MovementCostForCell` array lookups ≈ 48k lookups, once. Never per tick.
   Chokes are walled narrowest-first, up to `MaximumWalledChokes` (4), and turret slots are reserved
   `WallTurretSetback` cells behind the wall on our own side.
3. `DONE` — **Gap and pathability guarantees, extended.** Three independent guarantees now:
   - Structural, rings: unchanged — `OrderRingSides` clamps to 3 of 4 sides, gap faces our base.
   - Structural, chokes: `ChokeGapCells` is clamped to **at least one**, so a choke can never be
     sealed shut however the yaml is configured.
   - Behavioural: the before/after flood fill now also carries a set of **waypoints** — our own
     construction yards and refineries, plus both mouths of the choke's corridor. Losing any of them
     rejects the plan. Choke checks use `ChokeEscapeDistance: 50` (≥ `MaxBaseRadius: 49`, so the bot
     cannot wall itself out of ground it is about to expand onto) and a correspondingly larger
     `ChokePathCheckMaxCells: 8000` so the flood can actually travel that far.
4. `DONE` — **`BlocksProjectiles` risk from round 1 is resolved: it does not apply.** Every weapon
   the five shielded defences fire is unblockable. `HighV` (`gtwr`) and `TurretGun` (`gun`) both set
   `Blockable: false` explicitly; `^MissileWeapon` sets `Blockable: false`, covering `TowerMissile` /
   `TowerAAMissile` (`atwr`) and `Dragon` (`sam`); `obli`'s `Laser` is a `LaserZap`, whose `Blockable`
   defaults to false. A `brik` in front of a SkyNet turret therefore blocks enemy fire without
   blocking the turret's own. Nothing else was changed to accommodate this.
5. `DONE` — Throttling. A planning pass that finds nothing usable puts the planner to sleep for
   `WallPlanRetryDelay` (500) ticks; sites and chokes that failed are remembered so they are not
   re-evaluated on every queue tick.
6. `DONE` — 15 new `[Desc]`-annotated yaml fields, all defaulting to off/neutral
   (`MaximumWalledChokes: 0` by default), so no other mod or bot is affected. Configured on
   `BaseBuilderBotModule@skynet` only.
7. `IN PROGRESS` — `make all` 0 errors, `./utility.sh cnc --check-yaml` exit 0, **72/72** unit tests
   pass (63 existing + 9 new, covering choke detection, the always-a-gap invariant, turret slot
   placement, and a choke whose walling would trap the base being rejected).
   **Still needs a human skirmish on Empire Earth.** Unverified in-game: that choke walls are
   actually placeable at all (walls are `Adjacent: 5` and do not themselves give buildable area, so
   only chokes hugging the base envelope can be built), and that reserved turret slots are taken up
   rather than expiring — `TakeDefenseCell` re-checks placement with the real defence actor
   (`Adjacent: 4`) and drops the slot if it no longer fits, silently falling back to stock placement.

## R2 · Air — permanent AA avoidance, soft targets first

**Status:** `IN PROGRESS` (code done, awaiting playtest) · **Branch:** `ai/air-targeting`

Three changes, in priority order:

1. `DONE` — **Continuous AA avoidance.** Right now AA is checked only when a target is *selected*.
   If AA arrives while the squad is mid-attack, it does not flee. It is also unclear whether the
   route to a target avoids AA at all. Aircraft must continuously avoid anti-air in their vicinity
   throughout a harassment run — on approach, during the attack, and on the way home.

   **What already existed:** `AirIdleState` called `StateBase.ShouldFlee` (one scan around a random
   squad unit, every `AttackForceInterval` = 75 ticks); `AirAttackState` re-checked
   `NearToPosSafely` around *the target's* position each update; `AirFleeState` issued a move to a
   *random* own building and immediately reverted to idle. Nothing ever looked at the AA around the
   squad's own position while it was in transit or mid-attack, and nothing considered the route.

   **What was added:**
   - `SquadManagerBotModule` gained an `airSafetyTicks` counter that calls `Squad.TickAirSafety()`
     on air squads every `AirSafetyCheckInterval` ticks, independent of the squad state machine.
     One bounded `FindActorsInCircle` (`AirThreatScanRadius`) around the squad's own centre. This is
     what makes the check continuous: it fires on approach, mid-attack and on the way home alike.
     When it trips (`aaCount * AirThreatFleeMultiplier > squad size`, suppressed over our own
     buildings, rate limited by `AirRetreatOrderInterval`) the squad drops its target, retreats and
     switches to `AirFleeState`.
   - Per-squad **threat memory**: up to `AirThreatMemorySize` anti-air positions, merged within
     `AirThreatMemoryMergeRadius` and expiring after `AirThreatMemoryTicks`. Advisory bot-only
     state; never saved, never synced.
   - **Route avoidance at target selection.** `FindBestAirTarget` now also charges
     `AirRouteThreatPenalty` per known anti-air position within `AirRouteThreatRadius` of the
     straight line the squad would fly, ignoring anything within `DangerScanRadius` of the
     destination (already priced by the anti-air penalty). Threat positions come from the grid
     samples the scan already takes plus the squad's memory — **zero extra world queries**.
   - **Retreat is directed**: `AirFleeState` now flies to the own building furthest from the
     remembered threats instead of a random one. Falls back to the stock random building when
     nothing is remembered.
   - `AirIdleState`'s duplicate `ShouldFlee` scan is skipped when the continuous check is on.

   Aircraft still fly a straight line — the engine has no threat-aware air pathfinder and adding
   one was out of scope. "Avoids AA en route" therefore means *picks routes that avoid known AA*
   plus *bails out when AA appears along the way*, not *flies around it*.
2. `DONE` — **Defenceless units over structures.** New `AirTargetDefencelessBonus` awarded to any
   candidate with no armament able to target `Air`, plus a reshaped class table. At skynet's values
   an undefended tank (450 + 250 = 700) now clearly beats a refinery (300 + 250 = 550) and an
   ordinary building (100 + 250 = 350). `AirTargetAntiAirPenalty` went 300 → 700 and
   `AirTargetMinimumScore` 1 → 200, so a single SAM is enough to veto a unit target outright.
3. `DONE` — **Raise harvester weight**: `AirTargetHarvesterValue` 500 → 1000, which with the
   defenceless bonus is 1250 — still worth one SAM's worth of risk, but not two.

**Cost:** per bot, per tick, worst case — target scan `41/75` + safety check `1/25` ≈ **0.59
`FindActorsInCircle` calls**, versus round 1's `42/75` ≈ 0.56. Across 36 bots that is **~21
circle scans/tick, up from ~20**. There is at most one air squad per bot
(`GetSquadOfType` is a singleton lookup), so this does not scale with aircraft count. The new
scoring work is pure arithmetic bounded by `AirTargetScanSamples × (samples + memory)` ≈ 40 × 52.

**Verification:** `make all` 0 errors, Debug build with `-warnaserror
-p:EnforceCodeStyleInBuild=true` 0 warnings, `./utility.sh cnc --check-yaml` exit 0, 78/78 unit
tests pass (63 pre-existing + 15 new in
`OpenRA.Test/OpenRA.Mods.Common/AirThreatGeometryTest.cs` covering segment distance, corridor
counting, destination exclusion, retreat-point choice and the score ordering).

4. `TODO` — **Needs human playtest.** Nobody has watched a squad actually break off a run. The
   numbers most likely to need tuning: `AirThreatFleeMultiplier: 8` (with 12 aircraft, two AA
   actors nearby trigger a retreat — may be too twitchy, or not twitchy enough),
   `AirTargetMinimumScore: 200` combined with `AirTargetAntiAirPenalty: 700` (if SkyNet's orcas
   never leave home, this pair is too cowardly), and `AirTargetDistancePenalty: 1 → 3` (harassment
   should now stay local; if the air squad ignores a juicy far harvester, lower it).

---

# ROUND 3 — second playtest feedback

Second playtest. Verdict: the Sheep saved the game, the walls made no strategic sense, and the air
units still fly into anti-air and then retreat across the entire map.

**Key diagnosis:** the walls were not random. The cluster blocking one entrance *was* the round-2
choke-point feature working as designed. Chokes are terrain features, so the AI walled one and
correctly found nothing else qualifying. The behaviour was wrong because the design was wrong, not
because it was buggy. Round 3 deletes it.

## R3 · Walls — gut it (KISS)

**Status:** `DONE` · **Branch:** `ai/walls`

The players' own strategy, which is the spec: *you pay for the two endpoints and the engine fills
the line between them for free, so long walls are cheap and one-at-a-time is waste. Find a tower
with no wall, put a wall in front of it. That is the entire feature.*

1. `DONE` — **Deleted** choke detection (`TryFindChoke`, `ScanChokes`, caching, choke config/tests).
2. `DONE` — **Deleted** turret-behind-wall siting (`PlanShieldedDefenseSite`, slot reservation, the
   `consumedAnchors` counter, `TakeDefenseCell`). Turret placement is stock `ChooseBuildLocation`.
3. `DONE` — Planner is now one idea: the unwalled tower nearest the enemy gets the longest placeable
   run in a 15-cell window three cells off its enemy-facing side, queued as two `LineBuild` anchors.
4. `DONE` — Reachability is one bounded flood fill: with the line solid, a unit beside the
   construction yard must still get 20 cells clear. No baseline pass, no multi-target counting.
5. `DONE` — 17 yaml fields → 5: `WallTypes`, `WalledDefenseTypes`, `WallDistanceFromTower`,
   `MaximumWallSegments`, `WallPathCheckLocomotor`. Everything else is a constant in the planner.

Net −870 lines. Cost: the once-per-base 1200-cell choke scan is gone entirely; a planning pass is
now ≤4 towers × 15 placement checks plus ≤4 floods capped at 3000 cells, ≥500 ticks apart.
`MaximumWallSegments` raised 60 → 150, because a 15-cell line spends 15 of them and 60 only covered
four towers.

## R3 · Air — local harassment loop, squad cap 5

**Status:** `IN PROGRESS` (code done, awaiting playtest) · **Branch:** `ai/air-targeting`

Three observed failures:
- Aircraft get too close to AA and die. The threat scan samples 40 of 441 map regions and only
  re-checks around the squad every 25 ticks, so mobile AA that arrives after a scan is invisible.
  **The Sheep now has anti-air**, which is a large part of why orcas are dying.
- On being threatened they flee to one of their own buildings — which on a 202x202 map with
  expansions is routinely ~70 tiles away — then fly a straight line back in. Enormous, useless,
  visibly stupid moves.
- Squads are too large for harassment.

**Root causes found (round 3 recon):**

- **The flee test scales with squad size and the squad was never capped.** `TickAirSafety` fled only
  when `aaCount * AirThreatFleeMultiplier > Units.Count`. SkyNet builds `heli: 8` + `orca: 12` and
  *all twenty* went into one squad, so at `AirThreatFleeMultiplier: 8` it took **three** anti-air
  actors to make the squad leave. One Sheep (`SheepAA`, `Burst: 2` × 6000 damage) kills an 8500 HP
  Orca in a single salvo. The squad stood there and died. This is the single biggest concrete bug.
- **Detection was too sparse in both space and time.** `AirTargetScanSamples: 40` of 441 grid regions
  is a ~8.7% chance of sampling any given region per scan, and `AirSafetyCheckInterval: 25` with
  `AirThreatScanRadius: 12` gave almost no warning: an Orca (Speed 230) covers 5.6 cells and a Sheep
  (Speed 120) 2.9 cells per interval, so a Sheep can go from 18 cells (unseen) to inside its 10-cell
  `SheepAA` range between two consecutive checks.
- **The players' "ordered away reads as empty" hypothesis: half right, wrong mechanism.**
  `FindActorsInCircle` is *not* stale — `Mobile.SetCenterPosition` calls `World.UpdateMaps` every
  tick a unit moves, `ActorMap.ITick.Tick` applies the queued bin moves in one pass, and
  `ActorsInBox` re-filters on live `CenterPosition`. Worst case is one tick (40 ms) of lag. But the
  squad's **threat memory stores positions, not actors** (`Squad.AirThreatPositions`), so a mobile AA
  unit that moves leaves a ghost where it *was* (`AirThreatMemoryTicks: 900` = 36 s) and is unknown
  where it *is* until a scan happens to cover it. The AI never tracked the Sheep, only a place.

1. `DONE` — **Local evasion replaces going home.** New `AirStateBase.Evade` + pure
   `AirThreatGeometry.EvadeDestination`: hop `AirEvadeDistance` (12) cells directly away from the
   *nearest remembered threat*, plus a uniform ±`AirEvadeJitter` (5) cell lateral wander, clamped to
   the map. Worst-case move is 12 + ~7 ≈ 19 cells versus the old ~70. `AirFleeState` and the safety
   check both call it; going home is now only for aircraft that cannot rearm in the field
   (`SendHomeToResupply`, unchanged stock ammo logic — note CNC Orcas have `ReloadAmmoPool` and no
   `Rearmable`, so in practice they never go home at all). With `AirEvadeDistance: 0` the stock
   retreat-to-an-own-building path is untouched, so `ra`/`ts`/`d2k` are unaffected.
2. `DONE` — **Re-scan and strike from the new position.** `TickAirSafety` now scores candidate
   targets in the *same* `FindActorsInCircle` pass it already ran for anti-air, so the squad can
   re-target every `AirSafetyCheckInterval` (10 ticks) instead of every `AttackForceInterval` (75)
   — at **zero extra world queries**. It only commits when that scan saw *no* anti-air at all inside
   `AirThreatScanRadius`, so the fast path can never fly into cover it just measured. When the
   map-wide scan finds nothing and the squad remembers threats (i.e. it is loitering by an enemy
   base), `AirIdleState` hops to a nearby random point and tries again — the "move around the base"
   behaviour, done the cheap way, as agreed.
3. `DONE` — **Cap air squads at 5.** New `AirSquadSize` (0 = unlimited default) and
   `MaximumAirSquads` (0 = unlimited default). `SquadSize: 10` is untouched, so ground squads are
   unchanged. `FindNewUnits` calls the new `GetAirSquadWithRoom`, which fills existing air squads in
   list order and opens a new one only while under `MaximumAirSquads`. Aircraft that fit nowhere are
   left out of `activeUnits` so they wait at base and join the moment a slot frees up. skynet:
   `AirSquadSize: 5`, `MaximumAirSquads: 2` — two independent five-plane harassment groups.
4. `DONE` — **AA detection.** `AirSafetyCheckInterval` 25 → 10 and `AirThreatScanRadius` 12 → 16
   (Sheep AA range is 10, so that is 6 cells of standoff and ~2 checks of warning at closing speed).
   Mobile AA was already covered by `IsAntiAirCapable` — the failure was frequency, radius, and the
   squad-size-scaled flee test, all three now fixed. `AirThreatMemoryTicks` 900 → 300 because a
   36-second memory of a *position* is actively misleading for mobile AA.

**Cost:** per bot per tick, per air squad — target scan `AirTargetScanSamples/AttackForceInterval`
= 24/75 = 0.32 calls, safety check 1/10 = 0.1 calls. With `MaximumAirSquads: 2` that is 0.84
calls/bot/tick, **~30 `FindActorsInCircle`/tick across 36 bots, up from ~21**. Area-weighted (cost
scales with the box, and the safety scan grew from radius 12 to 16) it is ~34 `DangerScanRadius`
-equivalents/tick versus ~21. The increase buys 2.5× the check frequency over 1.8× the area for
*two* squads instead of one; it was paid for by cutting `AirTargetScanSamples` 40 → 24, which is
affordable precisely because the safety scan now doubles as a local target search.
`MaximumAirSquads` is the knob that bounds this — the cost no longer grows with aircraft built.

5. `TODO` — **Needs human playtest.** Nobody has watched the new loop. Most likely to need tuning:
   `AirEvadeDistance: 12` / `AirEvadeJitter: 5` (if the squad still gets shot, raise the hop; if it
   wanders aimlessly, lower the jitter), `MaximumAirSquads: 2` (raise for more pressure, at linear
   CPU cost; lower to 1 if the framerate suffers), and `AirRetreatOrderInterval: 50` which bounds how
   fast the dip-in/slip-out cycle can oscillate.

> Deferred, explicitly: a separate "big push" doctrine that masses aircraft against one high-value
> structure when no exposed targets exist. Agreed to be a different behaviour for a later round.

## R3 · Sheep — more HP

**Status:** `TODO`

Sheep die too fast to defensive structures (`GTWR` fires 3300 damage every 4 ticks at range 7).
Chosen fix: raise HP substantially. Blunt, but it covers every damage source rather than only
machine guns. HP 2000 → 6000. Range, armour type and cost unchanged.

## Deferred to Round 4 — adaptive AI

Track value-killed vs value-lost per actor type; build more of what performs and less of what
doesn't, clamped so nothing reaches zero. Deliberately sequenced *after* the above so it measures
the AI we intend to ship rather than behaviour that is about to be replaced.
