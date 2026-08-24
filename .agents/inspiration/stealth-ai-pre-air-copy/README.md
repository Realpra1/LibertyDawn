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
