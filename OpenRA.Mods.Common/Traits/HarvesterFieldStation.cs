#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License version 3 or later.
 */
#endregion

using System.Collections.Generic;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Tracks the actual resource cell from the latest successfully unloaded harvest cycle.")]
	public class HarvesterFieldStationInfo : TraitInfo, Requires<HarvesterInfo>
	{
		public readonly bool DebugLogging = false;

		public override object Create(ActorInitializer init) { return new HarvesterFieldStation(init.Self); }
	}

	public sealed class HarvesterFieldStation : INotifyHarvesterAction, INotifyHarvesterUnload,
		IHarvesterFieldStation, IGameSaveTraitData
	{
		readonly Actor self;
		readonly HarvesterFieldStationInfo info;
		readonly Harvester harvester;
		HarvesterFieldContextState state;

		public bool HasPendingField => state.HasPending;
		public CPos PendingField => state.Pending;
		public bool HasCommittedField => state.HasCommitted;
		public CPos CommittedField => state.Committed;

		public HarvesterFieldStation(Actor self)
		{
			this.self = self;
			info = self.Info.TraitInfo<HarvesterFieldStationInfo>();
			harvester = self.Trait<Harvester>();
		}

		void INotifyHarvesterAction.Harvested(Actor self, string resourceType)
		{
			var changed = !state.HasPending || state.Pending != self.Location;
			state = EconomyFieldDefensePolicy.Harvested(state, self.Location);
			if (changed)
				Debug("pending harvested cell={0} resource={1} committed={2}", state.Pending,
					resourceType, state.HasCommitted ? state.Committed.ToString() : "none");
		}

		void INotifyHarvesterUnload.UnloadCompleted(Actor self, Actor refinery)
		{
			var oldStation = state.HasCommitted ? state.Committed.ToString() : "none";
			state = EconomyFieldDefensePolicy.UnloadCompleted(state, harvester.IsEmpty);
			Debug("unload completed refinery={0} empty={1} old={2} new={3}", refinery.ActorID,
				harvester.IsEmpty, oldStation, state.HasCommitted ? state.Committed.ToString() : "none");
		}

		void INotifyHarvesterUnload.UnloadAborted(Actor self, Actor refinery)
		{
			state = EconomyFieldDefensePolicy.UnloadAborted(state);
			Debug("unload aborted refinery={0} fullness={1} pending={2} committed={3}", refinery.ActorID,
				harvester.Fullness, state.HasPending ? state.Pending.ToString() : "none",
				state.HasCommitted ? state.Committed.ToString() : "none");
		}

		void INotifyHarvesterUnload.UnloadStarted(Actor self, Actor refinery)
		{
			Debug("unload started refinery={0} fullness={1} pending={2} committed={3}", refinery.ActorID,
				harvester.Fullness, state.HasPending ? state.Pending.ToString() : "none",
				state.HasCommitted ? state.Committed.ToString() : "none");
		}

		void INotifyHarvesterAction.MovingToResources(Actor self, CPos targetCell) { }
		void INotifyHarvesterAction.MovingToRefinery(Actor self, Actor refineryActor) { }
		void INotifyHarvesterAction.MovementCancelled(Actor self) { }
		void INotifyHarvesterAction.Docked() { }
		void INotifyHarvesterAction.Undocked() { }

		void Debug(string format, params object[] args)
		{
			if (!info.DebugLogging)
				return;

			Log.Write("debug", "Harvester field station: {0}#{1} ({2}) at tick {3}: {4}",
				self.Info.Name, self.ActorID, self.Owner, self.World.WorldTick, string.Format(format, args));
		}

		List<MiniYamlNode> IGameSaveTraitData.IssueTraitData(Actor self)
		{
			return new List<MiniYamlNode>
			{
				new MiniYamlNode("HarvesterFieldHasPending", FieldSaver.FormatValue(state.HasPending)),
				new MiniYamlNode("HarvesterFieldPending", FieldSaver.FormatValue(state.Pending)),
				new MiniYamlNode("HarvesterFieldHasCommitted", FieldSaver.FormatValue(state.HasCommitted)),
				new MiniYamlNode("HarvesterFieldCommitted", FieldSaver.FormatValue(state.Committed))
			};
		}

		void IGameSaveTraitData.ResolveTraitData(Actor self, List<MiniYamlNode> data)
		{
			if (self.World.IsReplay)
				return;

			var hasPending = state.HasPending;
			var pending = state.Pending;
			var hasCommitted = state.HasCommitted;
			var committed = state.Committed;
			foreach (var node in data)
				switch (node.Key)
				{
					case "HarvesterFieldHasPending": hasPending = FieldLoader.GetValue<bool>(node.Key, node.Value.Value); break;
					case "HarvesterFieldPending": pending = FieldLoader.GetValue<CPos>(node.Key, node.Value.Value); break;
					case "HarvesterFieldHasCommitted": hasCommitted = FieldLoader.GetValue<bool>(node.Key, node.Value.Value); break;
					case "HarvesterFieldCommitted": committed = FieldLoader.GetValue<CPos>(node.Key, node.Value.Value); break;
				}

			state = new HarvesterFieldContextState(hasPending, pending, hasCommitted, committed);
		}
	}
}
