# Task packet
- Task ID: CNC-51
- Title: Transport-helicopter unload recovery and threat-safe landing
- Status at selection: pending
- Required base/prerequisites: Use the coordinated round's recorded shared base. No explicit prerequisite is stated in the task sheet.
- Related active tasks or PRs: CNC-39, CNC-39A, CNC-43, and CNC-43A are claimed in this round but have no stated functional dependency. Closely related completed work: CNC-21 (transport recovery) and CNC-25 (Mammoth drop). Later pending transport follow-ups CNC-65 and CNC-65A share passenger/loading and post-unload behavior. No active CNC-51 branch or PR was found at selection.
- Cross-worker concern: This task changes shared helicopter transport landing/unload and threat-routing behavior that completed CNC-21/CNC-25 established. Preserve their recovery, carrier limits, concurrent pickup, heavy-drop handoff, and ordinary transport behavior; avoid absorbing the later APC-specific CNC-65/CNC-65A scope. Another worker's commits may materially influence this task if they touch shared transport or threat-map code.

## Authoritative task text

**Transport-helicopter unload recovery and threat-safe landing.** Rescue, assault, and heavy-drop helicopters must not stall because an actor occupies the requested landing/unload cell. Before unloading, deterministically search nearby for an accessible cell with valid adjacent passenger exit space; it need not be the exact destination. Because landed helicopters are susceptible to every applicable damage type, evaluate each stopping/landing cell and its exit area against all live enemy weapons able to damage the carrier at landing altitude—not only actors classified as AA. This combined landing threat must include Mammoth tanks as well as MSAMs, use true/veterancy range and mobile-threat movement margins, and never use a fly-by discount. Transport attack approaches must demonstrably use both the threat-aware router and the coarse strategic map to select a safe approach vector and landing area instead of flying directly to a target cell. Re-evaluate when actors, structures, or new threats block/compromise the site; route to another safe unload cell or hold/withdraw instead of landing beside an MSAM, Mammoth, or equivalent threat. Add timeout/stall/unsafe-site diagnostics that log rejected threats and whether strategic routing was used. Test simultaneous transports, dense friendly units, structures, map edges, targets blocked in flight, newly arriving mobile/static AA, Mammoth cannons/missiles, and threats covering only one approach vector. The preserved manual log records three rescue missions timing out while still carrying cargo; a later Archipelago test visibly landed carriers beside both an MSAM and enemy Mammoth tanks.

## Relevant linked notes

- CNC-21, **Transport recovery**, is complete: after persistent route failure it transports eligible units by helicopter, caps transport helicopters at ten, uses the air-AI threat model or least-dangerous route, stations idle carriers safely, and repairs damaged carriers.
- CNC-25, **Mammoth drop**, is complete: its concurrent pickup correction sends each selected carrier to a distinct free pickup cell and waits for it to be exactly positioned and landed before boarding; successful unload hands surviving passengers to one assault squad, while safe abort restores ordinary eligibility. Preserve the reported evidence/history in `AUTONOMOUS-CNC-REPORTS/CNC-25.md` and its listed Archipelago logs.
- CNC-65 and CNC-65A are later pending APC transport tasks. They must not be subsumed; their APC passenger composition, specialist safety, and normal-squad handoff requirements remain distinct.

## Relevant deferred constraints

- The transport-testing deferred note records a CNC-37 contention fixture where a Chinook repeatedly issued `Unload` at its drop cell while its passenger remained inside. Revalidate vehicle unloading and rate-limit unchanged retry diagnostics in a future transport pass.
- Keep autonomous launchers on validated packaged maps; a stale local `TibTest.oramap` lacking `map.yaml` previously caused launcher startup failure.
- Work only on LibertyDawn Command & Conquer; do not broaden into unsupported mods.

## Selection rationale

CNC-51 is the first eligible pending task in task-sheet order after the coordinator-excluded/claimed CNC-39, CNC-39A, CNC-43, and CNC-43A entries. It is prioritized bug/polish work with no stated prerequisite, user-question gate, active CNC-51 PR, or pinned-final restriction. CNC-26C remains permanently pinned final and ineligible.
