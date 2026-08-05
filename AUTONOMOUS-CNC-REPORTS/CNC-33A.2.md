# CNC-33A.2: Independent Tiberium explosion options

- Status: in progress
- Cycles used: 0 of 30
- Branch: `agent/cnc33a2-tiberium-explosion-options`
- Base: green CNC-33A.1 PR #65 head `a302f673be`
- Pull request: pending

## Literal acceptance

In a full-engine match, the host can independently enable `No red Tiberium explosions` and `No blue Tiberium explosions`. With neither option enabled, all existing behavior remains unchanged. With either option enabled, every explosion attributable to that color of Tiberium is suppressed for the entire match, regardless of whether it originates in a field, cargo, a harvester, a weapon, a script, a delayed effect, a human deploy order, or an AI order. The setting remains authoritative after save/load, replay, capture, and ownership changes. Ordinary non-Tiberium weapons, deaths, and explosions must still work.

The player-visible gate is that an otherwise identical forced detonation produces normal damage/effects when its color is enabled and produces no color-attributable explosion, damage, mutation, or delayed residue when disabled. Red and blue settings must not suppress one another. Their precedence with `No mutants` is deterministic: red/blue suppression wins for that color; otherwise the no-mutants substitution behavior belongs to CNC-33A.3 and will be validated cumulatively once that option exists.

Forbidden outcomes are actor-name or map-name special cases; option state cached outside the authoritative world/lobby state; only visual suppression while damage or mutation survives; suppression of green Tiberium or ordinary weapons/deaths; a delayed explosion armed before an option/save transition bypassing the setting; ownership changes changing behavior; AI and human orders disagreeing; or release debug spam.

## Contention and source inventory

- Resource-layer reactions for red and blue fields, spread/growth, harvesting, cargo loading/unloading, and field destruction.
- Harvester cargo and death/deploy behavior, including stealth red bomb-truck missions and delayed/conditional explosions.
- Weapon projectiles, warheads, death weapons, actor traits, conditions, Lua/scripts, map rules, and queued activities that can create an explosion or mutation.
- Human deploy orders and every normal AI module able to issue, queue, cancel, retarget, or reserve the same actors, including the red bomb-truck manager and ordinary attack/harvest/repair logic.
- Save/load, replay determinism, capture/ownership changes, actors/effects created before and after option selection, and complete-match teardown.

## Plan

1. Audit engine and CNC rules/code for every red/blue explosion source and the existing lobby-option/world-condition architecture.
2. Add one authoritative, save/replay-stable world setting per color and a reusable semantic suppression gate at the narrowest common explosion boundary, with source-specific bridges only where required.
3. Add focused option, precedence, delayed-effect, save/load, ownership, order, and non-Tiberium regression tests; keep optional test instrumentation quiet in release rules.
4. Pass strict CNC build, all unit/interface/static checks, and exhaustive CNC YAML/map validation.
5. Run ordinary-bot full-engine headless MAX acceptance plus at least three distinct adversarial cycles, including independent/mixed settings, delayed/save-load/capture behavior, human/AI detonation paths, and a natural complete match; use isolated concurrent games when reliable.
6. Publish one cumulative draft PR to the CNC-33A.1 branch, wait for Linux and Windows checks, update durable state, then continue to CNC-33A.3.
