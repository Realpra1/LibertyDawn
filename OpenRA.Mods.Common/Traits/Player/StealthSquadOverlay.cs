#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Stores replay-safe stealth squad labels for the spectator overlay.")]
	public sealed class StealthSquadOverlayInfo : TraitInfo
	{
		public override object Create(ActorInitializer init) { return new StealthSquadOverlay(init.Self); }
	}

	public sealed class StealthSquadOverlay : IResolveOrder, IRenderAnnotations
	{
		internal const string OrderName = "RecordStealthSquadOverlay";

		sealed class Snapshot
		{
			public readonly string Profile;
			public readonly int Index;
			public readonly string Phase;
			public readonly uint[] ActorIds;
			public readonly int StrategicCellSize;
			public readonly CPos[] ConsideredTargets;
			public readonly CPos? ChosenTarget;

			public Snapshot(string profile, int index, string phase, uint[] actorIds,
				int strategicCellSize, CPos[] consideredTargets, CPos? chosenTarget)
			{
				Profile = profile;
				Index = index;
				Phase = phase;
				ActorIds = actorIds;
				StrategicCellSize = strategicCellSize;
				ConsideredTargets = consideredTargets;
				ChosenTarget = chosenTarget;
			}
		}

		readonly World world;
		readonly Dictionary<(string Profile, int Index), Snapshot> snapshots =
			new Dictionary<(string Profile, int Index), Snapshot>();
		public bool Enabled { get; set; }

		public StealthSquadOverlay(Actor self)
		{
			world = self.World;
		}

		internal static string Encode(string profile, int index, string phase,
			IEnumerable<uint> actorIds, int strategicCellSize,
			IEnumerable<CPos> consideredTargets, CPos? chosenTarget)
		{
			if (string.IsNullOrWhiteSpace(profile) || profile.Contains('|') || index < 0 ||
				(phase != null && phase.Contains('|')) || actorIds == null || strategicCellSize < 1 ||
				consideredTargets == null)
				throw new ArgumentException("Invalid stealth squad overlay snapshot.");
			var considered = consideredTargets.Distinct().OrderBy(cell => cell.Y).ThenBy(cell => cell.X).ToArray();
			if (considered.Length > StealthTargetAcquisitionBehavior.MaximumOptions ||
				considered.Any(cell => cell.X < 0 || cell.Y < 0) ||
				(chosenTarget.HasValue && (chosenTarget.Value.X < 0 || chosenTarget.Value.Y < 0)))
				throw new ArgumentException("Invalid stealth squad overlay targets.");
			return string.Join("|", profile, index.ToString(CultureInfo.InvariantCulture),
				phase ?? string.Empty, string.Join(",", actorIds.Distinct().OrderBy(id => id)),
				strategicCellSize.ToString(CultureInfo.InvariantCulture), EncodeCells(considered),
				chosenTarget.HasValue ? EncodeCell(chosenTarget.Value) : string.Empty);
		}

		internal static bool TryDecode(string payload, out string profile, out int index,
			out string phase, out uint[] actorIds, out int strategicCellSize,
			out CPos[] consideredTargets, out CPos? chosenTarget)
		{
			profile = null;
			index = -1;
			phase = null;
			actorIds = Array.Empty<uint>();
			strategicCellSize = 1;
			consideredTargets = Array.Empty<CPos>();
			chosenTarget = null;
			var fields = payload?.Split('|');
			if (fields == null || (fields.Length != 4 && fields.Length != 7) ||
				string.IsNullOrWhiteSpace(fields[0]) ||
				!int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out index) || index < 0)
				return false;
			if (fields[3].Length != 0)
			{
				var ids = fields[3].Split(',');
				actorIds = new uint[ids.Length];
				for (var i = 0; i < ids.Length; i++)
					if (!uint.TryParse(ids[i], NumberStyles.None, CultureInfo.InvariantCulture,
						out actorIds[i]) || actorIds[i] == 0)
						return false;
				if (actorIds.Distinct().Count() != actorIds.Length)
					return false;
			}

			if (fields.Length == 7)
			{
				if (!int.TryParse(fields[4], NumberStyles.None, CultureInfo.InvariantCulture,
					out strategicCellSize) || strategicCellSize < 1 ||
					!TryDecodeCells(fields[5], out consideredTargets) ||
					consideredTargets.Length > StealthTargetAcquisitionBehavior.MaximumOptions)
					return false;
				if (fields[6].Length != 0)
				{
					if (!TryDecodeCell(fields[6], out var chosen))
						return false;
					chosenTarget = chosen;
				}
			}

			profile = fields[0];
			phase = fields[2].Length == 0 ? null : fields[2];
			return true;
		}

		static string EncodeCells(IEnumerable<CPos> cells)
		{
			return string.Join(";", cells.Select(EncodeCell));
		}

		static string EncodeCell(CPos cell)
		{
			return cell.X.ToString(CultureInfo.InvariantCulture) + "," +
				cell.Y.ToString(CultureInfo.InvariantCulture);
		}

		static bool TryDecodeCells(string text, out CPos[] cells)
		{
			if (text.Length == 0)
			{
				cells = Array.Empty<CPos>();
				return true;
			}

			var parsed = new List<CPos>();
			foreach (var field in text.Split(';'))
			{
				if (!TryDecodeCell(field, out var cell) || parsed.Contains(cell))
				{
					cells = Array.Empty<CPos>();
					return false;
				}

				parsed.Add(cell);
			}

			cells = parsed.ToArray();
			return true;
		}

		static bool TryDecodeCell(string text, out CPos cell)
		{
			cell = default(CPos);
			var coordinates = text.Split(',');
			if (coordinates.Length != 2 ||
				!int.TryParse(coordinates[0], NumberStyles.None, CultureInfo.InvariantCulture, out var x) ||
				!int.TryParse(coordinates[1], NumberStyles.None, CultureInfo.InvariantCulture, out var y) ||
				x < 0 || y < 0)
				return false;
			cell = new CPos(x, y);
			return true;
		}

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString != OrderName ||
				!TryDecode(order.TargetString, out var profile, out var index,
					out var phase, out var actorIds, out var cellSize,
					out var consideredTargets, out var chosenTarget))
				return;

			var key = (profile, index);
			if (phase == null)
				snapshots.Remove(key);
			else
				snapshots[key] = new Snapshot(profile, index, phase, actorIds,
					cellSize, consideredTargets, chosenTarget);
		}

		IEnumerable<IRenderable> IRenderAnnotations.RenderAnnotations(Actor self, WorldRenderer wr)
		{
			if (!Enabled || !(world.IsReplay || world.LocalPlayer == null ||
				world.LocalPlayer.WinState != WinState.Undefined))
				yield break;

			var font = Game.Renderer.Fonts["Bold"];
			var smallFont = Game.Renderer.Fonts["Tiny"];
			foreach (var snapshot in snapshots.Values.OrderBy(item => item.Profile, StringComparer.Ordinal)
				.ThenBy(item => item.Index))
			{
				var members = snapshot.ActorIds.Select(world.GetActorById).Where(actor => actor != null &&
					actor.IsInWorld && !actor.IsDead && actor.Owner == self.Owner).ToArray();
				if (members.Length == 0)
					continue;
				var center = members.Select(actor => actor.CenterPosition).Average();
				var profile = snapshot.Profile == "stealth-tank" ? "stank" : snapshot.Profile;
				var squadName = $"{profile} sq {snapshot.Index + 1}";
				var label = $"{self.Owner.PlayerName} {squadName} · {PhaseLabel(snapshot.Phase)}";
				yield return new CircleAnnotationRenderable(center, WDist.FromCells(1), 2, self.Owner.Color);
				yield return new TextAnnotationRenderable(font, center + new WVec(0, 0, 1024),
					0, self.Owner.Color, label);

				var blue = Color.FromArgb(51, 64, 160, 255);
				var purple = Color.FromArgb(51, 192, 64, 255);
				foreach (var target in snapshot.ConsideredTargets.Where(cell => cell != snapshot.ChosenTarget))
				{
					var endpoint = StrategicCellCenter(target, snapshot.StrategicCellSize);
					yield return new LineAnnotationRenderable(center, endpoint, 2, blue);
					yield return new TextAnnotationRenderable(smallFont, endpoint + new WVec(0, 0, 512),
						0, Color.FromArgb(200, 64, 160, 255), squadName);
				}

				if (snapshot.ChosenTarget.HasValue)
				{
					var endpoint = StrategicCellCenter(snapshot.ChosenTarget.Value, snapshot.StrategicCellSize);
					yield return new LineAnnotationRenderable(center, endpoint, 3, purple);
					yield return new TextAnnotationRenderable(smallFont, endpoint + new WVec(0, 0, 512),
						0, Color.FromArgb(220, 192, 64, 255), squadName);
				}
			}
		}

		WPos StrategicCellCenter(CPos strategicCell, int size)
		{
			var mapCell = new CPos(strategicCell.X * size + size / 2,
				strategicCell.Y * size + size / 2);
			return world.Map.CenterOfCell(world.Map.Clamp(mapCell));
		}

		static string PhaseLabel(string phase)
		{
			switch (phase)
			{
				case "SquadConstruction": return "forming";
				case "TargetAcquisition":
				case "TargetValueFilter":
				case "TargetThreatFilter":
				case "TargetDistanceChoice": return "targeting";
				case "Approach": return "routing";
				case "UndefendedAttack": return "attacking";
				case "CrushEvaluation": return "crushing";
				case "Kite": return "kiting";
				case "MassAttack": return "mass attack";
				case "RecalculateFlee": return "fleeing";
				case "Repair": return "repairing";
				default: return phase.ToLowerInvariant();
			}
		}

		bool IRenderAnnotations.SpatiallyPartitionable => false;
	}
}
