# Deferred Work

## AI allied supply production

- Location: `UnitBuilderBotModule` and `SpecialOrderBotModule`.
- Impact: Existing idle supply trucks are now delivered to economically stranded allies, but the AI does not yet request production specifically for allied rescue.
- Evidence: Production requests currently have no per-ally rescue state or per-factory/airfield five-minute quota.
- Next action: add a typed rescue-production request to `UnitBuilderBotModule`, capped at one truck per 7,500 ticks for each currently available vehicle factory or airfield; cancel pending requests once the ally gains cash, a harvester/refinery, or loses every production building, MCV, and mobile unit.
