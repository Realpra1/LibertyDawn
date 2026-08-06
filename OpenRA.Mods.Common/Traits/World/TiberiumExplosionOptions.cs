#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. You can redistribute
 * it and/or modify it under the terms of the GNU General Public License as
 * published by the Free Software Foundation, either version 3 of the License,
 * or (at your option) any later version.
 */
#endregion

using System;
using System.Collections.Generic;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Controls Tiberium explosion and mutation lobby options.")]
	public class TiberiumExplosionOptionsInfo : TraitInfo, ILobbyOptions
	{
		public const string RedOptionId = "noredtiberiumexplosions";
		public const string BlueOptionId = "nobluetiberiumexplosions";
		public const string MutantsOptionId = "nomutants";

		[Desc("Semantic impact type used by red Tiberium explosions.")]
		public readonly string RedImpactType = "RedTiberiumExplosion";

		[Desc("Semantic impact type used by blue Tiberium explosions.")]
		public readonly string BlueImpactType = "BlueTiberiumExplosion";

		public readonly string RedCheckboxLabel = "No red Tiberium explosions";
		public readonly string RedCheckboxDescription = "Prevents red Tiberium from exploding";
		public readonly bool RedCheckboxEnabled = false;
		public readonly bool RedCheckboxLocked = false;
		public readonly bool RedCheckboxVisible = true;
		public readonly int RedCheckboxDisplayOrder = 20;

		public readonly string BlueCheckboxLabel = "No blue Tiberium explosions";
		public readonly string BlueCheckboxDescription = "Prevents blue Tiberium from exploding";
		public readonly bool BlueCheckboxEnabled = false;
		public readonly bool BlueCheckboxLocked = false;
		public readonly bool BlueCheckboxVisible = true;
		public readonly int BlueCheckboxDisplayOrder = 21;

		public readonly string MutantsCheckboxLabel = "No mutants ever";
		public readonly string MutantsCheckboxDescription = "Prevents new mutants from being created";
		public readonly bool MutantsCheckboxEnabled = false;
		public readonly bool MutantsCheckboxLocked = false;
		public readonly bool MutantsCheckboxVisible = true;
		public readonly int MutantsCheckboxDisplayOrder = 22;

		[Desc("Write semantic explosion option decisions to debug.log.")]
		public readonly bool DebugLogging = false;

		IEnumerable<LobbyOption> ILobbyOptions.LobbyOptions(MapPreview map)
		{
			yield return new LobbyBooleanOption(RedOptionId, RedCheckboxLabel, RedCheckboxDescription,
				RedCheckboxVisible, RedCheckboxDisplayOrder, RedCheckboxEnabled, RedCheckboxLocked);
			yield return new LobbyBooleanOption(BlueOptionId, BlueCheckboxLabel, BlueCheckboxDescription,
				BlueCheckboxVisible, BlueCheckboxDisplayOrder, BlueCheckboxEnabled, BlueCheckboxLocked);
			yield return new LobbyBooleanOption(MutantsOptionId, MutantsCheckboxLabel, MutantsCheckboxDescription,
				MutantsCheckboxVisible, MutantsCheckboxDisplayOrder, MutantsCheckboxEnabled, MutantsCheckboxLocked);
		}

		public override object Create(ActorInitializer init) { return new TiberiumExplosionOptions(init.Self, this); }
	}

	public class TiberiumExplosionOptions : IImpactTypeSuppressor, IMutantCreationSuppressor,
		IActorCreationSuppressor, INotifyCreated
	{
		readonly TiberiumExplosionOptionsInfo info;
		readonly World world;
		bool noRedExplosions;
		bool noBlueExplosions;
		bool noMutants;

		public TiberiumExplosionOptions(Actor self, TiberiumExplosionOptionsInfo info)
		{
			this.info = info;
			world = self.World;
		}

		void INotifyCreated.Created(Actor self)
		{
			noRedExplosions = self.World.LobbyInfo.GlobalSettings
				.OptionOrDefault(TiberiumExplosionOptionsInfo.RedOptionId, info.RedCheckboxEnabled);
			noBlueExplosions = self.World.LobbyInfo.GlobalSettings
				.OptionOrDefault(TiberiumExplosionOptionsInfo.BlueOptionId, info.BlueCheckboxEnabled);
			noMutants = self.World.LobbyInfo.GlobalSettings
				.OptionOrDefault(TiberiumExplosionOptionsInfo.MutantsOptionId, info.MutantsCheckboxEnabled);
		}

		bool IMutantCreationSuppressor.SuppressMutantCreation => noMutants;

		bool IActorCreationSuppressor.SuppressActorCreation(Actor actor)
		{
			var mutant = actor.TraitOrDefault<Mutant>();
			var suppressed = ShouldSuppressMutantActor(noMutants, mutant != null,
				mutant?.SpawnedByMap ?? false, mutant?.HasEnteredWorld ?? false);
			if (suppressed && info.DebugLogging)
				Log.Write("debug", "Tiberium mutation option: suppressed actor={0}, owner={1}, tick={2}",
					actor.Info.Name, actor.Owner?.InternalName ?? "none", world.WorldTick);

			return suppressed;
		}

		bool IImpactTypeSuppressor.SuppressImpact(string impactType)
		{
			var suppressed = IsImpactSuppressed(impactType, info.RedImpactType, info.BlueImpactType,
				noRedExplosions, noBlueExplosions);
			if (info.DebugLogging)
				Log.Write("debug", "Tiberium explosion option: impact={0}, suppressed={1}, no-red={2}, no-blue={3}, tick={4}",
					impactType, suppressed, noRedExplosions, noBlueExplosions, world.WorldTick);

			return suppressed;
		}

		public static bool IsImpactSuppressed(string impactType, string redImpactType, string blueImpactType,
			bool noRedExplosions, bool noBlueExplosions)
		{
			return (noRedExplosions && string.Equals(impactType, redImpactType, StringComparison.OrdinalIgnoreCase)) ||
				(noBlueExplosions && string.Equals(impactType, blueImpactType, StringComparison.OrdinalIgnoreCase));
		}

		public static bool ShouldSuppressMutantActor(bool noMutants, bool isMutant, bool spawnedByMap,
			bool hasEnteredWorld)
		{
			return noMutants && isMutant && !spawnedByMap && !hasEnteredWorld;
		}
	}
}
