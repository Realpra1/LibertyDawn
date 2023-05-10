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
using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	public struct ResourceLayerContents
	{
		public static readonly ResourceLayerContents Empty = default;
		public readonly string Type;
		public readonly int Density;

		// I don't like this way of adding overlay to res sprites, but don't know how else to do it. Turning resources into actors may have been better ultimately.
		public readonly Color? Overlay;

		public ResourceLayerContents(string type, int density, Color? overlay = null)
		{
			Type = type;
			Density = density;
			Overlay = overlay;
		}
	}

	public class ResourceTickInfo
	{
		public readonly int Token;
		public readonly int ExpectedStageInterval;
		public readonly int ExpectedSpreadInterval;
		public int LastStageTime;
		public int LastSpreadTime;

		public ResourceTickInfo(int expectedStageInterval, int expectedSpreadInterval, int currentTime)
		{
			Token = currentTime;
			ExpectedStageInterval = expectedStageInterval;
			ExpectedSpreadInterval = expectedSpreadInterval;
			LastStageTime = currentTime;
			LastSpreadTime = currentTime;
		}
	}

	[TraitLocation(SystemActors.World)]
	[Desc("Attach this to the world actor.")]
	public class ResourceLayerInfo : TraitInfo, IRulesetLoaded, IResourceLayerInfo, Requires<BuildingInfluenceInfo>
	{
		public class ResourceTypeInfo
		{
			[FieldLoader.Require]
			[Desc("Resource index in the binary map data.")]
			public readonly byte ResourceIndex = 0;

			[FieldLoader.Require]
			[Desc("Terrain type used to determine unit movement and minimap colors.")]
			public readonly string TerrainType = null;

			[FieldLoader.Require]
			[Desc("Terrain types that this resource can spawn on.")]
			public readonly HashSet<string> AllowedTerrainTypes = null;

			[Desc("Maximum number of resource units allowed in a single cell.")]
			public readonly int MaxDensity = 10;

			[Desc("How fast does each stage/density grow, null/0 for never.")]
			public readonly Dictionary<int, int> DensityIntervals = new Dictionary<int, int>();

			[Desc("How much density does each stage have (code assumes stage 1 always 1).")]
			public readonly Dictionary<int, int> StageDensities = new Dictionary<int, int>();

			[Desc("Max stage will change into this resource, null for never. 'Explode' for explosion.")]
			public readonly string MaxStageEvolvesTo = null;

			[Desc("Spread interval (max stages only), 0 for never.")]
			public readonly int SpreadInterval = 0;

			[Desc("Weapon(s) to use if resource explodes.")]
			public readonly HashSet<string> Explosions = null;

			public HashSet<WeaponInfo> ExplosionsInfo = new HashSet<WeaponInfo>();

			[Desc("Chance of exploding when damaged.")]
			public readonly int ExplosionChance = 0;

			[Desc("Blink interval before max stage change.")]
			public readonly int BlinkInterval = 13;

			[Desc("Blink interval before max stage change.")]
			public readonly int BlinkWarningInterval = 0;

			[Desc("Color used for blinking.")]
			public readonly string BlinkColor = null;

			[Desc("Minimum damage.")]
			public readonly int MinimumDamage = 1000;

			[Desc("After exploding what remains, random choice from list, any non resource means removal. Current stage minus 1 max always assumed.")]
			public readonly HashSet<string> ExplodeInto = null;

			public ResourceTypeInfo(MiniYaml yaml)
			{
				FieldLoader.Load(this, yaml);
			}
		}

		[FieldLoader.LoadUsing(nameof(LoadResourceTypes))]
		public readonly Dictionary<string, ResourceTypeInfo> ResourceTypes = null;

		[Desc("Override the density saved in maps with values calculated based on the number of neighbouring resource cells.")]
		public readonly bool RecalculateResourceDensity = false;

		[Desc("Change all spread and not-max stage times (percent).")]
		public readonly int SpreadModifier = 100;

		[Desc("Change max stage time (percent).")]
		public readonly int StageModifier = 100;

		// Copied to EditorResourceLayerInfo, ResourceRendererInfo
		protected static object LoadResourceTypes(MiniYaml yaml)
		{
			var ret = new Dictionary<string, ResourceTypeInfo>();
			var resources = yaml.Nodes.FirstOrDefault(n => n.Key == "ResourceTypes");
			if (resources != null)
				foreach (var r in resources.Value.Nodes)
					ret[r.Key] = new ResourceTypeInfo(r.Value);

			return ret;
		}

		public void RulesetLoaded(Ruleset rules, ActorInfo info)
		{
			foreach (var resourceInfo in ResourceTypes.Values)
			{
				if (resourceInfo.Explosions != null)
				{
					foreach (var explosion in resourceInfo.Explosions)
					{
						var weaponToLower = explosion.ToLowerInvariant();
						if (!rules.Weapons.TryGetValue(weaponToLower, out var weapon))
							throw new YamlException($"Weapons Ruleset does not contain an entry '{weaponToLower}'");
						resourceInfo.ExplosionsInfo.Add(weapon);
					}
				}
			}
		}

		bool IResourceLayerInfo.TryGetTerrainType(string resourceType, out string terrainType)
		{
			if (resourceType == null || !ResourceTypes.TryGetValue(resourceType, out var resourceInfo))
			{
				terrainType = null;
				return false;
			}

			terrainType = resourceInfo.TerrainType;
			return true;
		}

		bool IResourceLayerInfo.TryGetResourceIndex(string resourceType, out byte index)
		{
			if (resourceType == null || !ResourceTypes.TryGetValue(resourceType, out var resourceInfo))
			{
				index = 0;
				return false;
			}

			index = resourceInfo.ResourceIndex;
			return true;
		}

		public override object Create(ActorInitializer init) { return new ResourceLayer(init.Self, this); }
	}

	public class ResourceLayer : IResourceLayer, IWorldLoaded, ITick
	{
		readonly ResourceLayerInfo info;
		readonly World world;
		protected readonly Map Map;
		protected readonly BuildingInfluence BuildingInfluence;
		protected readonly CellLayer<ResourceLayerContents> Content;
		protected readonly Dictionary<byte, string> ResourceTypesByIndex;

		readonly Dictionary<CPos, int> tickTokens = new Dictionary<CPos, int>();
		readonly Dictionary<CPos, bool> delayedExplosions = new Dictionary<CPos, bool>();
		readonly Dictionary<CPos, int> blinks = new Dictionary<CPos, int>();
		readonly Dictionary<CPos, int> stageTimes = new Dictionary<CPos, int>();
		readonly Dictionary<CPos, int> spreadTimes = new Dictionary<CPos, int>();
		readonly Dictionary<int, LinkedList<Action>> scheduledActions = new Dictionary<int, LinkedList<Action>>();

		int resCells;

		public event Action<CPos, string> CellChanged;

		protected void Schedule(int waitForTickTime, Action action)
		{
			if (waitForTickTime <= tickTime)
				throw new Exception("Invalid scheduling.");
			if (!scheduledActions.ContainsKey(waitForTickTime))
				scheduledActions.Add(waitForTickTime, new LinkedList<Action>());
			scheduledActions[waitForTickTime].AddLast(action);
		}

		public ResourceLayer(Actor self, ResourceLayerInfo info)
		{
			this.info = info;
			world = self.World;
			Map = world.Map;
			BuildingInfluence = self.Trait<BuildingInfluence>();
			Content = new CellLayer<ResourceLayerContents>(Map);
			ResourceTypesByIndex = info.ResourceTypes.ToDictionary(
				kv => kv.Value.ResourceIndex,
				kv => kv.Key);
		}

		protected ModifiesResourcesInfo.ModifiesResourcesTypeInfo GetModifiedTypeInfo(CPos cell, ResourceLayerInfo.ResourceTypeInfo typeInfo)
		{
			var content = Content[cell];
			if (content.Type == null)
				return null;

			var modifier = GetModifier(cell);

			if (modifier == null)
				return null;

			if (!modifier.Info.ModifiesResourcesTypes.TryGetValue(content.Type, out var resModInfo))
				return null;

			return resModInfo;
		}

		protected string GetEvolvesTo(CPos cell, ResourceLayerInfo.ResourceTypeInfo typeInfo)
		{
			var modTypeInfo = GetModifiedTypeInfo(cell, typeInfo);
			return modTypeInfo != null ? modTypeInfo.MaxStageEvolvesTo : typeInfo.MaxStageEvolvesTo;
		}

		protected int GetBlinkWarningInterval(CPos cell, ResourceLayerInfo.ResourceTypeInfo typeInfo)
		{
			var modTypeInfo = GetModifiedTypeInfo(cell, typeInfo);
			return modTypeInfo != null ? modTypeInfo.BlinkWarningInterval : typeInfo.BlinkWarningInterval;
		}

		protected int GetSpreadModifier(CPos cell, ResourceLayerInfo.ResourceTypeInfo typeInfo)
		{
			var modifier = GetModifier(cell);

			return modifier != null ? modifier.Info.SpreadModifier : info.SpreadModifier;
		}

		protected int GetStageModifier(CPos cell, ResourceLayerInfo.ResourceTypeInfo typeInfo)
		{
			var modifier = GetModifier(cell);

			return modifier != null ? modifier.Info.StageModifier : info.StageModifier;
		}

		protected int modifierCachedTickTime = 0;
		protected Dictionary<CPos, ModifiesResources> cachedModifiers = new Dictionary<CPos, ModifiesResources>();
		protected ModifiesResources GetModifier(CPos cell)
		{
			if (tickTime <= modifierCachedTickTime + 50)
				return cachedModifiers.ContainsKey(cell) ? cachedModifiers[cell] : null;

			var prevCached = cachedModifiers;
			cachedModifiers = new Dictionary<CPos, ModifiesResources>();
			var modifiers = world.ActorsWithTrait<ModifiesResources>();

			foreach (var modifier in modifiers)
			{
				if (modifier.Trait.IsTraitDisabled)
					continue;

				var moddedCells = world.Map.FindTilesInCircle(modifier.Actor.Location, WDist.ToCells(modifier.Trait.Range));

				foreach (var modCell in moddedCells)
				{
					if (!prevCached.ContainsKey(modCell))
						AddToTickQueue(modCell, Content[cell].Type, true);
					cachedModifiers.TryAdd(modCell, modifier.Trait);
				}
			}

			modifierCachedTickTime = tickTime;

			return cachedModifiers.ContainsKey(cell) ? cachedModifiers[cell] : null;
		}

		protected virtual void WorldLoaded(World w, WorldRenderer wr)
		{
			foreach (var cell in w.Map.AllCells)
			{
				var resource = world.Map.Resources[cell];
				if (!ResourceTypesByIndex.TryGetValue(resource.Type, out var resourceType))
					continue;

				if (!AllowResourceAt(resourceType, cell))
					continue;

				Content[cell] = CreateResourceCell(resourceType, cell, resource.Index);

				if (!info.RecalculateResourceDensity)
					AddToTickQueue(cell, resourceType);
			}

			if (!info.RecalculateResourceDensity)
				return;

			// Set initial density based on the number of neighboring resources
			foreach (var cell in w.Map.AllCells)
			{
				var resource = Content[cell];
				if (resource.Type == null || !info.ResourceTypes.TryGetValue(resource.Type, out var resourceInfo))
					continue;

				var adjacent = 0;
				var directions = CVec.Directions;
				for (var i = 0; i < directions.Length; i++)
				{
					var c = cell + directions[i];
					if (Content.Contains(c) && Content[c].Type == resource.Type)
						++adjacent;
				}

				if (resourceInfo.StageDensities.Count() > 0)
				{
					var stage = Math.Max(int2.Lerp(0, resourceInfo.StageDensities.Count(), adjacent, directions.Length), 1);
					Content[cell] = new ResourceLayerContents(resource.Type, resourceInfo.StageDensities[stage]);
				}
				else
				{
					var density = Math.Max(int2.Lerp(0, resourceInfo.MaxDensity, adjacent, directions.Length), 1);
					Content[cell] = new ResourceLayerContents(resource.Type, density);
				}

				AddToTickQueue(cell, resourceInfo);
			}
		}

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr) { WorldLoaded(w, wr); }

		protected virtual bool AllowResourceAt(string resourceType, CPos cell)
		{
			if (!Map.Contains(cell) || Map.Ramp[cell] != 0)
				return false;

			if (resourceType == null || !info.ResourceTypes.TryGetValue(resourceType, out var resourceInfo))
				return false;

			if (!resourceInfo.AllowedTerrainTypes.Contains(Map.GetTerrainInfo(cell).Type))
				return false;

			return !BuildingInfluence.AnyBuildingAt(cell);
		}

		ResourceLayerContents CreateResourceCell(string resourceType, CPos cell, int density)
		{
			if (!info.ResourceTypes.TryGetValue(resourceType, out var resourceInfo))
			{
				world.Map.CustomTerrain[cell] = byte.MaxValue;
				return ResourceLayerContents.Empty;
			}

			world.Map.CustomTerrain[cell] = world.Map.Rules.TerrainInfo.GetTerrainIndex(resourceInfo.TerrainType);
			++resCells;

			return new ResourceLayerContents(resourceType, density.Clamp(1, resourceInfo.MaxDensity));
		}

		bool CanAddResource(string resourceType, CPos cell, int amount = 1)
		{
			if (!world.Map.Contains(cell))
				return false;

			if (resourceType == null || !info.ResourceTypes.TryGetValue(resourceType, out var resourceInfo))
				return false;

			var content = Content[cell];
			if (content.Type == null)
				return amount <= resourceInfo.MaxDensity && AllowResourceAt(resourceType, cell);

			if (content.Type != resourceType)
				return false;

			return content.Density + amount <= resourceInfo.MaxDensity;
		}

		/// <summary>
		/// Finds closest allowed density using max or stage density config.
		/// </summary>
		static int ClosestDensity(ResourceLayerInfo.ResourceTypeInfo typeInfo, int newDensity, bool roundUp)
		{
			if (typeInfo.StageDensities.Count == 0)
				return Math.Min(newDensity, typeInfo.MaxDensity);

			int down = 0, up = typeInfo.MaxDensity;
			foreach (var density in typeInfo.StageDensities.Values)
			{
				if (newDensity >= density && density > down)
					down = density;
				if (newDensity <= density && density < up)
					up = density;
			}

			return roundUp ? up : down;
		}

		int AddResource(string resourceType, CPos cell, int amount = 1)
		{
			if (!Content.Contains(cell))
				return 0;

			if (resourceType == null || !info.ResourceTypes.TryGetValue(resourceType, out var resourceInfo))
				return 0;

			var content = Content[cell];
			int oldDensity;
			if (content.Type == null)
			{
				content = CreateResourceCell(resourceType, cell, 0);
				oldDensity = 0;
			}
			else
				oldDensity = content.Density;

			if (content.Type != resourceType)
				return 0;

			var density = ClosestDensity(resourceInfo, oldDensity + amount, true);

			Content[cell] = new ResourceLayerContents(content.Type, density);
			CellChanged?.Invoke(cell, content.Type);
			AddToTickQueue(cell, resourceInfo);

			return density - oldDensity;
		}

		/// <summary>
		/// Returns amount removed. May be more or less than amount parameter depending on the resource cell and stage config.
		/// </summary>
		int RemoveResource(string resourceType, CPos cell, int amountAttempt = 1)
		{
			if (!Content.Contains(cell))
				return 0;

			var content = Content[cell];
			if (content.Type == null || content.Type != resourceType)
				return 0;

			int newDensity = content.Density - amountAttempt;
			if (info.ResourceTypes.TryGetValue(resourceType, out var resourceInfo) && resourceInfo.StageDensities.Count != 0)
			{
				newDensity = ClosestDensity(resourceInfo, content.Density - amountAttempt, false);
			}

			var oldDensity = content.Density;
			var density = Math.Max(0, newDensity);

			if (density == 0)
			{
				Content[cell] = ResourceLayerContents.Empty;
				Map.CustomTerrain[cell] = byte.MaxValue;
				--resCells;
				if (!tickTokens.TryAdd(cell, tickTime + 1))
					tickTokens[cell] = tickTime + 1;

				CellChanged?.Invoke(cell, null);
			}
			else
			{
				Content[cell] = new ResourceLayerContents(content.Type, density);
				AddToTickQueue(cell, resourceType);
				CellChanged?.Invoke(cell, content.Type);
			}

			return oldDensity - density;
		}

		readonly int explodeDelayMin = 12;
		readonly int explodeDelayMax = 50;
		void DamageResource(Actor source, CPos cell, int damage)
		{
			try
			{
				var content = Content[cell];
				if (content.Type == null || !info.ResourceTypes.TryGetValue(content.Type, out var resourceInfo))
					return;

				if (damage < resourceInfo.MinimumDamage)
					return;

				if (world.SharedRandom.Next(1, 100) > resourceInfo.ExplosionChance)
					return;

				if (!delayedExplosions.ContainsKey(cell))
				{
					var waitForTime = tickTime + world.SharedRandom.Next(explodeDelayMin, explodeDelayMax);
					delayedExplosions.Add(cell, true);
					Schedule(waitForTime, () => DoExplode(source, resourceInfo, cell));
				}
			}
			catch (Exception ex)
			{
				Log.Write("debug", ex.Message + "\n" + ex.StackTrace);
			}
		}

		void ClearResources(CPos cell)
		{
			if (!Content.Contains(cell))
				return;

			// Don't break other users of CustomTerrain if there are no resources
			var content = Content[cell];
			if (content.Type == null)
				return;

			Content[cell] = ResourceLayerContents.Empty;
			Map.CustomTerrain[cell] = byte.MaxValue;
			--resCells;
			if (!tickTokens.TryAdd(cell, tickTime + 1))
				tickTokens[cell] = tickTime + 1;

			CellChanged?.Invoke(cell, null);
		}

		ResourceLayerContents IResourceLayer.GetResource(CPos cell) { return Content.Contains(cell) ? Content[cell] : default; }
		int IResourceLayer.GetMaxDensity(string resourceType)
		{
			if (!info.ResourceTypes.TryGetValue(resourceType, out var resourceInfo))
				return 0;

			return resourceInfo.MaxDensity;
		}

		bool IResourceLayer.CanAddResource(string resourceType, CPos cell, int amount) { return CanAddResource(resourceType, cell, amount); }
		int IResourceLayer.AddResource(string resourceType, CPos cell, int amount) { return AddResource(resourceType, cell, amount); }
		int IResourceLayer.RemoveResource(string resourceType, CPos cell, int amount) { return RemoveResource(resourceType, cell, amount); }
		void IResourceLayer.DamageResource(Actor source, CPos cell, int damage) { DamageResource(source, cell, damage); }
		void IResourceLayer.ClearResources(CPos cell) { ClearResources(cell); }
		bool IResourceLayer.IsVisible(CPos cell) { return !world.FogObscures(cell); }
		bool IResourceLayer.IsEmpty => resCells < 1;
		IResourceLayerInfo IResourceLayer.Info => info;
		void ITick.Tick(Actor self) { Tick(self); }

		void AddToTickQueue(CPos cell, string resourceType)
		{
			AddToTickQueue(cell, resourceType, false);
		}

		void AddToTickQueue(CPos cell, string resourceType, bool updateOnly)
		{
			if (!info.ResourceTypes.TryGetValue(resourceType, out var resourceInfo))
			{
				Log.Write("debug", "Resource info was not found for type: " + resourceType);
				return;
			}

			AddToTickQueue(cell, resourceInfo);
		}

		void AddToTickQueue(CPos cell, ResourceLayerInfo.ResourceTypeInfo typeInfo)
		{
			AddToTickQueue(cell, typeInfo, false);
		}

		void AddToTickQueue(CPos cell, ResourceLayerInfo.ResourceTypeInfo typeInfo, bool updateOnly)
		{
			try
			{
				var useTickTime = updateOnly && tickTokens.ContainsKey(cell) ? tickTokens[cell] : tickTime;

				if (!tickTokens.TryAdd(cell, useTickTime))
					tickTokens[cell] = useTickTime;
				spreadTimes.Remove(cell);
				stageTimes.Remove(cell);

				if (typeInfo == null)
					return;

				var spreadModifier = GetSpreadModifier(cell, typeInfo);
				var content = Content[cell];
				var stageModifier = content.Density == typeInfo.MaxDensity ? GetStageModifier(cell, typeInfo) : GetSpreadModifier(cell, typeInfo);
				var cellTickInfo = new ResourceTickInfo(typeInfo.DensityIntervals.ContainsKey(content.Density) ? typeInfo.DensityIntervals[content.Density] * 100 / stageModifier : 0, typeInfo.SpreadInterval * 100 / spreadModifier, useTickTime);

				if (typeInfo.SpreadInterval != 0 && content.Density == typeInfo.MaxDensity)
				{
					ScheduleSpreadTime(Math.Max(useTickTime + cellTickInfo.ExpectedSpreadInterval, tickTime + 1),
						() => DoResourceTickActions(cell, cellTickInfo),
						cell);
				}

				if (typeInfo.DensityIntervals.Count != 0)
				{
					if (!typeInfo.DensityIntervals.ContainsKey(content.Density))
					{
						Log.Write("debug", "Invalid yaml configuration of StageIntervals, missing for " + content.Type + " density: " + content.Density);
						return;
					}

					if (typeInfo.DensityIntervals[content.Density] == 0)
						return;

					ScheduleStageTime(Math.Max(useTickTime + cellTickInfo.ExpectedStageInterval, tickTime + 1),
						() => DoResourceTickActions(cell, cellTickInfo),
						cell);

					CheckForAndScheduleBlink(cell, typeInfo, Math.Max(useTickTime + cellTickInfo.ExpectedStageInterval, tickTime + 1));
				}
			}
			catch (Exception ex)
			{
				Log.Write("debug", ex.Message + "\n" + ex.StackTrace);
			}
		}

		protected void CheckForAndScheduleBlink(CPos cell, ResourceLayerInfo.ResourceTypeInfo typeInfo, int expectedStagingTime)
		{
			var blinkWarnInterval = GetBlinkWarningInterval(cell, typeInfo);
			var waitForTime = Math.Max(tickTime + 1, expectedStagingTime - blinkWarnInterval);

			if (blinks.ContainsKey(cell) && blinks[cell] <= waitForTime)
				return;
			if (blinks.ContainsKey(cell))
				blinks.Remove(cell);

			if (Content[cell].Density == typeInfo.MaxDensity
				&& blinkWarnInterval != 0
				&& !string.IsNullOrEmpty(typeInfo.BlinkColor)
				&& typeInfo.BlinkInterval != 0)
			{
				var color = Color.TryParse(typeInfo.BlinkColor, out var tColor) ? tColor : Color.Gray;
				blinks.TryAdd(cell, waitForTime);
				Schedule(waitForTime,
					() => DoBlink(cell, color, typeInfo.BlinkInterval, typeInfo));
			}
		}

		void DoResourceTickActions(CPos cell, ResourceTickInfo tickInfo)
		{
			if (Content[cell].Type == null || !info.ResourceTypes.TryGetValue(Content[cell].Type, out var resourceInfo))
				return;

			if (ResourceTypesByIndex[resourceInfo.ResourceIndex] != Content[cell].Type
				|| !tickTokens.ContainsKey(cell) || tickTokens[cell] != tickInfo.Token)
				return;

			if (resourceInfo.SpreadInterval != 0 && Content[cell].Density == resourceInfo.MaxDensity && tickTime - tickInfo.LastSpreadTime >= tickInfo.ExpectedSpreadInterval)
			{
				if (spreadTimes.ContainsKey(cell) && tickTime == spreadTimes[cell])
					spreadTimes.Remove(cell);
				tickInfo.LastSpreadTime = tickTime;
				var seedLoc = Util.CircularRandomNeighbors(cell, world.SharedRandom, (cell.X + cell.Y + cell.X * cell.Y) % 100 > 35)
					.Take(3)
					.SkipWhile(p => Content[p].Type == Content[cell].Type
						&& !CanAddResource(Content[cell].Type, p, resourceInfo.MaxDensity))
					.Cast<CPos?>().FirstOrDefault();

				if (seedLoc != null && CanAddResource(Content[cell].Type, seedLoc.Value))
					AddResource(Content[cell].Type, seedLoc.Value);
			}

			if (resourceInfo.SpreadInterval != 0 && Content[cell].Density == resourceInfo.MaxDensity)
				ScheduleSpreadTime(tickTime + tickInfo.ExpectedSpreadInterval,
					() => DoResourceTickActions(cell, tickInfo),
					cell);

			// Stage/stage to new resource
			if (resourceInfo.DensityIntervals.Count != 0 && tickTime - tickInfo.LastStageTime >= tickInfo.ExpectedStageInterval)
			{
				if (stageTimes.ContainsKey(cell) && tickTime == stageTimes[cell])
					stageTimes.Remove(cell);
				tickInfo.LastStageTime = tickTime;
				if (Content[cell].Density == resourceInfo.MaxDensity)
				{
					var maxStageEvolvesTo = GetEvolvesTo(cell, resourceInfo);
					var blinkWarnInterval = GetBlinkWarningInterval(cell, resourceInfo);

					if (!blinks.ContainsKey(cell) && blinkWarnInterval != 0 && !string.IsNullOrEmpty(maxStageEvolvesTo))
					{
						// Restart countdown if about to explode/change with no configured blinking first.
						CheckForAndScheduleBlink(cell, resourceInfo, tickTime + tickInfo.ExpectedStageInterval);
						if (blinks.ContainsKey(cell))
						{
							ScheduleStageTime(tickTime + tickInfo.ExpectedStageInterval,
								() => DoResourceTickActions(cell, tickInfo),
								cell);
							return;
						}
					}

					if (string.IsNullOrEmpty(maxStageEvolvesTo))
						return;

					if (maxStageEvolvesTo.ToLower() == "explode" && !delayedExplosions.ContainsKey(cell))
					{
						var resActor = world.CreateActor("vice", new TypeDictionary
						{
							new OwnerInit(world.Players.Where(p => p.PlayerName == "Creeps").FirstOrDefault())
						});
						DoExplode(resActor, resourceInfo, cell);
						resActor.Dispose();
						return;
					}

					info.ResourceTypes.TryGetValue(maxStageEvolvesTo, out var newResourceInfo);
					if (newResourceInfo == null)
						return;

					Content[cell] = new ResourceLayerContents(maxStageEvolvesTo, newResourceInfo.MaxDensity);
					CellChanged?.Invoke(cell, Content[cell].Type);
					AddToTickQueue(cell, newResourceInfo);
				}
				else
					AddResource(Content[cell].Type, cell, 1);

				CheckForAndScheduleBlink(cell, resourceInfo, tickTime + tickInfo.ExpectedStageInterval);

				return;
			}
		}

		protected void ScheduleStageTime(int expectedTime, Action action, CPos cell)
		{
			if (!stageTimes.ContainsKey(cell) || expectedTime < stageTimes[cell])
				Schedule(expectedTime, action);

			if (!stageTimes.ContainsKey(cell))
				stageTimes.Add(cell, expectedTime);
		}

		protected void ScheduleSpreadTime(int expectedTime, Action action, CPos cell)
		{
			if (!spreadTimes.ContainsKey(cell) || expectedTime < spreadTimes[cell])
				Schedule(expectedTime, action);

			if (!spreadTimes.ContainsKey(cell))
				spreadTimes.Add(cell, expectedTime);
		}

		int tickTime = -1;

		/// <summary>
		/// See Shroud.cs for similar use of Tick. 25 ticks in one second.
		/// Custom queue stuff is for performance as there can be a lot of resources on a map, all essentially having timed events.
		/// This way avoids for example checking all resource cells every tick or some such folly and spreads out the work over time.
		/// </summary>
		void Tick(Actor self)
		{
			try
			{
				if (resCells == 0)
					return;

				tickTime++;

				if (scheduledActions.ContainsKey(tickTime))
				{
					foreach (var action in scheduledActions[tickTime])
					{
						try
						{
							action();
						}
						catch (Exception ex)
						{
							Log.Write("debug", ex.Message + "\n" + ex.StackTrace);
						}
					}

					scheduledActions[tickTime].Clear();
					scheduledActions.Remove(tickTime);
				}
			}
			catch (Exception ex)
			{
				Log.Write("debug", ex.Message + "\n" + ex.StackTrace);
			}
		}

		void DoBlink(CPos cell, Color color, int interval, ResourceLayerInfo.ResourceTypeInfo typeInfo)
		{
			var content = Content[cell];
			var blinkWarnInterval = GetBlinkWarningInterval(cell, typeInfo);

			// 1. Check if cell density or cell type changed/empty.
			if (!blinks.ContainsKey(cell) || blinks[cell] != tickTime || content.Density != typeInfo.MaxDensity || content.Type != typeInfo.TerrainType || blinkWarnInterval == 0)
			{
				Content[cell] = new ResourceLayerContents(content.Type, content.Density, null); // Remove any overlay.
				blinks.Remove(cell);
				CellChanged?.Invoke(cell, content.Type);
				return;
			}

			// 2. Either blink or remove overlay and re-queue.
			if (content.Overlay == null)
			{
				Content[cell] = new ResourceLayerContents(content.Type, content.Density, color);
				blinks[cell] = tickTime + 4;
				Schedule(tickTime + 4,
							() => DoBlink(cell, color, interval, typeInfo));
			}
			else
			{
				Content[cell] = new ResourceLayerContents(content.Type, content.Density, null);
				blinks[cell] = tickTime + interval;
				Schedule(tickTime + interval,
							() => DoBlink(cell, color, interval, typeInfo));
			}

			CellChanged?.Invoke(cell, content.Type);
		}

		void DoExplode(Actor source, ResourceLayerInfo.ResourceTypeInfo resourceInfo, CPos cell)
		{
			try
			{
				foreach (var weapon in resourceInfo.ExplosionsInfo)
				{
					if (weapon.Projectile != null)
					{
						var burst = world.SharedRandom.Next(1, 8);
						for (var i = 0; i <= burst; i++)
						{
							ResourceProjectile(source, weapon, cell);
						}
					}
					else
					{
						ResourceExplosion(source, weapon, cell);
					}
				}

				delayedExplosions.Remove(cell);

				var content = Content[cell];
				var newDensity = world.SharedRandom.Next(0, Math.Max(0, content.Density - 1));
				if (newDensity == 0 || resourceInfo.ExplodeInto.Count == 0)
					RemoveResource(content.Type, cell, content.Density);
				else
				{
					var newType = resourceInfo.ExplodeInto.ElementAt(world.SharedRandom.Next(0, resourceInfo.ExplodeInto.Count - 1));
					info.ResourceTypes.TryGetValue(newType, out var newResourceInfo);

					if (newType == content.Type)
						RemoveResource(content.Type, cell, content.Density - newDensity);
					else if (newResourceInfo == null)
						RemoveResource(content.Type, cell, content.Density);
					else
					{
						Content[cell] = new ResourceLayerContents(newType,
							ClosestDensity(newResourceInfo, int2.Lerp(1, newResourceInfo.MaxDensity, newDensity, resourceInfo.MaxDensity), false));
						CellChanged?.Invoke(cell, Content[cell].Type);
						AddToTickQueue(cell, newResourceInfo);
					}
				}
			}
			catch (Exception ex)
			{
				Log.Write("debug", ex.Message + "\n" + ex.StackTrace);
			}
		}

		void ResourceProjectile(Actor source, WeaponInfo weapon, CPos cell)
		{
			Func<WPos> muzzlePosition = () => Map.CenterOfCell(cell);

			var angle = world.SharedRandom.Next(0, 180);
			var length = world.SharedRandom.Next(-weapon.Range.Length, weapon.Range.Length);
			var targetPos = new WPos((int)(muzzlePosition().X + Math.Cos(angle * Math.PI / 180) * length),
				(int)(muzzlePosition().Y + Math.Sin(angle * Math.PI / 180) * length),
				muzzlePosition().Z);

			// 1024 for 360 degrees, so 128 is 45 degrees:
			Func<WAngle> muzzleFacing = () => new WAngle(int2.Lerp(20, 128, length, weapon.Range.Length));
			var muzzleOrientation = WRot.FromYaw(muzzleFacing());

			var args = new ProjectileArgs
			{
				Weapon = weapon,
				Facing = muzzleFacing(),
				CurrentMuzzleFacing = muzzleFacing,
				DamageModifiers = Array.Empty<int>(),
				InaccuracyModifiers = Array.Empty<int>(),
				RangeModifiers = Array.Empty<int>(),
				Source = muzzlePosition(),
				CurrentSource = muzzlePosition,
				SourceActor = source,
				PassiveTarget = targetPos,
				GuidedTarget = Target.FromPos(targetPos)
			};

			if (args.Weapon.Projectile != null)
			{
				var projectile = args.Weapon.Projectile.Create(args);
				if (projectile != null)
					world.Add(projectile);

				if (args.Weapon.Report != null && args.Weapon.Report.Any())
					Game.Sound.Play(SoundType.World, args.Weapon.Report, world, muzzlePosition());
			}
		}

		void ResourceExplosion(Actor damageSource, WeaponInfo weapon, CPos cell)
		{
			if (weapon == null)
				return;

			var source = damageSource;
			if (weapon.Report != null && weapon.Report.Any())
				Game.Sound.Play(SoundType.World, weapon.Report, damageSource.World, Map.CenterOfCell(cell));

			weapon.Impact(Target.FromPos(Map.CenterOfCell(cell)), source);
		}
	}
}
