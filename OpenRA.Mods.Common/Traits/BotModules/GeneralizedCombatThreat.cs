#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. You can redistribute
 * it and/or modify it under the terms of the GNU General Public License.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.GameRules;
using OpenRA.Mods.Common.Projectiles;
using OpenRA.Mods.Common.Warheads;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>
	/// Immutable, rules-derived combat comparison. This is deliberately a helper only:
	/// no bot module consumes it until a separately reviewed task opts in.
	/// </summary>
	public sealed class GeneralizedCombatThreatCalculator
	{
		public readonly struct SplashZone
		{
			public readonly double InnerRadiusCells;
			public readonly double OuterRadiusCells;
			public readonly double InnerDamageFraction;
			public readonly double OuterDamageFraction;

			public SplashZone(double innerRadiusCells, double outerRadiusCells,
				double innerDamageFraction, double outerDamageFraction)
			{
				InnerRadiusCells = innerRadiusCells;
				OuterRadiusCells = outerRadiusCells;
				InnerDamageFraction = innerDamageFraction;
				OuterDamageFraction = outerDamageFraction;
			}

			public double AffectedCells => Math.Max(0,
				OuterRadiusCells * OuterRadiusCells - InnerRadiusCells * InnerRadiusCells);

			// SpreadDamage interpolates linearly with radius. Integrate that line against
			// d(radius^2), because outer portions of an annulus contain more affected cells.
			public double WeightedAffectedCells
			{
				get
				{
					var width = OuterRadiusCells - InnerRadiusCells;
					if (width <= 0)
						return 0;

					var area = AffectedCells;
					var slope = (OuterDamageFraction - InnerDamageFraction) / width;
					var radialMoment = (Math.Pow(OuterRadiusCells, 3) - Math.Pow(InnerRadiusCells, 3)) / 3 -
						InnerRadiusCells * area / 2;
					return InnerDamageFraction * area + 2 * slope * radialMoment;
				}
			}
		}

		public sealed class DirectionalThreat
		{
			public string Attacker { get; internal set; }
			public string Defender { get; internal set; }
			public bool CanTarget { get; internal set; }
			public int DefenderHitPoints { get; internal set; }
			public string DefenderArmor { get; internal set; }
			public double RangeCells { get; internal set; }
			public double NominalRangeCells { get; internal set; }
			public double MinimumRangeCells { get; internal set; }
			public double ProjectileSpeedCellsPerTick { get; internal set; }
			public double TargetSpeedCellsPerTick { get; internal set; }
			public double DefenderHitRadiusCells { get; internal set; }
			public double InaccuracyCells { get; internal set; }
			public double ExpectedHitChance { get; internal set; }
			public double SplashFactor { get; internal set; }
			public double SplashAndInaccuracyMultiplier { get; internal set; }
			public double DamagePerCycle { get; internal set; }
			public double CycleTicks { get; internal set; }
			public double DamagePerTick { get; internal set; }
			public double RangeMultiplier { get; internal set; } = 1;
			public double EffectiveDamagePerTick => DamagePerTick * RangeMultiplier;
			public double RawKillRate => DefenderHitPoints > 0 ? DamagePerTick / DefenderHitPoints : 0;
			public double KillRate => DefenderHitPoints > 0 ? EffectiveDamagePerTick / DefenderHitPoints : 0;
			public IReadOnlyList<SplashZone> SplashZones { get; internal set; } = Array.Empty<SplashZone>();
		}

		public sealed class PairThreat
		{
			public DirectionalThreat Forward { get; internal set; }
			public DirectionalThreat Reverse { get; internal set; }
			public double AttackerVeterancyFactor { get; internal set; } = 1;
			public double DefenderVeterancyFactor { get; internal set; } = 1;
			public double DefenderThreatInAttackerEquivalents { get; internal set; }
			public double AttackerThreatInDefenderEquivalents { get; internal set; }
		}

		readonly Dictionary<(string Attacker, string Defender), PairThreat> cache;
		public IReadOnlyDictionary<(string Attacker, string Defender), PairThreat> Cache => cache;
		public static int CanonicalPairCount(int actorCount) => actorCount * (actorCount + 1) / 2;

		public static (string First, string Second) CanonicalKey(string first, string second)
		{
			first = first.ToLowerInvariant();
			second = second.ToLowerInvariant();
			return string.CompareOrdinal(first, second) <= 0 ? (first, second) : (second, first);
		}

		public GeneralizedCombatThreatCalculator(Ruleset rules)
		{
			var combatActors = rules.Actors.Values
				.Where(a => !a.Name.StartsWith(ActorInfo.AbstractActorPrefix, StringComparison.Ordinal)
					&& a.HasTraitInfo<IHealthInfo>() && a.HasTraitInfo<ITargetableInfo>()
					&& a.HasTraitInfo<ArmamentInfo>())
				.OrderBy(a => a.Name, StringComparer.Ordinal)
				.ToArray();

			cache = new Dictionary<(string, string), PairThreat>(CanonicalPairCount(combatActors.Length));
			for (var attackerIndex = 0; attackerIndex < combatActors.Length; attackerIndex++)
				for (var defenderIndex = attackerIndex; defenderIndex < combatActors.Length; defenderIndex++)
				{
					var attacker = combatActors[attackerIndex];
					var defender = combatActors[defenderIndex];
					cache.Add((attacker.Name, defender.Name), CalculatePair(attacker, defender));
				}
		}

		public bool TryGet(string attacker, string defender, out PairThreat threat)
		{
			var key = CanonicalKey(attacker, defender);
			if (key.First == attacker.ToLowerInvariant())
				return cache.TryGetValue((key.First, key.Second), out threat);

			if (!cache.TryGetValue((key.First, key.Second), out var canonical))
			{
				threat = null;
				return false;
			}

			threat = Reverse(canonical);
			return true;
		}

		/// <summary>
		/// Fast actor lookup using the immutable type-pair cache plus the deliberately
		/// simple CNC veterancy factors. Other live state is intentionally ignored.
		/// </summary>
		public bool TryGetCached(Actor attacker, Actor defender, out PairThreat threat)
		{
			var attackerLevel = attacker.TraitOrDefault<GainsExperience>()?.Level ?? 0;
			var defenderLevel = defender.TraitOrDefault<GainsExperience>()?.Level ?? 0;
			return TryGetCached(attacker.Info.Name, defender.Info.Name, attackerLevel, defenderLevel, out threat);
		}

		public bool TryGetCached(string attacker, string defender, int attackerVeterancyLevel,
			int defenderVeterancyLevel, out PairThreat threat)
		{
			if (!TryGet(attacker, defender, out var baseline))
			{
				threat = null;
				return false;
			}

			threat = ApplyVeterancyFactors(baseline, VeterancyFactor(attackerVeterancyLevel),
				VeterancyFactor(defenderVeterancyLevel));
			return true;
		}

		public static double VeterancyFactor(int level)
		{
			switch (level.Clamp(0, 3))
			{
				case 1: return 1.25;
				case 2: return 1.5625;
				case 3: return 2.44;
				default: return 1;
			}
		}

		static PairThreat ApplyVeterancyFactors(PairThreat baseline, double attackerFactor, double defenderFactor)
		{
			return new PairThreat
			{
				Forward = baseline.Forward,
				Reverse = baseline.Reverse,
				AttackerVeterancyFactor = attackerFactor,
				DefenderVeterancyFactor = defenderFactor,
				DefenderThreatInAttackerEquivalents = ScaleCachedExchange(
					baseline.DefenderThreatInAttackerEquivalents, defenderFactor, attackerFactor),
				AttackerThreatInDefenderEquivalents = ScaleCachedExchange(
					baseline.AttackerThreatInDefenderEquivalents, attackerFactor, defenderFactor)
			};
		}

		public static double ScaleCachedExchange(double baseline, double subjectFactor, double opponentFactor)
		{
			return baseline * subjectFactor / opponentFactor;
		}

		public IEnumerable<PairThreat> OrderedPairs()
		{
			foreach (var pair in cache.Values)
			{
				yield return pair;
				if (pair.Forward.Attacker != pair.Forward.Defender)
					yield return Reverse(pair);
			}
		}

		/// <summary>
		/// Recalculates from effective live traits instead of trusting the immutable baseline.
		/// This intentionally bypasses the rules cache so veterancy, conditions, current HP,
		/// ammo exhaustion, disabled armaments, and transformations cannot return stale data.
		/// </summary>
		public PairThreat CalculateLive(Actor attacker, Actor defender)
		{
			var forward = CalculateLiveDirection(attacker, defender);
			var reverse = CalculateLiveDirection(defender, attacker);
			return CreatePair(forward, reverse);
		}

		static PairThreat Reverse(PairThreat pair)
		{
			return new PairThreat
			{
				Forward = pair.Reverse,
				Reverse = pair.Forward,
				AttackerVeterancyFactor = pair.DefenderVeterancyFactor,
				DefenderVeterancyFactor = pair.AttackerVeterancyFactor,
				DefenderThreatInAttackerEquivalents = pair.AttackerThreatInDefenderEquivalents,
				AttackerThreatInDefenderEquivalents = pair.DefenderThreatInAttackerEquivalents
			};
		}

		PairThreat CalculatePair(ActorInfo attacker, ActorInfo defender)
		{
			var forward = CalculateDirection(attacker, defender);
			var reverse = CalculateDirection(defender, attacker);
			return CreatePair(forward, reverse);
		}

		static PairThreat CreatePair(DirectionalThreat forward, DirectionalThreat reverse)
		{
			forward.RangeMultiplier = EffectiveRangeFactor(forward.RangeCells, reverse.NominalRangeCells);
			reverse.RangeMultiplier = EffectiveRangeFactor(reverse.RangeCells, forward.NominalRangeCells);

			return new PairThreat
			{
				Forward = forward,
				Reverse = reverse,
				DefenderThreatInAttackerEquivalents = RangeAdjustedThreatEquivalent(
					reverse.RawKillRate, forward.RawKillRate, reverse.RangeCells, forward.NominalRangeCells),
				AttackerThreatInDefenderEquivalents = RangeAdjustedThreatEquivalent(
					forward.RawKillRate, reverse.RawKillRate, forward.RangeCells, reverse.NominalRangeCells)
			};
		}

		public static double ThreatEquivalent(double incomingKillRate, double outgoingKillRate)
		{
			if (incomingKillRate <= 0)
				return 0;

			return outgoingKillRate > 0 ? incomingKillRate / outgoingKillRate : double.PositiveInfinity;
		}

		public static double EffectiveRangeFactor(double enemyEffectiveRangeCells, double ownBaseRangeCells)
		{
			return ownBaseRangeCells > 0 ? enemyEffectiveRangeCells / ownBaseRangeCells : 0;
		}

		public static double RangeAdjustedThreatEquivalent(double incomingRawKillRate, double outgoingRawKillRate,
			double enemyEffectiveRangeCells, double ownBaseRangeCells)
		{
			var rangeFactor = EffectiveRangeFactor(enemyEffectiveRangeCells, ownBaseRangeCells);
			return rangeFactor > 0 ? ThreatEquivalent(incomingRawKillRate, outgoingRawKillRate) * rangeFactor : 0;
		}

		public static double SumDefenderThreatInAttackerEquivalents(IEnumerable<PairThreat> matchups)
		{
			return matchups.Sum(m => m.DefenderThreatInAttackerEquivalents);
		}

		static DirectionalThreat CalculateDirection(ActorInfo attacker, ActorInfo defender)
		{
			var hp = defender.TraitInfo<IHealthInfo>().MaxHP;
			var targetTypes = default(BitSet<TargetableType>);
			foreach (var targetable in defender.TraitInfos<ITargetableInfo>())
				targetTypes = targetTypes.Union(targetable.GetTargetTypes());
			var armor = defender.TraitInfos<ArmorInfo>().Select(a => a.Type).FirstOrDefault(a => a != null);
			var hitRadius = defender.TraitInfos<HitShapeInfo>()
				.Select(h => Cells(h.Type.OuterRadius)).DefaultIfEmpty(0.5).Max();
			var targetSpeed = MovementSpeedCellsPerTick(defender);
			var targetEngagementRange = defender.TraitInfos<ArmamentInfo>()
				.Where(a => a.WeaponInfo != null).Select(a => Cells(a.ModifiedRange)).DefaultIfEmpty(0).Max();

			var applicable = attacker.TraitInfos<ArmamentInfo>()
				.Where(a => a.WeaponInfo != null && a.WeaponInfo.IsValidTarget(targetTypes))
				.Select(a => CalculateArmament(a, armor, hitRadius, a.ModifiedRange,
					Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>(), null, null,
					targetSpeed, targetEngagementRange))
				.ToArray();

			return CombineDirections(attacker.Name, defender.Name, hp, armor, hitRadius, applicable);
		}

		static DirectionalThreat CalculateLiveDirection(Actor attacker, Actor defender)
		{
			var health = defender.TraitOrDefault<IHealth>();
			var hp = health?.HP ?? 0;
			var targetTypes = defender.GetEnabledTargetTypes();
			var armor = defender.TraitsImplementing<Armor>()
				.Where(a => !a.IsTraitDisabled).Select(a => a.Info.Type).FirstOrDefault(a => a != null);
			var hitRadius = defender.TraitsImplementing<HitShape>()
				.Where(h => !h.IsTraitDisabled).Select(h => Cells(h.Info.Type.OuterRadius)).DefaultIfEmpty(0.5).Max();
			var targetSpeed = MovementSpeedCellsPerTick(defender);
			var targetEngagementRange = defender.TraitsImplementing<Armament>()
				.Where(a => !a.IsTraitDisabled && !a.IsTraitPaused).Select(a => Cells(a.MaxRange())).DefaultIfEmpty(0).Max();
			var firepowerModifiers = attacker.TraitsImplementing<IFirepowerModifier>().Select(m => m.GetFirepowerModifier()).ToArray();
			var reloadModifiers = attacker.TraitsImplementing<IReloadModifier>().Select(m => m.GetReloadModifier()).ToArray();
			var inaccuracyModifiers = attacker.TraitsImplementing<IInaccuracyModifier>().Select(m => m.GetInaccuracyModifier()).ToArray();
			var ammoPools = attacker.TraitsImplementing<AmmoPool>().ToArray();
			var applicable = attacker.TraitsImplementing<Armament>()
				.Where(a => !a.IsTraitDisabled && !a.IsTraitPaused && a.Weapon.IsValidTarget(targetTypes))
				.Where(a => ammoPools.Where(p => p.Info.Armaments.Contains(a.Info.Name))
					.All(p => p.CurrentAmmoCount >= a.Info.AmmoUsage))
				.Select(a => CalculateArmament(a.Info, armor, hitRadius, a.MaxRange(),
					firepowerModifiers, reloadModifiers, inaccuracyModifiers, attacker, defender,
					targetSpeed, targetEngagementRange))
				.ToArray();

			return CombineDirections(attacker.Info.Name, defender.Info.Name, hp, armor, hitRadius, applicable);
		}

		static DirectionalThreat CombineDirections(string attacker, string defender, int hp, string armor,
			double hitRadius, DirectionalThreat[] applicable)
		{
			var totalDpt = applicable.Sum(a => a.DamagePerTick);
			var weight = applicable.Sum(a => a.DamagePerCycle);
			return new DirectionalThreat
			{
				Attacker = attacker,
				Defender = defender,
				CanTarget = applicable.Any(a => a.DamagePerTick > 0),
				DefenderHitPoints = hp,
				DefenderArmor = armor ?? "none",
				RangeCells = applicable.Select(a => a.RangeCells).DefaultIfEmpty(0).Max(),
				NominalRangeCells = applicable.Select(a => a.NominalRangeCells).DefaultIfEmpty(0).Max(),
				MinimumRangeCells = applicable.Select(a => a.MinimumRangeCells).DefaultIfEmpty(0).Min(),
				ProjectileSpeedCellsPerTick = applicable.Select(a => a.ProjectileSpeedCellsPerTick).DefaultIfEmpty(0).Max(),
				TargetSpeedCellsPerTick = applicable.Select(a => a.TargetSpeedCellsPerTick).DefaultIfEmpty(0).Max(),
				DefenderHitRadiusCells = hitRadius,
				InaccuracyCells = Weighted(applicable, a => a.InaccuracyCells, weight),
				ExpectedHitChance = Weighted(applicable, a => a.ExpectedHitChance, weight),
				SplashFactor = Weighted(applicable, a => a.SplashFactor, weight),
				SplashAndInaccuracyMultiplier = Weighted(applicable, a => a.SplashAndInaccuracyMultiplier, weight),
				DamagePerCycle = applicable.Sum(a => a.DamagePerCycle),
				CycleTicks = Weighted(applicable, a => a.CycleTicks, weight),
				DamagePerTick = totalDpt,
				SplashZones = applicable.SelectMany(a => a.SplashZones).ToArray()
			};
		}

		static DirectionalThreat CalculateArmament(ArmamentInfo armament, string armor, double hitRadius,
			WDist effectiveRange, int[] firepowerModifiers, int[] reloadModifiers, int[] inaccuracyModifiers,
			Actor attacker, Actor defender, double targetSpeedCellsPerTick, double targetEngagementRangeCells)
		{
			var weapon = armament.WeaponInfo;
			var rangeZones = weapon.Warheads.OfType<SpreadDamageWarhead>().SelectMany(SplashZones).ToArray();
			var splashRadius = rangeZones.Where(z => z.WeightedAffectedCells > 0)
				.Select(z => z.OuterRadiusCells).DefaultIfEmpty(0).Max();
			var effectiveHitRadius = Math.Max(hitRadius, splashRadius);
			var projectile = ProjectileMovement(weapon.Projectile);
			var nominalRangeCells = Cells(effectiveRange);
			var minimumRangeCells = Cells(weapon.MinRange);
			var movementLimitedRangeCells = EffectiveRangeCells(nominalRangeCells, minimumRangeCells,
				projectile.SpeedCellsPerTick, targetSpeedCellsPerTick, effectiveHitRadius,
				projectile.IsInstant, projectile.IsHoming, targetEngagementRangeCells);
			effectiveRange = new WDist((int)(movementLimitedRangeCells * 1024));
			var inaccuracy = weapon.TargetActorCenter && weapon.Projectile is InstantHitInfo ? 0 :
				ProjectileInaccuracyCells(weapon.Projectile, effectiveRange, inaccuracyModifiers);
			var damagingWarheads = weapon.Warheads.OfType<DamageWarhead>().Where(w => w.Damage > 0).ToArray();
			var raw = 0d;
			var effective = 0d;
			var weightedHitChance = 0d;
			var weightedSplash = 0d;
			var weightedMultiplier = 0d;
			var allZones = new List<SplashZone>();
			foreach (var warhead in damagingWarheads)
			{
				var warheadDamage = Util.ApplyPercentageModifiers(warhead.Damage,
					firepowerModifiers.Append(Versus(warhead, armor)));
				if (defender != null)
					warheadDamage = ApplyDamageModifiers(warheadDamage, warhead.DamageTypes, attacker, defender);
				var zones = warhead is SpreadDamageWarhead spread ? SplashZones(spread) : Array.Empty<SplashZone>();
				var splash = zones.Count == 0 ? 1 : SplashFactor(zones);
				var warheadSplashRadius = zones.Select(z => z.OuterRadiusCells).DefaultIfEmpty(0).Max();
				var hitChance = ExpectedHitChance(Math.Max(hitRadius, warheadSplashRadius), inaccuracy);
				var multiplier = hitChance * splash;
				raw += warheadDamage;
				effective += warheadDamage * multiplier;
				weightedHitChance += warheadDamage * hitChance;
				weightedSplash += warheadDamage * splash;
				weightedMultiplier += warheadDamage * multiplier;
				allZones.AddRange(zones);
			}

			var hitChanceAverage = raw > 0 ? weightedHitChance / raw : 0;
			var splashAverage = raw > 0 ? weightedSplash / raw : 0;
			var multiplierAverage = raw > 0 ? weightedMultiplier / raw : 0;
			var burst = Math.Max(1, weapon.Burst);
			var burstDelay = weapon.BurstDelays.Length == 0 ? 0 :
				Enumerable.Range(0, Math.Max(0, burst - 1)).Sum(i => weapon.BurstDelays[Math.Min(i, weapon.BurstDelays.Length - 1)]);
			var cycle = Math.Max(1, Util.ApplyPercentageModifiers(weapon.ReloadDelay, reloadModifiers) + burstDelay);

			return new DirectionalThreat
			{
				RangeCells = movementLimitedRangeCells,
				NominalRangeCells = nominalRangeCells,
				MinimumRangeCells = minimumRangeCells,
				ProjectileSpeedCellsPerTick = projectile.SpeedCellsPerTick,
				TargetSpeedCellsPerTick = targetSpeedCellsPerTick,
				InaccuracyCells = inaccuracy,
				ExpectedHitChance = hitChanceAverage,
				SplashFactor = splashAverage,
				SplashAndInaccuracyMultiplier = multiplierAverage,
				DamagePerCycle = effective * burst,
				CycleTicks = cycle,
				DamagePerTick = effective * burst / cycle,
				SplashZones = allZones
			};
		}

		static double Weighted(IEnumerable<DirectionalThreat> values, Func<DirectionalThreat, double> selector, double weight)
		{
			return weight > 0 ? values.Sum(v => selector(v) * v.DamagePerCycle) / weight : 0;
		}

		static int Versus(DamageWarhead warhead, string armor)
		{
			if (armor == null || !warhead.Versus.TryGetValue(armor, out var percentage))
				return 100;

			return percentage;
		}

		static int ApplyDamageModifiers(int value, BitSet<DamageType> damageTypes, Actor attacker, Actor defender)
		{
			var damage = new Damage(value, damageTypes);
			var applied = (decimal)value;
			foreach (var modifier in defender.TraitsImplementing<IDamageModifier>())
				applied *= modifier.GetDamageModifier(attacker, damage) / 100m;

			if (defender.Owner?.PlayerActor != null)
				foreach (var modifier in defender.Owner.PlayerActor.TraitsImplementing<IDamageModifier>())
					applied *= modifier.GetDamageModifier(attacker, damage) / 100m;

			return (int)applied;
		}

		public static double Cells(WDist distance) => distance.Length / 1024d;

		public static double ExpectedHitChance(double effectiveHitRadiusCells, double inaccuracyCells)
		{
			if (inaccuracyCells <= 0)
				return 1;

			return (effectiveHitRadiusCells / inaccuracyCells).Clamp(0, 1);
		}

		public static double EffectiveRangeCells(double maximumRangeCells, double minimumRangeCells,
			double projectileSpeedCellsPerTick, double targetSpeedCellsPerTick, double effectiveHitRadiusCells,
			bool isInstant, bool isHoming, double targetEngagementRangeCells)
		{
			if (maximumRangeCells <= minimumRangeCells)
				return 0;

			if (isInstant || targetSpeedCellsPerTick <= 0)
				return maximumRangeCells;

			double effectiveRange;
			if (isHoming)
				effectiveRange = targetSpeedCellsPerTick > projectileSpeedCellsPerTick ?
					targetEngagementRangeCells : maximumRangeCells;
			else if (projectileSpeedCellsPerTick <= 0)
				effectiveRange = 0;
			else
				effectiveRange = effectiveHitRadiusCells * projectileSpeedCellsPerTick / targetSpeedCellsPerTick;

			effectiveRange = Math.Min(maximumRangeCells, effectiveRange);
			return effectiveRange > minimumRangeCells ? effectiveRange : 0;
		}

		static (double SpeedCellsPerTick, bool IsInstant, bool IsHoming) ProjectileMovement(IProjectileInfo projectile)
		{
			if (projectile == null || projectile is InstantHitInfo || projectile is RailgunInfo ||
				projectile is AreaBeamInfo || projectile is LaserZapInfo)
				return (double.PositiveInfinity, true, false);

			if (projectile is BulletInfo bullet)
				return (bullet.Speed.Select(Cells).Average(), false, false);

			if (projectile is MissileInfo missile)
				return (Cells(missile.Speed), false, missile.LockOnProbability > 0);

			var speedField = projectile.GetType().GetField("Speed");
			if (speedField?.GetValue(projectile) is WDist speed)
				return (Cells(speed), false, false);

			if (speedField?.GetValue(projectile) is WDist[] speeds && speeds.Length > 0)
				return (speeds.Select(Cells).Average(), false, false);

			return (double.PositiveInfinity, true, false);
		}

		static double MovementSpeedCellsPerTick(ActorInfo actor)
		{
			var mobile = actor.TraitInfoOrDefault<MobileInfo>();
			if (mobile != null)
				return mobile.Speed / 1024d;

			var aircraft = actor.TraitInfoOrDefault<AircraftInfo>();
			return aircraft != null ? aircraft.Speed / 1024d : 0;
		}

		static double MovementSpeedCellsPerTick(Actor actor)
		{
			var speed = MovementSpeedCellsPerTick(actor.Info);
			if (speed <= 0)
				return 0;

			return Util.ApplyPercentageModifiers((int)(speed * 1024),
				actor.TraitsImplementing<ISpeedModifier>().Select(m => m.GetSpeedModifier())) / 1024d;
		}

		public static double SplashFactor(IEnumerable<SplashZone> zones)
		{
			// One directly hit cell is the lower bound. Zone radii themselves are never
			// clamped: an inner radius may legitimately be below or above one cell.
			return Math.Max(1, zones.Sum(z => z.WeightedAffectedCells));
		}

		public static IReadOnlyList<SplashZone> SplashZones(SpreadDamageWarhead warhead)
		{
			if (warhead.Range == null || warhead.Range.Length == 0 || warhead.Falloff.Length == 0)
				return Array.Empty<SplashZone>();

			var zones = new List<SplashZone>();
			var firstRadius = Cells(warhead.Range[0]);
			if (firstRadius > 0)
				zones.Add(new SplashZone(0, firstRadius, warhead.Falloff[0] / 100d, warhead.Falloff[0] / 100d));

			for (var i = 1; i < warhead.Range.Length; i++)
				zones.Add(new SplashZone(Cells(warhead.Range[i - 1]), Cells(warhead.Range[i]),
					warhead.Falloff[i - 1] / 100d, warhead.Falloff[i] / 100d));

			return zones;
		}

		static double ProjectileInaccuracyCells(IProjectileInfo projectile, WDist range, int[] modifiers)
		{
			if (projectile == null)
				return 0;

			var field = projectile.GetType().GetField("Inaccuracy");
			if (!(field?.GetValue(projectile) is WDist configured))
				return 0;

			var typeField = projectile.GetType().GetField("InaccuracyType");
			var type = typeField?.GetValue(projectile) is InaccuracyType value ? value : InaccuracyType.Maximum;
			configured = new WDist(Util.ApplyPercentageModifiers(configured.Length, modifiers));
			return type == InaccuracyType.PerCellIncrement ? Cells(configured) * Cells(range) : Cells(configured);
		}
	}
}
