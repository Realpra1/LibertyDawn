# Worker State: CNC-43A

Reread this file after context compaction, before every code-change cycle, after
test results arrive, and before publication. This is the complete assigned work
contract. Do not read the full task sheet, coordinator state, or another worker's
spec. Read applicable `AGENTS.md`. Inspect another worker's named PR commits only
when the dependency section directs it.

## Assignment

- Worker: `worker-4-cnc-43a`
- Task: `CNC-43A — Flame Tank balance`
- Status: `Complete - testing`
- Common base branch/SHA: `agent/cnc38-early-viki-infantry-rush` / `09ccdac3c1ecb5134a4751f2bcbd8a7970dfe6bf`
- Task branch: `agent/round-20260806-cnc43a-flame-tank-balance`
- Intended PR base: `agent/cnc38-early-viki-infantry-rush`
- Cycle budget: `20` isolated code-change cycles
- Cycles used: `2`
- Game/build lock directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260806-bug-polish-01/locks`
- Game capacity: `2`
- Large-build capacity: `1`
- Task report: `/root/github/LibertyDawn/COORDINATED-CNC-ROUNDS/20260806-bug-polish-01/WORKER-4-CNC-43A/REPORT.md`
- Match-analysis directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260806-bug-polish-01/analysis/worker-4-cnc-43a`
- Liberty Dawn design reference: `.agents/references/LIBERTY-DAWN-DESIGN.md`
- Full-engine game tests completed: `40` (including labeled diagnostic/invalid runs that advanced far enough to expose evidence; four tick-0 packaging failures excluded)
- Sol-xhigh policy escalation: `unused (requires at least 10 game tests; one maximum)`
- PR: `#79 https://github.com/Realpra1/LibertyDawn/pull/79`

## Integrated repair assignment

- Phase: `isolated implementation`
- Current release branch/head: `not assigned`
- Integration notes: `not assigned`
- Repair branch: `not assigned`
- Repair PR base: `not assigned`
- Integrated cycles used this RC: `0/3`
- Integrated cycles used total: `0/12`

Before relaunching this worker for combined testing or repair, the integrator must
replace these fields with the exact release head, note path, branch, and counters.
During that phase, the repair branch replaces the original task branch as the
writable branch; the task scope and behavioral contract do not change.

## Why and predicted change

CNC-43A exists because the Recon II Flame Tank is intended to be a fast,
short-range assault unit with good health, but its current survivability and
damage into true tanks are to be raised by two exact, literal amounts. This is a
bounded Command & Conquer content task: increase Flame Tank health by 20% and
its damage against tank armor by 10%, preserve its damage against every other
armor class, and make no unrelated unit or AI changes.

The predicted observable change is that every newly created rookie `FTNK` has
36,000 maximum HP instead of 30,000. At full damage falloff, each of the seven
resolved `BigFlamer` damage pulses deals 396 rather than 360 damage to Heavy
armor, so both projectiles in one complete burst deal 5,544 rather than 5,040
total Heavy damage after all delayed pulses resolve. All non-Heavy damage,
weapon timing, cost, tech access, AI decisions, and the Flame Tank's death
explosion remain control-identical. The stronger health and Heavy damage may
combine to roughly 32% more damage-exchange capacity during continuous trades;
tests must prove that true tanks, range, aircraft, screening, and defensive fire
remain meaningful counters.

## Authoritative behavior

- Set `FTNK` `Health.HP` from 30,000 to exactly 36,000 at every normal load path,
  including multiplayer/skirmish and campaign maps that inherit the default
  actor. Keep the existing Light armor type.
- Increase only the resolved `BigFlamer` attack's Heavy-armor modifier from 20
  to exactly 22 on all seven `SpreadDamage` warheads at delays 0, 25, 50, 75,
  100, 125, and 150. This is a relative 10% modifier increase.
- Keep each resolved attack warhead's raw damage at 1,800, spread at 750, delay,
  falloff, target filters, and damage types unchanged. Keep burst 2, burst delay
  10, reload 65, range `3c512`, and projectile speed 341 unchanged.
- At full falloff, preserve complete two-projectile burst damage exactly at
  25,200 versus None, 30,240 versus Wood, 30,240 versus TiberiumWood, 17,640
  versus Light, and 5,040 versus Tiberium. Heavy alone changes from 5,040 to
  5,544. These totals assume rookie actors, no handicap or other modifiers,
  identical hit geometry, and all fourteen pulse impacts completing.
- Keep `^FlametankExplode`, including the on-death explosion selected by both
  `Explodes.Weapon` and `Explodes.EmptyWeapon`, control-identical. It remains at
  Heavy 20. Keep `^FlameWeapon`, `^FlamethrowerExplode`, Flamethrower,
  Chemspray, BigChem, Napalm, Chemical Tank, and every other weapon/unit
  control-identical.
- Keep Flame Tank cost 600, `Repairable.HpPerStep` 2,046, speed 92, Light armor,
  build palette order, Recon II/Covert II prerequisites, queues, description,
  vision, cloak/recon traits, death/husk behavior, AI weights/limits/delays,
  squad assignment, target policy, and production/economy policy unchanged.
- The player-visible role remains a Recon II assault/finisher: better able to
  reach and clear infantry, light vehicles, and structures, and less futile
  when a true tank arrives, but not a new general-purpose tank counter.

## Forbidden behavior and failure signals

- A raw `Damage` increase, reload/burst change, or shared-template edit that
  changes any non-Heavy matchup is a failure.
- Changing only `Warhead@1Dam` is a failure: `BigFlamer` has seven delayed
  damage warheads and the final Heavy result must be +10%, not a first-pulse-only
  fraction of that.
- Treating “10%” as ten percentage points (`Heavy: 30`) is a 50% damage increase
  and is forbidden. The required resolved value is 22.
- Changing `^FlametankExplode` would also buff the Flame Tank death explosion;
  that suicide/chain-reaction collateral is forbidden. The death explosion must
  retain Heavy 20 and all previous resolved values.
- Changing shared `^FlameWeapon` or `^FlamethrowerExplode` would affect infantry
  flamers, chemical weapons, and/or other armor classes and is forbidden.
- Do not scale `Repairable.HpPerStep`, cost, build time, armor, speed, range,
  prerequisites, tooltip text, AI production, targeting, or squads to compensate
  for the requested health change. Record any observed follow-up concern as
  deferred work instead of broadening scope.
- Equal-credit changed Flame Tanks routinely defeating unsupported Light Tanks,
  Medium Tanks, or Mammoth Tanks in straightforward open-ground head-on fights
  is a balance failure signal. So is an unsupported Flame Tank rush turning a
  control hard-stop under fixed defenses into a routine breakthrough, or range,
  kiting, aircraft, focus fire, and screening ceasing to trade efficiently.
- A resolved-YAML claim, attack request, projectile, pulse log, or AI production
  event without final max HP and final target HP after all delayed pulses is not
  black-box acceptance.
- Do not edit product C# code, Red Alert, Dune 2000, Tiberian Sun, the task sheet,
  coordinator state, another worker state/report, or any unrelated content.

## Relevant current implementation and control behavior

At pinned base `09ccdac3c1ecb5134a4751f2bcbd8a7970dfe6bf`:

- `mods/cnc/rules/vehicles.yaml` owns `FTNK`. It inherits `^Tank`, has cost 600,
  Recon II prerequisites, Light armor, `Health.HP: 30000`,
  `Repairable.HpPerStep: 2046`, speed 92, and one armament using `BigFlamer`.
  Its death path uses `^FlametankExplode` and spawns `FTNK.Husk`.
- Campaign `rules.yaml` files that mention `FTNK` only disable its production;
  none overrides health or armament, so the default actor value reaches existing
  placed Flame Tanks. Placed FTNK actors also exist in CNC campaign content.
- `mods/cnc/weapons/other.yaml` owns `BigFlamer`. It first inherits
  `^FlameWeapon`, then `^FlametankExplode`; the later parent replaces/extends
  matching nested damage nodes. Do not reason from `^FlameWeapon`'s raw 786
  damage: `./utility.sh cnc --resolved-weapons BigFlamer` proves the effective
  attack has seven 1,800-damage, spread-750 warheads with Heavy 20 and delays
  0/25/50/75/100/125/150, plus burst 2.
- `mods/cnc/weapons/explosions.yaml` owns `^FlametankExplode`, which inherits the
  seven pulse definitions from `^FlamethrowerExplode`, raises their raw damage
  and spread, and is consumed both by `BigFlamer` and FTNK death. MiniYaml merges
  nested overrides recursively (`OpenRA.Game/MiniYaml.cs` and its tests), so a
  `BigFlamer`-local Heavy child override can preserve sibling armor modifiers;
  the final resolved output, not indentation intuition, is authoritative.
- `DamageWarhead` applies the matching `Armor.Type` percentage, and
  `Util.ApplyPercentageModifiers` multiplies decimal percentages then truncates
  to `int`. With raw 1,800, Heavy 20 is 360 per pulse and Heavy 22 is 396; seven
  pulses times two projectiles are exactly 5,040 and 5,544.
- Heavy is the tank armor used by Light Tank, Medium Tank, and Mammoth Tank;
  FTNK itself remains Light. The design reference says Flame Tank destroys
  infantry, buildings, and light vehicles but struggles against true tanks.
- History shows FTNK HP last rose from 27,000 to 30,000 in `73b0854bec`, armor
  changed Heavy to Light in `7bc4659778`, and flame damage was consolidated into
  delayed shared explosion templates in `395b34ebcc`. That consolidation is why
  a superficially small shared edit has broad consumers today.
- `make test` builds the supported CNC product and runs
  `./utility.sh cnc --check-yaml`; the utility also exposes
  `--resolved-rules FTNK` and `--resolved-weapons BigFlamer` for exact merged
  evidence. There is no existing task-specific FTNK test.
- The named base branch currently has open PR #77 and has advanced to
  `96ca6049b586ec0a19907588168f6734490f12d6` with coordination artifacts and a
  role-launcher change after the pin; its product content is unchanged from the
  recorded base for the files in this task. Implement from the recorded SHA and
  use the named branch as PR base as assigned.

## Likely wrong approaches and challenges

- Editing `^FlameWeapon` Heavy 20 to 22 is attractive but wrong: it broadens the
  change to every inheriting flame/chemical consumer and still does not express
  the task as Flame Tank attack ownership.
- Editing `^FlametankExplode` is also wrong because the same template is the
  on-death weapon. That would silently reward contact deaths and massed chain
  reactions, not merely the normal attack.
- Overriding only the first damage warhead under `BigFlamer` misses six delayed
  pulses. Conversely, duplicating all raw damage and every armor modifier risks
  drifting shared flame behavior. Prefer the smallest BigFlamer-local Heavy-only
  override for all seven existing warhead identities, or an equally isolated
  content design whose resolved output proves the same ownership.
- Changing raw damage from 1,800 would raise None, Wood, TiberiumWood, Light, and
  Tiberium damage. Changing Heavy from 20 to 30 misreads a relative percentage.
- Assuming source text alone establishes effective values is unsafe because two
  inherited templates merge matching nested warheads. Capture resolved rules
  and weapons before and after.
- Measuring the immediate projectile impact misses damage through tick 150 and
  the second projectile. Sample final health only after both shots and all seven
  pulses have resolved; log individual test-target health transitions when
  diagnosing.
- Comparing unlike hit shapes, offsets, veterancy ranks, player handicaps,
  facings, terrain, splash neighbors, repair auras, or starting distances can
  manufacture a damage difference. Use identical test actors/hit shapes and
  matched manifests, and isolate collateral before mixed combat.
- Automatically scaling repair per step to preserve repair time is outside the
  literal request. The existing 2,046 stays fixed, so a full repair naturally
  takes more steps; report this expected consequence rather than hiding it.
- Repeating a successful scripted duel is weak evidence. After the first
  full-engine smoke, increase unit count, approach distance, geometry, defensive
  pressure, target selection, normal queue/cash contention, and save/load state.
- Letting a test-only map replace ordinary AI behavior is forbidden. It may
  place actors, issue one bounded starting order, and log exact health, but both
  sides must use ordinary supported bot types with their normal modules, and
  acceptance also requires natural/mixed games.
- Adding product diagnostics or a broad regression fixture for two content
  values is likely needless churn. Use existing resolved-rule commands and
  bounded ignored analysis-map logging unless evidence is genuinely
  indistinguishable; remove any noisy temporary diagnostics before publication.

## Competing systems and ownership

Configuration ownership is deliberately narrow:

- `mods/cnc/rules/vehicles.yaml` owns FTNK health and must be the only actor-rules
  product file needed. `mods/cnc/weapons/other.yaml` owns the `BigFlamer`
  specialization and is the appropriate attack-only boundary. The shared flame
  and death templates in `mods/cnc/weapons/explosions.yaml` are consumers to
  protect, not policy to change.
- `BigFlamer` consumes the same seven-pulse `^FlametankExplode` content that FTNK
  death consumes. Flamethrower, Chemspray, BigChem, Napalm, and Chemical Tank
  share nearby/base policy. Resolved comparison must prove none changed.
- `UnitBuilderBotModule` can produce FTNK through shared `Vehicle.Nod` and
  `Vehicle.GDI` queues. Cabal, Watson, HAL9001, Brutalis/Wavemaker, Skynet,
  IronReaper, Easy, and Easiest configure FTNK production weights; several also
  configure FTNK caps and 5,000-tick delays. These compete with harvesters,
  other vehicles, infantry, aircraft, upgrades, shared cash, unit caps, and
  queued/external requests. VIKI lists an FTNK delay but has no FTNK build
  weight in its current Covert-focused composition.
- Recon II (`upgrade.recon2`) enables FTNK, while Covert II disables it.
  Base/economy/technology behavior, smart-economy cash reservation, production
  availability, and queue contention determine whether ordinary bots actually
  field it. A game where no FTNK is produced does not exercise the task.
- `SquadManagerBotModule` consumes eligible idle FTNK into protection and normal
  ground attack squads; FTNK is not in ordinary exclusion lists. Ground states
  issue Stop, AttackMove, Attack, regroup, flee, and strategic retarget orders.
  FTNK also has `^AutoTargetGroundAssaultMove`, so stance/autotarget may choose a
  nearby actor while a squad is moving. Tests must distinguish the requested
  Heavy target from retarget/splash collateral.
- Enemy `StealthTankSquadBotModule` policy explicitly values FTNK as an attack
  target (priority 6,500). Normal enemy squads, strategic target selection,
  focus fire, aircraft, and defenses also consume FTNK health. These remain
  unchanged but are important counter pressure.
- `Repairable` lets player-issued FTNK repair orders consume a `fix` facility;
  the configured step is 2,046. Ordinary ground squads do not add a dedicated
  repair lifecycle in the inspected ground state path, but repair auras,
  veterancy self-heal, Recon repair generation, crate/handicap multipliers, and
  player orders can alter observed health. Disable/exclude them from exact
  damage measurements, then exercise ordinary recovery context separately.
- The death explosion, husk spawn, campaign placed actors, transport passenger
  capacity, crush behavior, cloak/repair-gen conditions, experience firepower
  and damage modifiers, and map actor overrides are player-visible consumers of
  the same actor. They must remain control-identical outside the requested max
  HP and normal attack Heavy modifier.
- No routing, transport, reservation, persistence algorithm, or hot-path code is
  changed. This is content resolution plus normal combat; algorithmic changes,
  new scans/allocations, and AI policy edits are outside the ownership boundary.

## Cross-worker dependencies

- CNC-43 (MCV crush flavor) is the adjacent, claimed configuration-only task.
  Its worktree is currently on
  `agent/round-20260806-cnc43-mcv-crush-flavor`, with no implementation commit
  or open PR at spec time. It is expected to touch the same
  `mods/cnc/rules/vehicles.yaml` file near `MCV`, while CNC-43A touches `FTNK`.
  Before implementing and again before publication, inspect that branch/PR's
  commits (not its worker state/spec), preserve its MCV-only changes, and resolve
  only a real textual/content overlap. Do not absorb MCV crush scope.
- No CNC-43A-specific active branch or PR existed at spec time and the task
  packet states no prerequisite. `mods/cnc/weapons/other.yaml` has no named
  cross-worker overlap.
- The intended PR base `agent/cnc38-early-viki-infantry-rush` currently points to
  `96ca6049b586ec0a19907588168f6734490f12d6` (open PR #77), while the coordinated
  common base is deliberately pinned at `09ccdac3c1ecb5134a4751f2bcbd8a7970dfe6bf`.
  The intervening branch changes are coordination/launcher artifacts rather
  than relevant CNC product content. Do not silently rebase away from the
  recorded common SHA; follow coordinator/integrator branch instructions and
  report any later product overlap.

If this section names another task PR, inspect that PR's commits while working and
before publication. Do not read its worker spec.

## Spec-time policy consultation

- Proposed-policy narrative: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260806-bug-polish-01/analysis/worker-4-cnc-43a/spec-policy/PROPOSED-POLICY.md`
- Sol-high policy review: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260806-bug-polish-01/analysis/worker-4-cnc-43a/spec-policy/POLICY-REVIEW.md`
- Verdict and confidence: `sensible; medium confidence`
- Recommendations adopted as testable hypotheses: `The combined buffs may yield roughly 32% more exchange capacity; equal-credit massed FTNK must still lose normal open-ground head-on fights to Light/Medium/Mammoth Tanks. Test one-lane and two-direction focus fire, fixed defensive-fire approach thresholds, long-range/kiting and aircraft counters, then mixed armies. Measure starting credits/forces, every delayed pulse, completed bursts, target damage, final survivors and HP. Require an intended assault case where changed FTNK survives or completes useful work that control narrowly misses, while death explosion and non-Heavy damage remain exact parity.`
- Recommendations rejected or deferred, with reason: `No test recommendation was rejected. Changing AI production/composition in response to a stronger unit is deferred because the authoritative task forbids AI changes; if mixed games make FTNK the preferred answer to every ground force, report a balance failure/risk rather than compensating out of scope. A required overall match-win improvement is not imposed because this is a literal non-strategic content adjustment; task-relevant damage, survival, counter integrity, and non-regression are the comparative gates.`

## Acceptance and tests

### Literal black-box acceptance

Run a fresh full-engine CNC scenario, not a manager-only/unit fixture, with an
ordinary real AI on each side and all relevant normal player, production,
economy, squad, targeting, movement, combat, damage, experience, death, and
repair modules loaded. A focused ignored analysis map may place identical
rookie test actors and issue a single attack order to accelerate the event, but
must not replace the ordinary bots or combat path.

Using no handicap, veterancy, firepower/damage modifier, repair aura, terrain
modifier, or neighboring splash victim, prove all of the following from final
actor health after the last delayed pulse:

1. A fresh changed `FTNK` reports `MaxHealth = 36000` and starts at 36,000/36,000;
   the matched base control reports 30,000/30,000.
2. A `BigFlamer` full two-projectile burst at full falloff against an identical
   high-health Heavy test target causes exactly 5,544 changed damage after all
   fourteen pulse hits, versus exactly 5,040 at base. Each changed pulse is 396
   versus 360 control and all seven delay identities must occur for each shot.
3. On identical None, Wood, TiberiumWood, Light, and Tiberium test actors with
   the same hit shape, the complete changed/control burst results are exact
   parity at 25,200 / 30,240 / 30,240 / 17,640 / 5,040 respectively.
4. Killing an otherwise identical FTNK beside a Heavy test target produces the
   same final target HP in changed and control, proving the on-death
   `^FlametankExplode` remains Heavy 20 rather than receiving the attack buff.
5. The run evidence names the commit/build/content checksum, map/hash, seed,
   factions, start slots, ordinary bot types, options, actor IDs/types/owners,
   initial and final HP, order tick, pulse/damage ticks, final sample tick,
   Headless MAX activation, advancing world tick, clean exit, and log/replay
   artifact paths. A source diff or resolved-YAML dump supports but does not
   replace this player-visible full-engine result.

### Focused checks and instrumentation

Before the first edit, save base-control outputs under the ignored analysis
directory for:

- `./utility.sh cnc --resolved-rules FTNK`
- `./utility.sh cnc --resolved-weapons BigFlamer`
- `./utility.sh cnc --resolved-weapons '^FlametankExplode'`
- resolved Flamethrower, Chemspray, BigChem, and Napalm output or stable focused
  checksums sufficient to prove they did not change.

After every relevant edit, run `git diff --check`, then the same resolved
commands. Assert, rather than eyeball, that FTNK HP is exactly 36,000; each of
`Warhead@1Dam` through `Warhead@7Dam` under resolved `BigFlamer` has Heavy 22;
all seven remain raw 1,800/spread 750 with the same delays and siblings; and the
death explosion/related weapons match the saved control byte-for-byte or by a
documented normalization that excludes no behavioral field. Confirm the product
diff is restricted to FTNK health in `mods/cnc/rules/vehicles.yaml` and
BigFlamer attack-only Heavy policy in `mods/cnc/weapons/other.yaml` (plus this
state/report and ignored evidence). Run `make test` before publication; it must
build supported CNC content and pass `./utility.sh cnc --check-yaml` with no new
error or warning. Run the broad required CI/check targets recorded by the PR.

Use a test-map-local bounded health recorder only when needed. For each observed
test actor, log scenario ID, tick, actor ID/type/owner/armor, attacker and weapon,
pre/post health, damage delta, and terminal result; stop logging after the
bounded pulse window. For natural production evidence, enable existing bounded
production/target diagnostics only in ignored test map/config when an FTNK is
not produced or is retargeted: distinguish unavailable prerequisite, queue/cash
competition, production request/selection, squad owner, order, target switch,
projectile/pulse, and final result. Reservation/rejection is not part of the
content change, so absence is expected unless normal production contention is
being diagnosed. Do not add per-tick product logging. Remove all noisy temporary
diagnostics and test-only product/map references before publication; preserve
only ignored artifacts and concise conclusions/paths in report/state.

No new product unit test is required merely to mirror two YAML values. If the
implementation needs code or an inheritance mechanism not already covered by
existing MiniYaml tests, stop and reassess scope; any focused committed test must
protect a reusable invariant rather than duplicate the literal config.

### Ordinary and differential games

Use the global lock around every build/game command. A two-game changed/control
batch reserves both game slots:

```text
python3 .agents/skills/coordinate-cnc-development/scripts/with_resource_slots.py \
  --lock-dir /root/github/LibertyDawn/.worktrees/coordinated-cnc/20260806-bug-polish-01/locks \
  --resource game --capacity 2 --slots 2 -- \
  python3 launch-ai-parallel.py --manifest ABSOLUTE_MANIFEST --output ABSOLUTE_NEW_BATCH --jobs 2
```

Use an isolated worktree at base SHA for control; do not toggle source between
runs. Match map artifact/hash, factions, seed, start slots, facing, actors,
options, cash, bots, initial state, and duration. Keep support/settings/logs,
replays, saves, benchmark prefixes, ports, displays, and batch directories
separate. Use `skynet` and/or `brutalis` as ordinary supported real AIs when
their Recon path/production is relevant, and prove the exact bot types from the
current logs. A focused test-only map may be shared by both builds.

Difficulty ladder and primary feedback:

1. **Cycle-1 matched smoke (first behavioral test):** immediately after the
   first config change and baseline static gate, run changed and base control in
   parallel at headless MAX on the focused full-engine armor harness. Stress the
   simple Heavy burst and initial FTNK max HP. Failure hypothesis: wrong health,
   only one pulse changed, inheritance broadened/nulled the override, or the
   harness samples too early. Failure signal: anything other than
   30,000/5,040 control and 36,000/5,544 changed after the pulse window. Pass:
   exact final actor values plus ordinary bots/modules and required run identity.
2. **Armor/death isolation:** add the five non-Heavy actors and an independently
   killed FTNK near a Heavy actor. Stress nested MiniYaml ownership and the death
   consumer. Failure signal: any non-Heavy or death-explosion delta, missing
   pulse, splash ambiguity, or changed target choice. Pass: exact matrix parity,
   Heavy attack-only delta, and death parity.
3. **Counter exchange pairs:** run equal-credit unsupported FTNK against Heavy
   Light Tanks, then Medium Tanks, then Mammoths on open connected ground. Use a
   long approach and a short approach; include one-lane and two-direction focus
   formations. Failure hypothesis: the roughly 32% exchange-capacity gain
   displaces true-tank hard counters or is practically inert. Pass: changed FTNK
   completes measurably more bursts/damage or survives a meaningful extra shot,
   but true tanks remain the repeatable head-on winners with meaningful survivor
   HP. Record costs, forces, first-contact tick, completed bursts, final units/HP.
4. **Defensive approach and counter integrity:** send equal-credit control and
   changed FTNK toward an intended building through fixed ordinary base defense,
   first directly, then with longer-range/kiting units and aircraft pressure.
   Failure hypothesis: 36,000 crosses a salvo threshold that turns an unsupported
   hard-stop into a routine breakthrough or makes range/air inefficient. Pass:
   changed earns a bounded extra burst/useful damage while fixed defense, kiting,
   and aircraft still stop/beat unsupported FTNK reliably.
5. **Mixed real-AI contention:** normal mixed armies with infantry screens,
   scouts, Light/Medium Tanks, artillery/air, multiple FTNK, target switching,
   splash, damaged actors, and normal squads. Do not script combat after initial
   placement. Failure hypothesis: mass/focus/normal retargeting makes FTNK the
   preferred answer to every ground force or masks the Heavy-only invariant.
   Pass: FTNK primarily clears infantry/light/buildings, true tanks/counters
   remain valuable, and unexpected orders/results are explicitly judged.
6. **Economy/production and state transition:** fresh normal starts with scarce
   cash and shared vehicle/upgrade queues; require a supported AI to reach Recon
   II, produce and squad at least one FTNK, and exercise queue/cash competition.
   Where practical also observe Covert II removing FTNK availability. Include a
   save shortly before contact, reload once, and verify a changed FTNK retains
   correct max/current health and attack results; also rerun fresh because reload
   is never sole proof. Failure: no exercised FTNK, stale 30,000 max HP, health
   percentage corruption, changed production/tech timing without an explained
   deterministic consequence, or save-only acceptance.
7. **Natural conclusion:** at least one real headless MAX match on an ordinary
   connected map (Empire Earth4 is a known harness example) with normal starts,
   ordinary AI, all normal modules, and enough duration for FTNK production must
   reach a natural conclusion. Pair against base when practical. Record outcome,
   FTNK production/contact/survival/useful damage evidence, army/economy values,
   tick count, wall time, and replay/log paths. If FTNK does not occur, change
   seed/map/cash/bots and rerun; an unexercised natural match is regression
   context only.

After each materially judged game or pair, increment the game count, stage only
authorized current/control artifacts under a fresh analysis `inputs/` directory,
run the required Commenter and (because this is balance policy) routine Policy
Reviewer path, read both outputs, and record conclusions/test inspirations in
the cycle journal/report. Never stage source, worker state, or implementation
notes to those roles.

### Old-behavior control and required improvement

The old-behavior control is exact commit
`09ccdac3c1ecb5134a4751f2bcbd8a7970dfe6bf` in an isolated worktree. There is no
same-build feature toggle, so every comparison must identify separate control
and changed commits plus actor/weapon resolved-output checksums. Keep all run
inputs matched as specified above.

Required literal deltas are exact, not statistical: +6,000 max HP (+20%) and
Heavy 20 to 22 on all attack pulses, yielding 5,040 to 5,544 full-burst center
damage (+10%). Required preservation metrics are zero changed/control delta for
the five non-Heavy full-burst totals, death-explosion Heavy damage, weapon timing
and filters, AI/config values, cost, production prerequisites, and all other
resolved fields.

The changed behavior must materially outperform control in scenarios that
exercise its intended assault value: more Heavy target damage/completed pulses
before loss, survival of at least one meaningful additional shot/salvo where a
threshold permits, or completion of a useful building/infantry/light-vehicle
burst the control narrowly misses. Repeated parity in approach survival and
useful work requires investigation of range, pathing, pulse completion, and
whether the test actually exercises the health; a loss/tie in overall natural
match is not by itself failure because no AI strategy changed.

Counter preservation is equally required: true tanks remain repeatable
equal-credit head-on winners; ranged/kiting and aircraft still trade efficiently;
and fixed defenses do not change from a reliable unsupported hard-stop to a
routine breakthrough. Investigate apparent violations across distances,
formations, and seeds to separate focus fire, first-shot timing, pathing, facing,
splash, repairs, and target selection from the policy. Do not silently retune
the requested numbers; report a reproducible policy failure/risk.

This config-only change adds no scan, allocation, ordering, or hot-path work.
For a long matched MAX pair, compare ticks/second or wall time; no reproducible
simulation slowdown beyond ordinary run noise is acceptable. Investigate a
repeatable median regression greater than 5%, but source inspection plus stable
matched throughput is sufficient—do not invent performance code.

### Adversarial cases

After literal acceptance first passes, complete at least three distinct clean
full-engine ordinary-AI adversarial scenarios after the latest relevant fix. If
one fails and causes a config fix, restart this three-clean requirement for the
affected cases.

1. **Armor matrix plus death collision.** Failure hypothesis: the BigFlamer
   override leaks into a sibling armor class or into `^FlametankExplode`, or one
   of seven pulse identities stays at Heavy 20. Perturbation: identical high-HP
   actors cover every armor class, a second enemy enters splash range, and a
   Flame Tank dies adjacent to a Heavy actor. Failure signal: any non-Heavy or
   death-explosion changed/control delta, changed collateral target, missing
   pulse, or Heavy total other than 5,544. Pass evidence: exact final HP matrix,
   all pulse ticks, exact attack-only Heavy delta, exact death parity.
2. **Equal-credit massed true-tank counter.** Failure hypothesis: multiplicative
   health/damage and cheap focus fire displace Heavy-armored counters.
   Perturbation: Light and Medium Tank forces of equal total credit fight changed
   FTNK first through one approach lane and then from two directions/open ground;
   repeat Mammoth with its self-heal observed. Failure signal: changed FTNK
   routinely wins uncomplicated head-on fights, counter survivors become
   negligible, or results depend on unrecorded facing/path bias. Pass evidence:
   Heavy tanks repeatedly win with meaningful final HP while changed FTNK shows
   the requested bounded increase in damage/completed work versus control.
3. **Defense, distance, and air/kiting counter.** Failure hypothesis: 36,000 HP
   crosses a defensive threshold and erases positioning counters. Perturbation:
   vary short/long approach, choke/open geometry, fixed defensive fire,
   long-range/kiting force, and ordinary air attacks; target a real production
   building beyond the screen. Failure signal: unsupported changed FTNK routinely
   reaches/destroys what control cannot, or range/air cannot gain an efficient
   trade. Pass evidence: one bounded extra useful changed action is plausible,
   but defense, separation, focus fire, and aircraft remain decisive.
4. **Mixed-module contention and retarget.** Failure hypothesis: ordinary squad
   orders, AutoTarget, multiple FTNK, infantry screens, damaged actors, and
   splash conceal unintended armor damage or make FTNK universally preferred.
   Perturbation: normal AI mixed armies and shared cash/vehicle queues, with an
   FTNK target destroyed mid-burst so the squad must transition/retarget.
   Failure signal: wrong final actor receives unexplained buffed damage,
   production composition changes despite identical rules/seed before combat,
   stuck/idle squads, or FTNK dominates all target classes. Pass evidence:
   identifiable orders and target transitions, correct final Heavy/non-Heavy
   outcomes, and combined-arms/counter value preserved.
5. **Save/load and missing/changed asset boundary.** Failure hypothesis: actor
   health serialization, reload initialization, destruction, or loss of the
   intended Heavy target invalidates the resolved change. Perturbation: save a
   damaged changed FTNK before contact, reload and finish; in a separate fresh
   run destroy the original Heavy target mid-sequence and let normal AI continue.
   Failure signal: max HP reverts, health percentage jumps incorrectly, pulse
   totals differ from fresh, no valid retarget/final outcome, or reload is the
   only passing evidence. Pass evidence: correct state/result after reload plus a
   clean fresh confirmation and an explicitly judged normal recovery transition.

Scenarios 1–3 are the minimum distinct clean set; scenarios 4 and 5 cover normal
module contention and persistence/destruction assumptions and should be run
unless evidence proves them irrelevant. Routing/transport topology such as
Archipelago is not required because no routing or transport behavior changes;
do not spend cycles on an island map merely to satisfy an unrelated pattern.

### Final regression

After the latest fix and at least three clean adversarial scenarios, rerun the
literal armor harness from a fresh process/build with ordinary bots and all
normal modules. Add the strongest compatible stress without contaminating exact
measurement: multiple nearby moving FTNK/Heavy units and defensive pressure may
operate outside the isolated measurement lane, while the instrumented rookie
FTNK and identical armor actors remain modifier-free and geometrically isolated.

The final run must again prove fresh FTNK 36,000 HP; changed Heavy full burst
5,544 after all fourteen pulses; exact five-class non-Heavy parity; unchanged
death-explosion Heavy damage; intended map/hash, bots, factions, actors, options,
seed, Headless MAX, tick progress, final outcomes, flushed replay/log evidence,
and clean exit. Then rerun resolved-rule/weapon assertions, `git diff --check`,
`make test`, the required broad CI/checks, and one natural full match to
conclusion if the final implementation or merge base changed relevant content.

Record all commands, commits, checksums, seeds, artifact paths, exact values,
counter results, diagnostics removed/retained, performance comparison, PR, and
GitHub check conclusions in the task report and handoff. Acceptance from a stale
save, pre-fix game, missing-FTNK natural match, or source-only inspection is
invalid.

## Implementation rules

Task-specific implementation/publication plan:

1. Confirm branch/head and save pinned-base resolved FTNK/BigFlamer/death/sibling
   evidence. Inspect the named CNC-43 branch/PR commits for actual overlap.
2. Make the smallest content-only change at FTNK health ownership and
   BigFlamer attack ownership. Do not edit shared explosion/flame policy, AI, or
   C# code.
3. Run focused resolved assertions and YAML/build baseline, then make the first
   behavioral test the matched full-engine ordinary-AI control/changed pair in
   cycle 1. Do not defer games for extra unit-only work.
4. Use the evidence loop and difficulty ladder to challenge inheritance,
   delayed pulses, counter integrity, massing, defensive thresholds, normal
   production/squad contention, and save/load. Use Commenter/Policy Reviewer
   feedback after each materially judged batch; keep diagnostics bounded and
   ignored, and remove temporary noise before publication.
5. Finish the literal regression, report, clean diff, individual task PR against
   the recorded base branch, and required checks. Do not merge the PR. Propose
   `Complete - testing` only with all evidence green; otherwise publish the
   safest result as `First iteration - testing` with exact failures/risks.

- Do not ask implementation or preference questions. Investigate code, history,
  controls, configs, tests, and evidence; choose the strongest safe option and
  record material assumptions. Stop only this task for a real authority,
  credential, missing-file, unsafe-path, or irreducible blocker.
- Keep responsibilities separate and dependencies explicit. Prefer short,
  cohesive classes and functions; split oversized responsibilities when that
  improves cohesion, testability, or hot-path clarity without unrelated churn.
  Preserve unrelated behavior and user changes.
- Put tunable policy in the owning rules/config/save/map layer and algorithmic
  invariants in code. Do not duplicate policy across AI personalities or hide a
  rules/config concern in test-only code.
- Add proportionate unit/interface/static tests. Add useful bounded debug logging
  and handled warnings/errors at the owning boundary: make failures actionable,
  never silently swallow exceptions or substitute success, avoid per-tick spam,
  and remove obsolete/noisy temporary instrumentation before publication.
- Keep deterministic simulation hot paths bounded. Avoid repeated full-map/unit
  scans, uncontrolled allocations, nondeterministic iteration/order, unbounded
  retry queues, or logging that materially reduces MAX throughput. Measure or
  explain performance-sensitive changes with current evidence.
- Inventory and test ordinary modules that compete for the same units, queues,
  cash, reservations, targets, repair, or retargeting.
- Record worthwhile out-of-scope fixes, refactors, and optimizations under
  `Deferred work` in the task report/handoff; never expand scope silently or make
  concurrent workers edit a shared deferred-work file.
- Keep raw logs/replays/saves/profiles outside Git or under ignored
  `AUTONOMOUS-CNC-LOGS/`. Record concise paths, seeds, and conclusions here or in
  the task report.
- Never push directly to `bleed`, merge a GitHub PR, or edit the task sheet or
  coordinator state. Update this state and task report on the recorded task branch
  or, during integrated repair, the recorded repair branch.

## Evidence-driven loop

One cycle begins when a product-code/config change is made. A cycle may build,
run focused checks, and execute up to two materially useful games needed to judge
that change. Merely reading logs or correcting an invalid harness without a
product change does not begin another cycle; record it honestly.

Treat full-engine simulations with ordinary AI as cheap primary feedback. The
first behavioral test after the first implementation change must be a full-engine
ordinary-AI game, normally headless MAX, with every relevant normal module enabled
from test 1. A focused custom map, pre-spawned actors, short distance, or obvious
cheese setup may make the event immediate, but it must not replace the real engine
or ordinary AI with a passive/custom bot or isolated manager fixture. Run focused
unit/static checks as useful baseline gates before or alongside it; do not delay
game evidence while accumulating unit-only confidence. Keep available game slots
working while other agents code or analyze because simulation is cheaper than
missing human feedback.

For every change to AI strategy, priorities, economy, production, targeting,
recovery, or tactics, compare against old behavior repeatedly throughout the loop.
Prefer a same-build feature-disabled control. If unavailable, run the recorded
base SHA or named known-good older AI commit from an isolated worktree. Record the
exact control commit/toggle, content/config checksum, map, factions, seed, starts,
options, initial state, opponents, and metrics. Keep these matched so the intended
behavior is the meaningful difference. Use both game slots for paired control and
changed-AI runs when practical; make the first behavioral test such a pair when
the feature toggle or recorded control build is ready.

The changed AI must materially outperform old behavior in scenarios that actually
exercise the change. Judge match outcome together with task-relevant measures such
as survival, objective completion, tech timing, income/spending, army/economic
value, useful damage/kills, losses, idle queues/units, recovery time, and CPU cost.
If it loses, ties, or gains only marginally, assume a likely implementation error,
bad strategic policy, or displaced regression until evidence rules those out.
Inspect code and logs, vary adversarial scenarios, and fix the cause; do not call
feature-activation logs a success. Because matches can vary, repeat materially
useful comparisons before blaming noise. A non-strategic change need not win more,
but it must not degrade the relevant old-AI behavior without an explicit accepted
tradeoff in the spec.

Treat all tests as attempts to break the implementation. Compilation, lint, and
static analysis are baseline gates; every unit, integration, save/load, replay, or
game test must exercise a regression risk, boundary, invalidation, contention,
failure/recovery path, or assumption under pressure. Before running it, record:

- Failure hypothesis: what plausible defect this test could expose.
- Perturbation: what is made harder or different from the last passing test.
- Failure signal: the exact log/state/player-visible outcome that proves breakage.
- Pass evidence: the final observable result needed to falsify the hypothesis.

The existing broad regression suite counts as an adversarial gate against breaking
unrelated behavior, but it does not replace targeted falsification of this task.

One initial full-engine cheese-in-front-of-the-mouse smoke setup may establish
that the harness and simplest behavior work. As soon as it passes, change at least one
meaningful dimension—timing, map geometry, resources, missing/destroyed assets,
unit count, pressure, competing orders, save/load boundary, or match duration—and
make every later test harder or materially different. Never spend cycles on
near-identical happy-path confirmations when a stronger falsification is possible.
These tests replace much human feedback: use surprising results to challenge the
spec's assumptions, inspect the repository/evidence, and choose the next change
without asking the user an implementation question.

For each cycle:

1. Reread this state, current diff, and previous evidence.
2. Implement or revise the smallest evidence-driven change.
3. Run focused unit/static checks and fix relevant errors or warnings without
   treating them as a substitute for the game.
4. From cycle 1, run the simplest not-yet-proven full-engine ordinary-AI
   adversarial scenario that can falsify the current implementation while proving
   the requested outcome if it survives.
5. Diagnose results against desired and forbidden behavior. Add bounded
   instrumentation when evidence cannot distinguish mission purpose, candidate
   rejection, reservation owner, competing consumer, movement/order, contention,
   state transition, and final outcome.
6. Remove or reduce obsolete/noisy diagnostics after they answer the question.
7. Update the cycle journal before making another code change.

## Match narrative and policy-feedback loop

After every materially judged full-engine match or paired control batch:

1. Increment `Full-engine game tests completed` for each game, including an
   invalid setup that still ran far enough to expose evidence; label invalid runs.
2. Copy (do not symlink) only the authorized current/control logs, manifests,
   summaries, and metrics into the role output directory's `inputs/` subtree. In
   that directory, write a strict JSON Commenter job containing only their absolute
   `artifacts` paths, optional `design_reference`, and the absolute `output` path
   ending in `NARRATIVE.md`. Launch a no-history fresh `commenter` role (Terra 5.6
   medium). Do not stage source code, this worker state, the task sheet,
   implementation notes, or inline job-file commentary.
3. Read its factual `NARRATIVE.md`. Verify cited artifacts/ticks and use it to
   understand exact control differences, causal win/loss sequence, and what the
   losing AI did well. Correct the input/evidence rather than editing the narrative
   into a preferred story.
4. For AI-policy work, copy that narrative (do not symlink) to the Policy Reviewer
   output directory as `inputs/NARRATIVE.md`. Write a strict JSON job there with
   exactly the absolute `design_reference`, staged `narrative`, and `output` paths;
   output must end in `POLICY-REVIEW.md`. Launch a no-history fresh
   `policy-reviewer` role (Terra 5.6 medium). Questions embedded in the narrative
   are the worker's questions to this playtester; the job contains no inline
   context.
5. Read the `POLICY-REVIEW.md` before choosing the next code change. Treat advice
   as hypotheses: record what inspired the next test/change and what was rejected
   with reasons. Never substitute the review for adversarial game evidence.

Detailed narratives/reviews stay under the ignored analysis directory. Preserve
their paths plus concise factual and policy conclusions in the cycle journal and
task report. A paired two-game batch may share one Commenter and Policy Reviewer.

If a policy problem persists after at least ten completed full-engine game tests,
the worker may ask exactly one Sol 5.6 xhigh `policy-escalation` instance. First
write a new narrative stating the game-test count, repeated failure pattern,
attempted policies, evidence for/against each, and focused questions. The escalated
reviewer still reads only the design document and narrative. Record use in the
assignment field. Never invoke it before test 10 or invoke it twice for one task.

Prefer the full engine and real bot types. On Linux use the explicit headless MAX
path when graphics/input are irrelevant. Prove the current run loaded the intended
map, bots, actors, options, activated headless MAX, advanced ticks, flushed logs,
replay/benchmark evidence where configured, and produced the final outcome. A
passive fixture or manager-only simulation is not sole proof.
Use focused setup maps to accelerate reproduction, but before acceptance run a
fully enabled scenario containing every relevant ordinary module. Headless MAX
never replaces required graphical, rendering, input, lobby, or platform checks.

Force every inventoried competing system to act in at least one integrated test.
For routing or transport, test both an ordinary connected map and an island or
blocked topology such as Archipelago. If the event does not occur, change the
seed, map, duration, starting actors/resources, bots, or focused setup; do not pass
an unexercised path. Judge every unexpected behavior explicitly as acceptable or
defective.

Use ordinary full matches for emergent AI behavior. Full-engine real-AI testing
starts in cycle 1 and remains the main feedback loop; increase difficulty as soon
as the first behavior works rather than postponing games until late acceptance.

After normal acceptance first passes, require at least three distinct clean
adversarial scenarios after the latest relevant fix. Every adversarial scenario
must use the full engine, ordinary game AIs, and relevant normal modules. A focused
map may force the edge case, but passive/custom bots or isolated simulations do
not count. Define its expected failure signal, force it to occur, and inspect
current logs/replays; a happy-path rerun is not adversarial evidence.

Include hostile geometry, timing/state transitions, unusual unit counts, missing
critical assets, destruction/capture, save/load where state persists, and shared
resource/order contention as relevant. If a fix follows an adversarial failure,
restart the requirement for three clean adversarial scenarios affected by that
fix, then rerun the original literal acceptance with all normal modules. Keep that
final regression literal, but add the strongest compatible stress dimension that
does not invalidate the acceptance scenario; it must also try to break the code.

Prefer a matched differential as the golden adversarial test when the behavior
can be toggled: keep faction, map, seed, starts, options, and initial state aligned
and enable the behavior for only one side. When the scenario materially exercises
the feature, require a decisive advantage over the old-behavior control;
investigate a loss, tie, or marginal gain rather than calling it proof, and
document unavoidable nondeterminism. Do not substitute unrelated different AI
personalities for the old-behavior control unless the spec explicitly needs that
secondary benchmark.

Run at least one real full match at the fastest applicable speed to a natural
conclusion. For AI/engine behavior use headless MAX; use graphical modes when the
feature concerns rendering, lobby, input, or platform behavior. Use long-distance
starts for progression/endurance and short-distance starts for rush/defense. Do
not waste concurrency on near-copy spawn swaps unless position bias matters.

Wrap shared resources with:

```text
python3 .agents/skills/coordinate-cnc-development/scripts/with_resource_slots.py \
  --lock-dir /root/github/LibertyDawn/.worktrees/coordinated-cnc/20260806-bug-polish-01/locks \
  --resource game --capacity 2 --slots 1 -- COMMAND...
```

Reserve two game slots when using a two-game `launch-ai-parallel.py` batch. Poll
background games within 60 seconds, normally cap them at 30 minutes, isolate every
support directory, settings, log, replay, save, benchmark prefix, map artifact,
port, and display, and judge each run separately. Use concurrent slots for
materially different scenarios. Return to serial tests if contention corrupts
timing or evidence. A required full match may exceed 30 minutes while it continues
making useful progress; stop it when evidence is sufficient or progress stalls.

For expensive setup, optionally save shortly before the critical event and reload
after a logic change. Record the save's commit, config, seed, and tick; reject an
incompatible or stale save. Never use reload as the sole acceptance, adversarial,
or final-regression evidence because it may retain stale initialization or AI
state. Confirm the result again from a fresh match.

After 20 unsuccessful code-change cycles, publish the safest useful result as
`First iteration - testing`. Do not pad cycle counts after evidence is sufficient.

When the phase is integrated testing, the isolated 20-cycle cap no longer blocks
the assigned release validation. Use at most three code-change cycles for the
current RC and at most twelve across four RCs, updating both integrated counters.
Test the exact recorded release head before changing code; put any change only on
the recorded repair branch and rerun the materially affected original acceptance,
adversarial, and combined scenarios.

## Completion and publication

Propose `Complete - testing` only after literal acceptance, all required clean
adversarial cases, final regression, task checks, report, PR, and required GitHub
checks pass. Otherwise propose `First iteration - testing` with exact failures and
risks. The reviewer and integrated release determine final status.

The task report must cover behavior, design choices, assumptions, cycle count,
tests, seeds/artifact paths, diagnostics removed or retained, performance and
determinism, old-control configuration and comparative results, PR/checks,
deferred work, and remaining risks.

Push the task branch and open one individual PR. Do not merge it. Wait for every
required GitHub check; diagnose and fix relevant failures within the isolated
cycle budget and rerun them. If required checks cannot become green, propose
`First iteration - testing` rather than completion.

When review returns a correction, perform at most one review-response code/test
cycle, applying the highest-impact safe finding you agree with or recording
evidence for rejection. This cycle counts within the 20 isolated cycles; never
silently exceed the budget.

## Cycle journal

| Cycle | Commit/change | Failure hypothesis and perturbation | Checks/games | Narrative/policy review | Failure/pass evidence | Decision/next harder test |
|---|---|---|---|---|---|---|
| 1 | `1fc17f351a`: FTNK HP 36000; seven BigFlamer-local Heavy 22 overrides | Wrong max HP, incomplete/leaking pulse override, death-template contamination, counter displacement, or no natural production; literal matrix/death collision, isolated true tanks, fixed defense/air, ordinary Empire Earth4, and save/load | Baseline and pre-publication builds; 34 completion-worthy games before cycle-2 final rerun. Literal v2 exact; 3x matched Medium seeds; Light/Mammoth, defense, natural production/contact, and save/load evidence captured | Required commenter/policy paths through `analysis/worker-4-cnc-43a/`; reviews supported literal localization and counter preservation, while strategic conclusions from isolated/natural outcomes were appropriately limited | Exact 36000/5544 changed vs 30000/5040 control; five non-Heavy totals and 1991 death collision parity. Medium/Light/Mammoth and fixed defense/air remained decisive. Natural pair produced/contacted FTNK and concluded; changed save retained 36000 max/current state through post-load damage | Keep requested balance values. Reject out-of-scope AI retuning; close only the YAML-warning and final-regression gates |
| 2 | `6f3a33ea16`: declare all seven local nodes `SpreadDamage` | `make test` warned that value-less local warhead nodes lacked a declared type, despite correct resolved behavior | Cycle-2 resolved outputs byte-identical to cycle 1; fresh matched literal pair passed; final `make test` passed Release 0 warnings/0 errors plus all CNC YAML/maps | `analysis/worker-4-cnc-43a/final-literal-v1/{commenter,policy}`; literal result accepted as narrow evidence, with strategic policy correctly delegated to prior adversarial games | Final changed 36000 HP, Heavy 396x14=5544; control 30000/360x14=5040; exact non-Heavy/death parity; tick 340 clean exits. No warning remains | Publish the two-commit product change; wait for PR checks; no further content change indicated |

Review response (no product/code cycle): the response added bounded analysis-map terminal capture and ran two invalid then two clean matched natural games. The valid pair named SkyNet as winner at changed tick 29,720 and control tick 25,027 and recorded complete terminal army/economy/type state. Fresh roles at `analysis/worker-4-cnc-43a/natural-terminal-v2/{commenter,policy}/` called the evidence usable and the policy result mixed/medium-confidence. The suggested broader FTNK commitment/withdrawal work is deferred because this task forbids AI changes and one adaptive pair does not expose a causal CNC-43A defect.

## Handoff receipt

- Proposed status: `Complete - testing`
- Final branch/head: `agent/round-20260806-cnc43a-flame-tank-balance`; product head `6f3a33ea165e0b4b90d0e4a9c974b70a12f78a12`; reviewed PR head before this response receipt `f584f56f12915d650bb3739cb39bfd31ee8a373a`
- PR and checks: `#79 https://github.com/Realpra1/LibertyDawn/pull/79`; `MERGEABLE/CLEAN`; assigned base is unprotected and GitHub reported no required checks, no check rollup, and no workflow runs after polling
- Cycles used: `2/20`
- Acceptance evidence: final literal v4 proves 36000 HP and 5544 Heavy burst vs pinned control 30000/5040, with exact five-class and death parity
- Adversarial evidence: true tanks remain equal-credit winners/holders; fixed turrets, range, MLRS, and Orca remain a hard stop; ordinary natural FTNK production/contact, named SkyNet wins with terminal state, and a post-load damaged FTNK were exercised
- Old-behavior control and comparative result: exact `09ccdac3c1ecb5134a4751f2bcbd8a7970dfe6bf`; only intended HP/Heavy damage differed in literal evidence
- Match narratives and routine policy-review conclusions: all material batches have staged fresh outputs; `natural-terminal-v2` closes the missing terminal record, its Commenter calls the pair usable, and its Policy Reviewer is mixed/medium-confidence while explicitly declining causal attribution from one pair
- Sol-xhigh policy escalation (unused, or test count/path/conclusion): unused; no persistent policy problem required escalation
- Final regression: literal v4 matched pair seed 43001 passed at tick 340; final resolved outputs exact; final `make test` passed
- Error/warning and diagnostic-cleanup result: cycle 2 removed seven new YAML warnings by declaring `SpreadDamage`; final build 0 warnings/0 errors; no product diagnostics; temporary load wrapper removed
- Performance/determinism result: config-only change adds no hot-path work; focused final pair completed in 6.006/6.007 seconds and 56.566/56.559 valid ticks/s; natural wall time diverged with the intentionally changed combat trajectory and is not used as a performance comparison
- Deferred work: multi-seed/replay-derived attack, route, and withdrawal policy study; AI production/target retuning remains explicitly out of scope, while terminal winner/economy/force capture is now complete
- Known failures/risks: three geographically separated FTNK remained unengaged at the Mammoth hold bound, but all six Mammoths survived with 536480 changed aggregate HP after killing 14/17; the earlier concentrated Mammoth harness was invalidated because death-chain geometry reversed both changed and control
- Relevant artifact paths: `analysis/worker-4-cnc-43a/{final-resolved-cycle2,final-literal-v1,true-tanks-v1,natural-v1,natural-terminal-v2,save-load-v1,defense-v1,mtnk-isolated}` and task report
