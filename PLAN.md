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

**Status:** `TODO` · **Branch:** `ai/air-targeting` · **Worktree:** `../ld-air`

Right now SkyNet's orcas and helis (`AirUnitsTypes: heli, orca`) get folded into generic attack
squads and fly at whatever the squad is fighting — usually straight into SAM sites.

**We want:** air units that pick off *undefended* things. Lone tanks out of AA cover, harvesters at
the tiberium field, production buildings on the unprotected side of a base.

### Steps

1. `TODO` — **Recon.** Read `OpenRA.Mods.Common/Traits/BotModules/Squads/States/AirStateBase.cs`
   and `AirStates.cs`. The engine already ships `FindDefenselessTarget()` and AA-proximity scanning;
   establish exactly what it does today and why SkyNet isn't benefiting. **Report before writing
   code** — this may be more of a tuning problem than a code problem.
2. `TODO` — Add target *scoring* rather than first-match: weight harvesters and undamaged
   production buildings above generic units, and penalise anything within AA range.
3. `TODO` — Expose the weights as yaml fields on `SquadManagerBotModule` so we can tune without
   recompiling.
4. `TODO` — Wire the new fields into `SquadManagerBotModule@skynet` in `mods/cnc/rules/ai.yaml`.
5. `TODO` — Verify: build, `--check-yaml`, then a skirmish where we leave a harvester exposed and
   confirm the orcas go for it.

**Risk:** bot code runs inside the deterministic simulation. Anything non-deterministic here causes
multiplayer desyncs. Use `World.SharedRandom` only — never `Math.Random`.

---

## Workstream 2 — SkyNet walls its base and towers

**Status:** `TODO` · **Branch:** `ai/walls` · **Worktree:** `../ld-walls`

SkyNet builds no walls at all. `sbag` / `cycl` / `brik` appear nowhere in its `BuildingLimits`,
`BuildingFractions`, or `DefenseQueues`.

**We want:** concrete walls (`brik`) ringing its defensive towers and the base perimeter.

### Steps

1. `TODO` — **Recon.** Confirm `brik` prerequisites and which production queue it belongs to.
   Read `BaseBuilderQueueManager.cs` (the fork has already modified it, +31 lines) to see how
   placement currently works.
2. `TODO` — Establish whether `BaseBuilderBotModule` can place walls usefully at all. It places
   buildings on a grid; walls need *line* placement to be worth anything. Expect this to need new
   C# — likely a `WallTypes` config plus a placement routine that rings an existing defense.
3. `TODO` — Implement placement: ring each finished tower, then close obvious gaps in the base
   perimeter. Cap total wall segments so it doesn't bankrupt itself.
4. `TODO` — **Leave gates.** Walls must never fully enclose the base or the AI seals itself in and
   its own harvesters, MCVs and attack squads can't get out. Requirements:
   - Every ring leaves at least one deliberate gap, sized for the widest unit footprint.
   - Gaps face outward toward the map, not into a cliff or the sea.
   - After placement, verify a path still exists from the construction yard to the nearest
     tiberium field and to the map edge. If it doesn't, don't place the segment.
   - This is the single most likely way this workstream makes the AI *worse*. Test it explicitly.
5. `TODO` — Add `brik` to SkyNet's build config in `ai.yaml` with sane limits and delays.
6. `TODO` — **More towers.** SkyNet's `BuildingLimits` currently allows `gtwr: 1` and `gun: 1`
   against `atwr: 40`. Raise the cheap towers to match the spirit of the thing — the AI should be
   allowed to spam defences the way we spam everything else. Suggested `gtwr: 25`, `gun: 25`, and
   nudge their `BuildingFractions` up from `1` so it actually picks them. Tune after playtest.
7. `TODO` — Verify: build, `--check-yaml`, skirmish on Empire Earth, screenshot the AI base and
   confirm the walls form lines rather than confetti — and that harvesters still reach tiberium.

**Risks:** an AI that spends its whole income on walls is a worse opponent, not a better one — the
budget cap is not optional. And an AI that walls itself in is a *dead* opponent. Gaps are a hard
requirement, not a nice-to-have.

---

## Workstream 3 — The Sheep

**Status:** `TODO` · **Branch:** `unit/sheep` · **Worktree:** `../ld-sheep`

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

1. `TODO` — **Art decision.** There is no sheep sprite. Options, cheapest first:
   (a) reuse an existing civilian infantry `.shp` with a white/pale remap palette;
   (b) reuse one of the CNC Funpark dinosaur sprites (`steg`, `tric`) — arguably weirder;
   (c) import a sprite the way the fork did for `stealth.shp` / `ctnk.shp`.
   **Bring me the options with a screenshot before committing to one.**
2. `TODO` — Actor definition in `mods/cnc/rules/infantry.yaml`. Infantry locomotor, no weapon,
   high `RevealsShroud`.
3. `TODO` — Sequence definition in `mods/cnc/sequences/` for whichever sprite we pick.
4. `TODO` — "Steak" death effect in `mods/cnc/weapons/explosions.yaml` — zero damage, cosmetic only.
5. `TODO` — Add to the `Infantry.GDI` and `Infantry.Nod` build queues. No prerequisites.
6. `TODO` — **Do not** add it to SkyNet's `UnitsToBuild`. The AI building sheep is not the joke.
7. `TODO` — Verify: build, `--check-yaml`, then buy one and confirm it renders, moves, scouts,
   and dies correctly.

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
