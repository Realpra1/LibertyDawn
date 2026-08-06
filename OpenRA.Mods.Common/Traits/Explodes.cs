#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.GameRules;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	public enum ExplosionType { Footprint, CenterPosition }

	public enum DamageSource { Self, Killer }

	[Desc("This actor explodes when killed.")]
	public class ExplodesInfo : ConditionalTraitInfo, Requires<IHealthInfo>
	{
		[WeaponReference]
		[FieldLoader.Require]
		[Desc("Default weapon to use for explosion if ammo/payload is loaded.")]
		public readonly string Weapon = null;

		[WeaponReference]
		[Desc("Fallback weapon to use for explosion if empty (no ammo/payload).")]
		public readonly string EmptyWeapon = "UnitExplode";

		[Desc("Chance that the explosion will use Weapon instead of EmptyWeapon when exploding, provided the actor has ammo/payload.")]
		public readonly int LoadedChance = 100;

		[Desc("Chance that this actor will explode at all.")]
		public readonly int Chance = 100;

		[Desc("Health level at which actor will explode.")]
		public readonly int DamageThreshold = 0;

		[Desc("DeathType(s) that trigger the explosion. Leave empty to always trigger an explosion.")]
		public readonly BitSet<DamageType> DeathTypes = default(BitSet<DamageType>);

		[Desc("Who is counted as source of damage for explosion.",
			"Possible values are Self and Killer.")]
		public readonly DamageSource DamageSource = DamageSource.Self;

		[Desc("Possible values are CenterPosition (explosion at the actors' center) and ",
			"Footprint (explosion on each occupied cell).")]
		public readonly ExplosionType Type = ExplosionType.CenterPosition;

		[Desc("Optional semantic impact type that world traits may suppress before this explosion fires.")]
		public readonly string ImpactType = null;

		[WeaponReference]
		[Desc("Optional weapon used instead when mutant creation is disabled.")]
		public readonly string NonMutatingWeapon = null;

		[Desc("Optional impact types keyed by carried Harvester resource type. The explosion is suppressed when every loaded resource maps to a suppressed impact type.")]
		public readonly Dictionary<string, string> LoadedResourceImpactTypes = new Dictionary<string, string>();

		[Desc("Offset of the explosion from the center of the exploding actor (or cell).")]
		public readonly WVec Offset = WVec.Zero;

		public WeaponInfo WeaponInfo { get; private set; }
		public WeaponInfo EmptyWeaponInfo { get; private set; }
		public WeaponInfo NonMutatingWeaponInfo { get; private set; }

		public override object Create(ActorInitializer init) { return new Explodes(this, init.Self); }
		public override void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			if (!string.IsNullOrEmpty(Weapon))
			{
				var weaponToLower = Weapon.ToLowerInvariant();
				if (!rules.Weapons.TryGetValue(weaponToLower, out var weapon))
					throw new YamlException($"Weapons Ruleset does not contain an entry '{weaponToLower}'");
				WeaponInfo = weapon;
			}

			if (!string.IsNullOrEmpty(EmptyWeapon))
			{
				var emptyWeaponToLower = EmptyWeapon.ToLowerInvariant();
				if (!rules.Weapons.TryGetValue(emptyWeaponToLower, out var emptyWeapon))
					throw new YamlException($"Weapons Ruleset does not contain an entry '{emptyWeaponToLower}'");
				EmptyWeaponInfo = emptyWeapon;
			}

			if (!string.IsNullOrEmpty(NonMutatingWeapon))
			{
				var nonMutatingWeaponToLower = NonMutatingWeapon.ToLowerInvariant();
				if (!rules.Weapons.TryGetValue(nonMutatingWeaponToLower, out var nonMutatingWeapon))
					throw new YamlException($"Weapons Ruleset does not contain an entry '{nonMutatingWeaponToLower}'");
				NonMutatingWeaponInfo = nonMutatingWeapon;
			}

			base.RulesetLoaded(rules, ai);
		}
	}

	public class Explodes : ConditionalTrait<ExplodesInfo>, INotifyKilled, INotifyDamage
	{
		readonly IHealth health;
		readonly Harvester harvester;
		BuildingInfo buildingInfo;
		Armament[] armaments;

		public Explodes(ExplodesInfo info, Actor self)
			: base(info)
		{
			health = self.Trait<IHealth>();
			harvester = self.TraitOrDefault<Harvester>();
		}

		protected override void Created(Actor self)
		{
			buildingInfo = self.Info.TraitInfoOrDefault<BuildingInfo>();
			armaments = self.TraitsImplementing<Armament>().ToArray();

			base.Created(self);
		}

		void INotifyKilled.Killed(Actor self, AttackInfo e)
		{
			if (IsTraitDisabled || !self.IsInWorld)
				return;

			if (self.World.SharedRandom.Next(100) > Info.Chance)
				return;

			if (!Info.DeathTypes.IsEmpty && !e.Damage.DamageTypes.Overlaps(Info.DeathTypes))
				return;

			if (IsImpactSuppressed(self, Info.ImpactType) || LoadedResourceExplosionIsSuppressed(
				harvester?.Contents, Info.LoadedResourceImpactTypes,
				impactType => IsImpactSuppressed(self, impactType)))
				return;

			var weapon = ChooseWeaponForExplosion(self);
			if (weapon == null)
				return;

			if (NonMutatingImpactIsRequired(self.World, Info.ImpactType) && Info.NonMutatingWeaponInfo != null)
				weapon = Info.NonMutatingWeaponInfo;

			var source = Info.DamageSource == DamageSource.Self ? self : e.Attacker;
			if (weapon.Report != null && weapon.Report.Any())
				Game.Sound.Play(SoundType.World, weapon.Report, self.World, self.CenterPosition);

			if (Info.Type == ExplosionType.Footprint && buildingInfo != null)
			{
				var cells = buildingInfo.OccupiedTiles(self.Location);
				foreach (var c in cells)
					weapon.Impact(Target.FromPos(self.World.Map.CenterOfCell(c) + Info.Offset), source);

				return;
			}

			// Use .FromPos since this actor is killed. Cannot use Target.FromActor
			weapon.Impact(Target.FromPos(self.CenterPosition + Info.Offset), source);
		}

		static bool IsImpactSuppressed(Actor self, string impactType)
		{
			return !string.IsNullOrEmpty(impactType) &&
				!self.TraitsImplementing<IImpactTypeSuppressionBypass>()
					.Any(b => b.BypassImpactSuppression(impactType)) && self.World.WorldActor
				.TraitsImplementing<IImpactTypeSuppressor>().Any(s => s.SuppressImpact(impactType));
		}

		static bool NonMutatingImpactIsRequired(World world, string impactType)
		{
			return !string.IsNullOrEmpty(impactType) && world.WorldActor
				.TraitsImplementing<IMutantCreationSuppressor>().Any(s => s.SuppressMutantCreation);
		}

		public static bool LoadedResourceExplosionIsSuppressed(IReadOnlyDictionary<string, int> contents,
			IReadOnlyDictionary<string, string> impactTypes, Func<string, bool> isSuppressed)
		{
			if (contents == null || contents.Count == 0 || impactTypes == null || impactTypes.Count == 0)
				return false;

			var foundPayload = false;
			foreach (var content in contents)
			{
				if (content.Value <= 0)
					continue;

				foundPayload = true;
				if (!impactTypes.TryGetValue(content.Key, out var impactType) || !isSuppressed(impactType))
					return false;
			}

			return foundPayload;
		}

		WeaponInfo ChooseWeaponForExplosion(Actor self)
		{
			if (armaments.Length == 0)
				return Info.WeaponInfo;
			else if (self.World.SharedRandom.Next(100) > Info.LoadedChance)
				return Info.EmptyWeaponInfo;

			// PERF: Avoid LINQ
			foreach (var a in armaments)
				if (!a.IsReloading)
					return Info.WeaponInfo;

			return Info.EmptyWeaponInfo;
		}

		void INotifyDamage.Damaged(Actor self, AttackInfo e)
		{
			if (Info.DamageThreshold == 0 || IsTraitDisabled || !self.IsInWorld)
				return;

			if (!Info.DeathTypes.IsEmpty && !e.Damage.DamageTypes.Overlaps(Info.DeathTypes))
				return;

			// Cast to long to avoid overflow when multiplying by the health
			var source = Info.DamageSource == DamageSource.Self ? self : e.Attacker;
			if (health.HP * 100L < Info.DamageThreshold * (long)health.MaxHP)
				self.World.AddFrameEndTask(w => self.Kill(source, e.Damage.DamageTypes));
		}
	}
}
