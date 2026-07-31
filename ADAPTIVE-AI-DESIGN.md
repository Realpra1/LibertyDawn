# Adaptive AI — design questions before we build it

**Status:** design only. No code written. Pick an option per question and we implement.

## The feature

> *"If the AI could track the value/kill ratio of units and turrets, and then if the AI sees that
> something is underperforming, it will build less of that. And if something is performing really
> well, it will start to use that more."* — plus k/d **and** economy ratio.

Concretely: SkyNet measures, per actor type, credits-of-enemy-stuff-destroyed vs
credits-of-own-stuff-lost (and something economic), and shifts its own build weights toward what
works.

---

## What I found in the code

Six findings. The first one is the one that matters most.

### 1. SkyNet's `UnitsToBuild` numbers are currently **inert**. All of them.

`UnitBuilderBotModule.BotTick` picks between two selection paths:

```csharp
// UnitBuilderBotModule.cs:90
foreach (var q in Info.UnitQueues)
    BuildUnit(bot, q, idleUnitCount < Info.IdleBaseUnitsMaximum);

// UnitBuilderBotModule.cs:111-113
var unit = buildRandom ? ChooseRandomUnitToBuild(queue) : ChooseUnitToBuild(queue);
```

`mods/cnc/rules/ai.yaml:916` sets `IdleBaseUnitsMaximum: 999` — and so do **all six** LibertyDawn
bots (lines 585, 736, 916, 1070, 1226, 1354). The idle-unit count essentially never reaches 999, so
`buildRandom` is always true and the bot always takes `ChooseRandomUnitToBuild`
(`UnitBuilderBotModule.cs:165-173`), which is a **uniform pick over everything currently buildable**.

The weighted function `ChooseUnitToBuild` (`:175-193`) — the one with `UnitsToBuild` in it — is
effectively dead code for this mod.

What the weights *do* still do is act as a **whitelist**:

```csharp
// UnitBuilderBotModule.cs:120-121
if (Info.UnitsToBuild != null && !Info.UnitsToBuild.ContainsKey(name))
    return;
```

Only the key is read; the value is ignored. This is why `ctnk: 0`, `msam: 0` and `mcv: 0`
(`ai.yaml:917-946`) are still built — a weight of `0` is still a key. What actually shapes SkyNet's
army today is `UnitLimits` (`:965`) and `UnitDelays` (`:947`), not `UnitsToBuild`.

**Implication: an adaptive weighting system bolted onto `UnitsToBuild` as it stands would change
nothing.** Something in the selection path has to change first. See Q1.

### 2. Even when it *is* used, a "weight" is a **share ceiling**, not a share

```csharp
// UnitBuilderBotModule.cs:186-190
foreach (var unit in Info.UnitsToBuild.Shuffle(world.LocalRandom))
    if (buildableThings.Any(b => b.Name == unit.Key))
        if (myUnits.Count(a => a == unit.Key) * 100 < unit.Value * myUnits.Count)
            ...return it
```

Shuffle, then return the **first** type whose current share of the army is below its weight. So:

- Types under their ceiling are chosen **uniformly at random** among themselves. Weight `40` and
  weight `65` are equally likely to be picked while both are under-represented.
- The weights do not need to sum to 100 and don't. SkyNet's sum to ~390.
- `harv: 100` (`ai.yaml:924`) does not mean "100% harvesters" — it means "harvesters are never
  capped". Raising it to 200 or 400 would do **nothing**.
- `mtnk: 1` means "build a medium tank only while they are under 1% of the army" — near-total ban.
- `myUnits` counts actors with `IPositionable` (`:181-184`), so harvesters and aircraft are in the
  denominator and buildings are not.

So adaptation is **asymmetric**: pushing a weight down bites immediately; pushing it up does nothing
once the ceiling is already above any achievable share. Any multiplier scheme has to reckon with
this.

`BuildingFractions` works the same way (`BaseBuilderQueueManager.cs:374`):
`if (count * 100 > (frac.Value + totalLimitedFrac) * playerBuildings.Length) continue;` — also a
ceiling, also over a shuffled dictionary (`:350`), with the twist that fractions belonging to
*limit-saturated* types are redistributed into everyone else's headroom (`:336-347`). Note buildings
**do** use the weighted path — there is no random fallback for structures.

### 3. Kill attribution exists, but it is **last-hit only**

`AttackInfo` (`OpenRA.Game/Traits/TraitsInterfaces.cs:89-95`) carries exactly one `Attacker` — the
actor that dealt the final blow. There is no damage ledger anywhere in the engine. `Health` does not
retain who dealt what.

The good news: **`UpdatesPlayerStatistics` already does most of the bookkeeping we want**, and it is
attached to `^ExistsInWorld` in `mods/cnc/rules/defaults.yaml:3`, i.e. to essentially every actor —
units, buildings, turrets, harvesters.

```csharp
// PlayerStatistics.cs:210-251 (abridged)
void INotifyKilled.Killed(Actor self, AttackInfo e) {
    playerStats.DeathsCost += cost;                       // victim's owner: value lost
    if (e.Attacker == self) return;
    var attackerStats = e.Attacker.Owner.PlayerActor.Trait<PlayerStatistics>();
    ...
    attackerStats.KillsCost += cost;                      // killer's owner: value destroyed
}
```

`cost` comes from `ValuedInfo.Cost` (`Valued.cs:21`) — every buildable has one, so **pricing an
actor is free and exact**. The only missing piece is bucketing by `e.Attacker.Info.Name` instead of
summing into one player-wide total. That is a small, contained change to an existing trait.

Caveats worth knowing:

- **Overkill is discarded** (`Health.cs:162-164`): a second hit on an already-dead actor returns
  early, so a kill is credited once, to one actor. Good.
- `INotifyAppliedDamage` (the attacker-side hook, `OpenRA.Mods.Common/TraitsInterfaces.cs:134`) is
  **skipped when the attacker is dead or out of world** (`Health.cs:210`). An artillery shell that
  lands after the artillery dies credits nobody through that path. The victim-side `INotifyKilled`
  path has no such guard — `e.Attacker` is still there. **Use the victim-side hook.**
- Environmental kills are credited to whatever actor fired the weapon. The Red Tiberium detonation
  passes a `source` actor into `DoExplode` (`ResourceLayer.cs:830`, called from `:516` and `:718`) —
  that will be a neutral/world actor, not a player unit, so it lands in a bucket we can ignore. But
  **tiberium kills on our own units still count as losses**, which will make anything that parks on
  tiberium look bad.

### 4. Economy is measured **per refinery**, never per harvester

`Refinery.AcceptResources` (`Refinery.cs:103-131`) fires
`INotifyResourceAccepted(self /* the refinery */, refinery, resourceType, count, value)`
(`OpenRA.Mods.Common/TraitsInterfaces.cs:163`). The harvester is **not** a parameter. The refinery
does hold `dockedHarv` while a dock sequence is running (`Refinery.cs:169-177`), so per-harvester
attribution is *possible* but requires threading that through — a real change to `Refinery`, not a
free observation. Player-wide income is trivially available (`PlayerResources.Earned`,
`PlayerResources.cs:99`).

### 5. `Info` objects are **shared between all players using that bot**

`UnitsToBuild` and `BuildingFractions` are `readonly Dictionary` fields on the
`...BotModuleInfo` (`UnitBuilderBotModule.cs:31`, `BaseBuilderBotModule.cs:214`). OpenRA constructs
one `ActorInfo` per rules set, so **two SkyNet players in the same match share the same dictionary
instance**, and it persists for the process lifetime. Mutating it in place would cross-contaminate
bots and leak learning between matches in the same session. The adapted weights must live in a
**per-module copy**.

### 6. Determinism: this must be observation-in-sim, decision-out-of-sim

`PLAN.md:53-55`: bot logic runs host-only (`Player.cs:215`) inside `Sync.RunUnsynced`
(`ModularBot.cs:91`), and must use `World.LocalRandom`, never `World.SharedRandom`.

The stat collection is fine — `INotifyKilled` runs inside the synced simulation on every client and
is identical everywhere. The rule is only that the **decision** (which weight to nudge, any
randomness in exploration) stays in the bot module and touches nothing but `bot.QueueOrder`.

Also: `UnitBuilderBotModule` implements `IGameSaveTraitData` (`:215-242`). Adapted weights would
need adding there or a loaded save silently reverts to the authored numbers.

---

## The questions

### Q1. `IdleBaseUnitsMaximum: 999` makes the weights inert. What do we do about it? *(blocker)*

Nothing else in this document matters until this is settled. Note that whatever we pick changes
SkyNet's army composition **on its own**, before any adaptation — so it needs its own playtest.

| | Option | |
|---|---|---|
| A | **Lower `IdleBaseUnitsMaximum` to a real number** (e.g. 12–20) so the weighted path runs when the base isn't flooded with idlers. | Restores stock behaviour, zero C#. But it also re-enables the "bot does nothing" hazard flagged in the TODO at `UnitBuilderBotModule.cs:21-23`, and reverts a deliberate fork choice. |
| B | **(Recommended)** Add a `WeightedUnitSelection: true` flag that makes the random path *weighted-random* over `UnitsToBuild` values (instead of uniform over all buildables), keeping `IdleBaseUnitsMaximum: 999`. | Keeps the fork's "always be producing" behaviour, makes the numbers mean something immediately, and gives adaptation a linear knob (double the weight → double the probability) instead of the awkward ceiling semantics of Q-note 2. |
| C | Leave selection alone and adapt `UnitLimits` / `UnitDelays` instead, since those are what actually bite today. | Honest about current behaviour, but limits are hard caps — adaptation would be coarse and would fight the balance intent of the authored numbers. |

If B is chosen, note the authored numbers need a re-read as *relative shares*, and the `: 0` entries
(`ctnk`, `msam`, `mcv`) become genuine bans rather than the no-ops they are now.

### Q2. What exactly is the score?

Per actor type *T*, over some window:

| | Option | |
|---|---|---|
| A | `killsValue(T) / lostValue(T)` — pure ratio. | Intuitive, matches the ask. But divide-by-zero for anything that never dies, and it is scale-free: a scout that trades 1 kill for 1 death scores the same as a mammoth that trades 5000 for 5000. |
| B | **(Recommended)** `(killsValue(T) + economyValue(T)) / (lostValue(T) + builtValue(T))` — **return on investment**: everything it ever earned us over everything we ever spent on it. | The denominator is never zero (you built it), it prices idleness correctly (a unit that sits home and survives scores ~0, not ∞), and it puts combat and economy in the same unit — credits. |
| C | `killsValue(T) - lostValue(T)`, a net-credits difference. | Simple and stable, but it rewards *volume*, so the type you already build most wins by construction. Feedback loop in the wrong direction. |

Under B, a value of 1.0 means "paid for itself". That is a legible anchor for tuning and for the
debug output in Q10.

### Q3. How do we attribute a kill?

| | Option | |
|---|---|---|
| A | **(Recommended)** **Last hit**, via the existing `INotifyKilled` path — bucket `e.Attacker.Info.Name` in an extension of `UpdatesPlayerStatistics` (`PlayerStatistics.cs:210`). | It's the only thing the engine actually records, it already runs on every actor via `defaults.yaml:3`, and overkill is already deduplicated (`Health.cs:162`). ~30 lines. |
| B | Damage-share: track a per-victim ledger of `attacker type → damage dealt`, split the bounty proportionally on death. | Fairer to artillery and support units, which will otherwise be systematically undervalued. Costs a dictionary per damaged actor and a new hook in `Health.InflictDamage`; also mis-credits overkill damage, which is common. |
| C | Last hit, but **also** credit a fixed fraction to every friendly type that damaged the victim in the last N ticks. | Middle ground, more moving parts and one more magic number. |

Be honest about A's bias: **artillery, rocket launchers and anything that softens targets will
under-score, and whoever lands the final shot will over-score.** With `arty: 28` and `mlrs: 30`
being large SkyNet weights, this is not a hypothetical. If we ship A, we should watch specifically
for artillery weights collapsing.

### Q4. What does "economy ratio" mean concretely?

A harvester that kills nothing is not underperforming. Three things could be meant:

| | Option | |
|---|---|---|
| A | **(Recommended)** **Player-level economy health gates the whole system, per-actor economy is not modelled.** Compute `income / spend` for the player; if it is falling, uniformly raise the economy types (`harv`, `sharv`, `proc`) and damp combat adaptation; otherwise leave them at their authored values and let combat adaptation run. | Uses only `PlayerResources.Earned` (`PlayerResources.cs:99`) — zero new engine plumbing. It captures the actual failure mode ("SkyNet is broke") without pretending we can rank one harvester against another. |
| B | **Per-harvester income.** Thread the docked harvester through `Refinery.AcceptResources` so each `harv`/`sharv` accumulates credits delivered, then score it like any other type via Q2's `economyValue`. | The only way `harv` vs `sharv` can ever be compared on merit. Requires modifying `Refinery.cs` (touching the resource sim the fork already heavily rewrote — highest-risk file in the repo). |
| C | Proxy: credit each harvester with `Earned / harvesterCount` per interval. | Free, but by construction identical for every harvester of every type, so it can never distinguish `harv` from `sharv`. Adds noise, not signal. |

Related: **refineries and silos are structures with zero kills**. Under any scheme they must be
excluded from combat scoring or their weights will be driven to zero and
the bot's economy dies (`proc: 25` at `ai.yaml:835`, `silo: 1` at `ai.yaml:845`). See Q5.

### Q5. Which actor types are eligible for adaptation at all?

`UnitsToBuild` for SkyNet (`ai.yaml:917-946`) contains things that are not units in any meaningful
sense: `upgrade.recon1..3` and `downgrade.covert`/`downgrade.economy` (tech-tree actors with
`Valued: Cost: 1000`, `misc.yaml:340-360`), plus `mcv` and `mhq` (expansion).

| | Option | |
|---|---|---|
| A | Adapt everything in the dictionary. | Simplest, and clearly wrong — upgrade actors are priced at 1000 credits and never fight, so they'd score 0 and be suppressed, silently disabling the fork's entire tech-tree design. |
| B | **(Recommended)** **Opt-in list**: a yaml field `AdaptiveTypes:` naming exactly which types adapt. Everything else stays at its authored weight forever. | Explicit, reviewable, and safe by default. Also lets us ship with three or four types and widen it after a playtest. |
| C | Opt-out list (`AdaptiveExcludeTypes:`) with everything else adapting. | Less yaml, but any new unit added to the mod silently becomes adaptive — wrong default for a fork that adds units regularly. |

### Q6. How far may a weight move?

| | Option | |
|---|---|---|
| A | Unbounded. | Guarantees degenerate armies (all-of-one-thing) and throws away the balance work in `ai.yaml`. |
| B | **(Recommended)** **Clamp to `[0.5x, 2.0x]` of the authored value**, with a configurable floor so nothing reaches zero. | Authored weights stay the anchor and the human tuning still dominates; 2x is enough to be visible in a match without letting one lucky early trade rewrite the whole composition. Widen to 4x later if playtests say it's too timid. |
| C | Clamp to `[0.25x, 4x]`. | More responsive, more visibly "adaptive" — but with SkyNet's ceiling semantics (finding 2) the upside half is largely wasted, so it mostly buys a deeper downside. Reconsider if Q1-B (true weighted random) is chosen. |

Either way the **floor must be > 0**, per `PLAN.md:438-440` ("clamped so nothing reaches zero"). A
type driven to zero can never generate evidence again — see Q7.

### Q7. Exploration vs exploitation — how do we avoid the death spiral?

A unit loses one bad early fight, its weight drops, fewer get built, so it gathers less evidence and
never recovers. This is the single most likely way for the feature to make the AI *worse*.

| | Option | |
|---|---|---|
| A | Rely on the Q6 clamp floor alone. | Cheapest. A floor of 0.5x does keep a trickle in production, which may genuinely be enough. Worth trying first. |
| B | **(Recommended)** Clamp floor **plus a confidence weight**: blend toward the measured score in proportion to sample count, `w = authored * (1 + (score - 1) * min(1, samples / N))`. Below N observations the weight barely moves. | Solves cold start (Q8) and the spiral with one mechanism, and is ~5 lines. Low-sample types stay near their authored value, which is exactly the behaviour we want when we don't know. |
| C | Explicit ε-greedy: with probability ε ignore the weights and pick uniformly (using `World.LocalRandom`). | Textbook answer, and legitimate here. But it adds visible randomness to production that will be hard to distinguish from a bug during playtest. |

### Q8. Window and cold start

| | Option | |
|---|---|---|
| A | **(Recommended)** **Whole match, cumulative, with the confidence ramp from Q7-B and a warm-up tick** before any adaptation applies (~5000 ticks, matching the existing `UnitDelays` scale at `ai.yaml:947-964`). | Simplest storage (two ints per type), and matches are short enough that "the whole match" *is* recent. The warm-up costs nothing and removes the worst noise. |
| B | Rolling window of the last ~2 minutes. | Adapts to phase changes (early infantry → late armour). But it makes the bot forgetful and jittery, and needs a ring buffer per type. |
| C | Exponential decay (`score = 0.99 * score + 0.01 * observation`). | One number per type, gives recency for free. Harder to reason about and to explain in a debug line; the half-life becomes another magic constant. |

### Q9. Per-match only, or persisted across matches?

| | Option | |
|---|---|---|
| A | **(Recommended)** **Per-match only.** State lives in the module instance, reset every game, and is serialised into `IGameSaveTraitData` (`UnitBuilderBotModule.cs:215-242`) so save/load doesn't silently revert it. | Matches the "adapts to *this* opponent" intent, avoids a persistence file, and sidesteps the fairness question entirely. Also means every playtest starts from a known state — important while we're debugging this. |
| B | Persist to the support directory and reload next match. | Genuinely "learns" over a session, which is the more exciting version. But it makes bugs non-reproducible, makes playtest feedback unattributable, and would need per-map/per-faction keying to mean anything. |
| C | Per-match, but log the final table so *we* can hand-tune `ai.yaml` between rounds. | Effectively "offline learning with a human in the loop". Strictly better than B for this project's actual workflow — and it composes with A rather than replacing it. Fold into Q10. |

Warning on B: state that survives across matches within one process would also be shared between two
SkyNet players (finding 5) unless carefully keyed.

### Q10. How is this observable?

A bot that silently changes its mind is untestable. Whatever we ship, we need to see it.

| | Option | |
|---|---|---|
| A | `AIUtils.BotDebug` lines on each weight change (`AIUtils.cs:77-81`, gated behind the existing `Debug.BotDebug` setting). | Free, zero new UI, uses machinery already in the codebase and already used throughout `BaseBuilderQueueManager`. Spammy if unthrottled. |
| B | **(Recommended)** A: BotDebug on change, **plus** a periodic dump (every ~60s) of the full table — `type, built, lost, killed, score, authoredWeight, currentWeight` — one line per type. | The per-change lines answer "why did it just build that", the table answers "what does it currently believe", which is the question you actually have while watching a match. Both are throttled and both fall out of existing helpers. |
| C | An in-game overlay widget. | Nicest to use, most work, and needs UI plumbing outside the bot modules. Defer. |

### Q11. Do defensive structures adapt, and what is a turret's "value destroyed"?

The players asked for "units **and turrets**". Turrets are a different problem: they never move, so
their score is dominated by *where the enemy chose to attack*, not by whether the turret is good.

| | Option | |
|---|---|---|
| A | **(Recommended)** **Units first; structures in a second pass.** Ship Q1–Q10 for `UnitsToBuild` only, leave `BuildingFractions` (`ai.yaml:871-887`) authored. | The building path is genuinely different code (`BaseBuilderQueueManager.cs:336-375`), with limit-redistribution semantics and hard economic dependencies (`proc`, `nuke`) that will punish a naive score. Half the feature, most of the visible benefit, a fraction of the risk. |
| B | Adapt defence structures too, scoring a turret with the same ROI formula as units, restricted to `gtwr, gun, atwr, obli, sam` (the existing `ShieldedDefenseTypes` list, `ai.yaml:810`). | Delivers the literal request. `atwr: 50` vs `sam: 1` vs `obli: 1` are wildly different today, so there is real room to move. But a turret that is never attacked scores 0 and gets suppressed — arguably the exact opposite of correct, since deterrence is invisible to this metric. |
| C | Adapt structures on a **survival**-based score instead (`value lost` only, no kills), so turrets that die a lot get built less. | Sidesteps the deterrence problem. But it rewards building turrets nobody can reach, i.e. useless ones. |

If B is chosen, `proc`, `nuke`/`nuk2`, `silo` and all production buildings must be in the excluded
set — they are already special-cased as priority overrides in `ChooseBuildingToBuild`
(`BaseBuilderQueueManager.cs:252-330`) and adaptation would fight that logic.

---

## Recommended smallest shippable version

Roughly 150–200 lines of C# and ~6 yaml fields:

1. **Weighted random selection** (Q1-B): new `WeightedUnitSelection` flag in
   `UnitBuilderBotModuleInfo`; when set, `ChooseRandomUnitToBuild` becomes a weighted draw over
   `UnitsToBuild` using `World.LocalRandom`. **This alone is a balance change and needs its own
   playtest — ideally before adaptation is layered on top.**
2. **Per-type ledger** (Q3-A): extend `UpdatesPlayerStatistics` to bucket `KillsCost` by
   `e.Attacker.Info.Name` and `DeathsCost` by victim type, into a
   `Dictionary<string,(int killed, int lost, int built)>` on `PlayerStatistics`. No new trait, no new
   yaml, already attached to every actor via `defaults.yaml:3`.
3. **ROI score** (Q2-B), **whole-match with warm-up** (Q8-A), **confidence ramp** (Q7-B),
   **clamped `[0.5x, 2x]`** (Q6-B), applied to a **per-module copy** of the weights (finding 5).
4. **Opt-in `AdaptiveTypes`** (Q5-B), starting with a handful: `e1, e3, arty, mlrs, ltnk, htnk,
   orca`. Everything else — economy, upgrades, `mcv`/`mhq` — untouched.
5. **Player-level economy gate** (Q4-A) using `PlayerResources.Earned`.
6. **BotDebug on change + 60s table dump** (Q10-B).

Explicitly deferred: defensive structures (Q11-A), per-harvester income (Q4-B), damage-share
attribution (Q3-B), cross-match persistence (Q9-B).

## Main risk

**The metric is confounded with the AI's own competence, and the tightest loop is the dangerous
one.** The score measures "did this type trade well" but what it actually captures is "did SkyNet
*use* this type well". Aircraft dying to AA (the Round-3 air problem) would read as "orcas are bad"
rather than "our air micro is bad", and the bot's response — build fewer orcas — removes the
pressure to fix the real bug and hides the symptom from us. `PLAN.md:436-440` already anticipates
this by sequencing adaptation after the walls/air rework; the sequencing needs to hold.

Second-order: with last-hit attribution (Q3-A), **support units are structurally undervalued**.
Artillery and rocket launchers soften targets that someone else finishes. Watch `arty` and `mlrs`
specifically — if their weights sag toward the floor across several matches while the bot's actual
performance drops, that is the tell that we need Q3-B.
