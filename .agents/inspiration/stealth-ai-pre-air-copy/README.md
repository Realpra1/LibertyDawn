# Pre-Air-copy stealth specialist inspiration

This passive, noncompiled archive preserves the bespoke STNK/CTNK specialist
implementation exactly as it existed at source head
`609ebf92eeac565af51b2b6acd53de37bb3b39d2` (2026-08-24).

The files retain their original repository-relative paths beneath this folder.
They are inactive inspiration only: the C# files are outside all project roots,
and the archived YAML snippet is not loaded by the CNC mod.

Archived specialist-exclusive files:

- `OpenRA.Mods.Common/Traits/BotModules/StealthTankSquadBotModule.cs`
- `OpenRA.Mods.Common/Traits/BotModules/BotModuleLogic/StealthTankSquadPolicy.cs`
- `OpenRA.Mods.Common/Traits/BotOwnedStationaryWatchdog.cs`
- `OpenRA.Test/OpenRA.Mods.Common/StealthTankSquadPolicyTest.cs`
- specialist-only members from the mixed `CncAirSquadConfigurationTest.cs` and
  `ThreatAwareRoutePlannerTest.cs` fixtures, preserved as adjacent snippets
- `mods/cnc/rules/ai.yaml.specialist-snippet`

The original repeated `AdvancedSquadModules` ownership entry and both exact
specialist trait blocks are preserved in the YAML snippet.

Shared Air code, route-planning utilities, mixed-purpose tests, and unrelated
engine code are intentionally not archived here.

The mechanically renamed Air copies are checked by
`scripts/check-stealth-ai-air-copy.py`. That check reverses only the declared
type-identity substitutions and requires exact full-file equality with each Air
source.

## Ownership correction

`air-derived-nonowning-reference/StealthAIModule.cs.inspiration` and
`air-derived-nonowning-reference/StealthAISquad.cs.inspiration` preserve the two
mechanical copies that were initially staged as possible live owners. They are
deliberately outside compilation: the existing `SquadManagerBotModule` remains
the sole manager and owner of its `List<Squad>`, and specialist groups are
ordinary `Squad` instances. Only the Air-derived states and threat geometry are
adapted for live use against those original owner types.

`FINAL-MATRIX.json` is the immutable 184-function disposition matrix used by
the integration. `live-provenance/` preserves the independent post-integration
audit and its per-ID live-body map: all 98 restore IDs are mapped, the 51 Air
bodies remain authoritative, and all 35 retreat bodies remain excluded.
