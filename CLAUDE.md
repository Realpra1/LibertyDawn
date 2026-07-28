# LibertyDawn

A fork of [OpenRA](https://github.com/OpenRA/OpenRA) that heavily rebalances and extends the
**Tiberian Dawn (`cnc`) mod**. Everything else in the repo is stock upstream.

> **Scope: we only care about `mods/cnc/`.** The `ra`, `ts`, `d2k`, and `modcontent` mods are
> untouched upstream baggage. Do not spend effort on them; do not "fix" them.

## Quick facts

| | |
|---|---|
| Upstream fork point | OpenRA `74cced319c` — **2022-01-29** |
| Fork commits | ~215, all by `Realpra1`, on branch `bleed` |
| Target framework | `net6.0` (EOL — builds fine, warns loudly) |
| Engine version string | `{DEV_VERSION}` (never stamped) |

## Build & run (macOS / Apple Silicon)

```bash
make all            # ~17s, builds clean
./launch-game.sh Game.Mod=cnc
```

### First-time native library setup

`make` does **not** wire up native deps. `./configure-system-libraries.sh` is supposed to, but it
bails on the first missing library, so nothing gets linked. On macOS you need four dylibs
symlinked into `bin/`:

```bash
brew install sdl2 openal-soft freetype

ln -sf /opt/homebrew/lib/libSDL2-2.0.0.dylib            bin/SDL2.dylib
ln -sf /opt/homebrew/opt/openal-soft/lib/libopenal.1.dylib bin/soft_oal.dylib
ln -sf /opt/homebrew/lib/libfreetype.6.dylib            bin/freetype6.dylib
ln -sf "$PWD/vendor-libs/liblua5.1.dylib"               bin/lua51.dylib
```

**Lua 5.1 is the annoying one.** Homebrew dropped `lua@5.1`; `lua@5.4` is not ABI-compatible with
Eluant. Build it from source once:

```bash
curl -sL https://www.lua.org/ftp/lua-5.1.5.tar.gz | tar xz && cd lua-5.1.5/src
make macosx
cc -dynamiclib -install_name @rpath/liblua5.1.dylib -o liblua5.1.dylib \
   $(ls *.o | grep -vE 'lua.o|luac.o') -lm
# then copy liblua5.1.dylib into <repo>/vendor-libs/
```

Game assets (`.mix` files) live in `~/Library/Application Support/OpenRA/Content/cnc/` — install via
**Manage Content** in the main menu if missing.

### Verifying a change

```bash
./utility.sh cnc --check-yaml    # lints every rule + every map; exit 0 = good
```

Run this after *any* yaml edit. It is the only real test the mod has.

## What the fork actually changed

### 1. The upgrade tech tree (the core design)

Stock TD gates units on buildings (`weap`, `afld`, `pyle`). LibertyDawn replaces most of that with
nine purchasable upgrades forming three branches:

```
upgrade.economy1 → economy2 → economy3
upgrade.recon1   → recon2   → recon3
upgrade.covert1  → covert2  → covert3
```

Units now list e.g. `Prerequisites: anyhq, upgrade.covert2, ~techlevel.medium`. Branches are
*intended* to be mutually exclusive via negated prerequisites (`~!upgrade.covert2`) — but only
**one** such exclusion actually exists in the whole ruleset (`mods/cnc/rules/vehicles.yaml`). The
rest of the "pick a branch" design is aspirational.

### 2. Rewritten resource simulation

`OpenRA.Mods.Common/Traits/World/ResourceLayer.cs` grew **304 → 932 lines**. Tiberium now has
density stages, spread intervals, evolution between types, and a `RedTiberium` variant that
detonates (`ExplosionChance: 100`, weapons `AtomicTib` / `TiberiumMeteor`) and leaves behind
`BlueTiberium, Tiberium, Nothing`. Configured in `mods/cnc/rules/world.yaml`.

Supporting new engine code:
- `OpenRA.Game/FastUniqueQueue.cs` + `FastQueueEntry.cs` — hand-rolled ordered dictionary (290
  lines). Used in exactly one place: `GrantConditionInRange.cs`.
- `Traits/ModifiesResources.cs` — buildings that locally alter tiberium spread/growth rates.
- `Traits/World/SeedsResource.cs`, `Harvester.cs`, `HarvestResource.cs` — adapted to the above.

### 3. New engine traits

| File | Purpose | Status |
|---|---|---|
| `Traits/Conditions/GrantConditionInRange.cs` | Aura conditions (stealth gen, repair gen) | Used |
| `Traits/Modifiers/Blink.cs` | Flash an actor on a condition | Used — **buggy, see below** |
| `Traits/Conditions/SpreadsCondition.cs` | Infectious condition spread | **Dead code, 0 usages** |
| `Traits/ModifiesResources.cs` | Local resource-rate modifier | Used (2 sites) |

Also patched: `FreeActor` now supports `Prerequisites`; `Captures` allows capturing tech buildings;
`GrantConditionOnPrerequisite` / `TechTree` tweaks for the upgrade system.

### 4. Content additions

New units/buildings with imported art in `mods/cnc/bits/`: Stealth Tank (`stealth.shp`), Chemical
Tank (`ctnk.shp`), Stealth Harvester, Resonator (`resonator.shp`). New maps: `archipelago`,
`Empire-Earth.oramap`, `Red Dawn.oramap`. Campaign mission `rules.yaml` overrides were stripped.

### 5. AI

`mods/cnc/rules/ai.yaml` grew ~1200 lines to teach the bot the upgrade branches. Several commits
are explicitly "AI adjust" / "AI bug fix" with no further detail.

## Known landmines

- **`Blink.cs` ignores `BlinkColor`.** The constructor reads
  `if (!Color.TryParse(info.BlinkColor, out var c)) color = c;` — the `!` is inverted, so a *valid*
  color is discarded (falls back to white) and an *invalid* one assigns transparent. Both configured
  sites (`mods/cnc/rules/vehicles.yaml:109,189`, `BlinkColor: FF9999`) are silently ignored. The
  identical parse in `ResourceLayer.cs:651` is written correctly.
- **`SpreadsCondition` probability is off by one** — `SharedRandom.Next(100) > Probability` gives
  `Probability + 1` percent. It also doesn't exclude `self` from `FindActorsInCircle`, so an actor
  can infect itself. Moot for now since nothing uses the trait.
- **Mixed tabs and spaces in the yaml.** ~843 space-indented lines across `mods/cnc/`, sometimes
  alternating line-by-line inside a single block (see `mods/cnc/rules/world.yaml:246-252`). OpenRA's
  MiniYaml tolerates it; the repo's own `.editorconfig` and `OpenRA.ruleset` do not. **Use tabs for
  new lines** and don't reflow surrounding lines — the churn makes diffs unreadable.
- **`global mix database.dat`** (412 KB) is committed to the repo root and not gitignored.
- The fork is **3.5 years behind upstream**. Rebasing is not realistic; assume any OpenRA docs or
  wiki page you find describes newer trait APIs than what's here.

## Conventions

- Rules live in `mods/cnc/rules/`, weapons in `mods/cnc/weapons/`, art definitions in
  `mods/cnc/sequences/`, sprites in `mods/cnc/bits/`.
- Balance changes are yaml-only. Anything requiring new behavior needs a C# trait in
  `OpenRA.Mods.Common/Traits/` — prefer extending an existing trait over adding a new one.
- Always run `./utility.sh cnc --check-yaml` before declaring a change done.
