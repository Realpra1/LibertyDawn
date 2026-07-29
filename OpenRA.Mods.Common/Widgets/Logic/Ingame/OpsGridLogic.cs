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

using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Lint;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	enum OpsGridTab { ConstructionYard, Defense, WarFactory, Barracks, Helipad }

	// Hidden (hotkey-only, no menu entry) per-instance production manager. Lets a player see and
	// act on every Construction Yard / Defense structure / War Factory / Barracks / Helipad they
	// own at once, instead of the stock production palette's one-queue-at-a-time view. No Sell -
	// deliberately excluded.
	[ChromeLogicArgsHotkeys("ToggleOpsGridKey")]
	public class OpsGridLogic : ChromeLogic
	{
		const int BigQueueAmount = 100;
		const int MediumQueueAmount = 5;
		const int ColumnWidth = 84;
		const int ColumnGap = 4;

		// Semantic state colors, independent of the chrome skin's own accent - matches the
		// idle/building/paused language from the approved mockup.
		static readonly Color IdleColor = Color.FromArgb(87, 207, 152);
		static readonly Color BuildingColor = Color.FromArgb(73, 172, 214);
		static readonly Color PausedColor = Color.FromArgb(230, 163, 57);

		static readonly Dictionary<OpsGridTab, string[]> TabGroups = new Dictionary<OpsGridTab, string[]>
		{
			{ OpsGridTab.ConstructionYard, new[] { "Building" } },
			{ OpsGridTab.Defense, new[] { "Defence" } },
			{ OpsGridTab.WarFactory, new[] { "Vehicle" } },
			{ OpsGridTab.Barracks, new[] { "Infantry" } },
			{ OpsGridTab.Helipad, new[] { "Aircraft" } },
		};

		static readonly (OpsGridTab Tab, string Id)[] TabButtonIds =
		{
			(OpsGridTab.ConstructionYard, "TAB_CONYARD"),
			(OpsGridTab.Defense, "TAB_DEFENSE"),
			(OpsGridTab.WarFactory, "TAB_WARFACTORY"),
			(OpsGridTab.Barracks, "TAB_BARRACKS"),
			(OpsGridTab.Helipad, "TAB_HELIPAD"),
		};

		readonly World world;
		readonly Widget panel;
		readonly ContainerWidget columnHeaderRow;
		readonly ContainerWidget bulkRow;
		readonly ScrollPanelWidget gridBody;
		readonly ScrollItemWidget rowTemplate;
		readonly LabelWidget columnHeaderTemplate;
		readonly ContainerWidget bulkCellTemplate;
		readonly ButtonWidget cellTemplate;

		OpsGridTab activeTab = OpsGridTab.ConstructionYard;
		List<ActorInfo> activeColumns = new List<ActorInfo>();

		[ObjectCreator.UseCtor]
		public OpsGridLogic(Widget widget, World world, ModData modData, Dictionary<string, MiniYaml> logicArgs)
		{
			this.world = world;

			panel = widget.Get("OPS_GRID_PANEL");

			var toggleKey = new HotkeyReference();
			if (logicArgs.TryGetValue("ToggleOpsGridKey", out var yaml))
				toggleKey = modData.Hotkeys[yaml.Value];

			// The key listener is a sibling of the panel it toggles (both children of the always-
			// visible OPS_GRID_HOST), not nested inside it - HandleKeyPressOuter bails out on an
			// invisible parent, so a listener living inside the hidden panel would never fire.
			var keyhandler = widget.Get<LogicKeyListenerWidget>("OPS_GRID_KEYHANDLER");
			keyhandler.AddHandler(e =>
			{
				if (e.Event != KeyInputEvent.Down || !toggleKey.IsActivatedBy(e))
					return false;

				if (world.LocalPlayer == null || world.IsReplay)
					return false;

				panel.Visible ^= true;
				if (panel.Visible)
					RebuildColumns();

				return true;
			});

			var templates = widget.Get("OPS_GRID_TEMPLATES");
			columnHeaderTemplate = templates.Get<LabelWidget>("OPS_GRID_COLUMN_HEADER_TEMPLATE");
			bulkCellTemplate = templates.Get<ContainerWidget>("OPS_GRID_BULK_CELL_TEMPLATE");
			cellTemplate = templates.Get<ButtonWidget>("OPS_GRID_CELL_TEMPLATE");

			columnHeaderRow = panel.Get<ContainerWidget>("COLUMN_HEADER_ROW");
			bulkRow = panel.Get<ContainerWidget>("BULK_ROW");
			gridBody = panel.Get<ScrollPanelWidget>("GRID_BODY");
			rowTemplate = gridBody.Get<ScrollItemWidget>("ROW_TEMPLATE");

			foreach (var (tab, id) in TabButtonIds)
			{
				var button = panel.Get<ButtonWidget>(id);
				button.IsHighlighted = () => activeTab == tab;
				button.OnClick = () =>
				{
					activeTab = tab;
					RebuildColumns();
				};
			}
		}

		public override void Tick()
		{
			if (panel.Visible)
				RebuildRows();
		}

		// One row per (Actor, matching queues) - a Construction Yard contributes two queues
		// (Building + Defence) that both land on the same row.
		IEnumerable<(Actor Actor, ProductionQueue[] Queues)> MatchingRows()
		{
			var groups = TabGroups[activeTab];
			var player = world.LocalPlayer;

			return world.ActorsWithTrait<ProductionQueue>()
				.Where(p => p.Actor.Owner == player && p.Actor.IsInWorld && p.Trait.Enabled && groups.Contains(p.Trait.Info.Group))
				.GroupBy(p => p.Actor)
				.Select(g => (g.Key, g.Select(p => p.Trait).ToArray()))
				.OrderBy(r => r.Key.ActorID);
		}

		static string DisplayName(ActorInfo actor)
		{
			var tooltip = actor.TraitInfoOrDefault<TooltipInfo>();
			return !string.IsNullOrEmpty(tooltip?.Name) ? tooltip.Name : actor.Name;
		}

		// Columns only need recomputing when the tab changes or the panel is (re)opened - the
		// buildable set for a given tab is effectively static for the match's ruleset.
		void RebuildColumns()
		{
			// Same sort key the stock production palette itself uses
			// (ProductionPaletteWidget.cs: `AllItems().OrderBy(a => a.TraitInfo<BuildableInfo>().BuildPaletteOrder)`)
			// so column order matches what the player already expects from the normal sidebar.
			activeColumns = MatchingRows()
				.SelectMany(r => r.Queues.SelectMany(q => q.AllItems()))
				.GroupBy(a => a.Name)
				.Select(g => g.First())
				.OrderBy(a => a.TraitInfo<BuildableInfo>().BuildPaletteOrder)
				.ToList();

			columnHeaderRow.Children.Clear();
			bulkRow.Children.Clear();

			for (var i = 0; i < activeColumns.Count; i++)
			{
				var actor = activeColumns[i];
				var x = i * (ColumnWidth + ColumnGap);

				var header = (LabelWidget)columnHeaderTemplate.Clone();
				header.Visible = true;

				// Clone() copies the template's IsVisible closure verbatim (Widget's copy ctor does
				// `IsVisible = widget.IsVisible`), which still reads the TEMPLATE's own Visible field
				// - so setting .Visible on the clone alone has no effect on what IsVisible() reports.
				// Must rebind it to the clone itself.
				header.IsVisible = () => header.Visible;
				header.Bounds = new Rectangle(x, header.Bounds.Y, ColumnWidth, header.Bounds.Height);

				// Full names routinely exceed a single column's width (e.g. "Advanced
				// Communications Center") and LabelWidget never clips its own text, so an
				// un-truncated header bleeds into its neighbours. Truncating - rather than
				// widening columns - keeps every tab's full column set on screen at once; this
				// grid has no horizontal scroll, and 16+ columns already nearly fill the panel
				// width at the current size.
				var name = WidgetUtils.TruncateText(DisplayName(actor), ColumnWidth - 4, Game.Renderer.Fonts[header.Font]);
				header.GetText = () => name;
				columnHeaderRow.AddChild(header);

				var bulkCell = (ContainerWidget)bulkCellTemplate.Clone();
				bulkCell.Visible = true;
				bulkCell.IsVisible = () => bulkCell.Visible;
				bulkCell.Bounds = new Rectangle(x, bulkCell.Bounds.Y, ColumnWidth, bulkCell.Bounds.Height);
				WireBulkCell(bulkCell, actor.Name);
				bulkRow.AddChild(bulkCell);
			}
		}

		static int AmountFor(MouseInput mi)
		{
			return mi.Modifiers.HasModifier(Modifiers.Ctrl) ? BigQueueAmount :
				mi.Modifiers.HasModifier(Modifiers.Shift) ? MediumQueueAmount : 1;
		}

		void WireBulkCell(ContainerWidget bulkCell, string itemName)
		{
			var queueBtn = bulkCell.Get<ButtonWidget>("BULK_QUEUE");
			queueBtn.OnMouseDown = mi =>
			{
				var count = AmountFor(mi);
				foreach (var row in MatchingRows())
					foreach (var queue in row.Queues)
						if (!queue.AllQueued().Any() && queue.BuildableItems().Any(a => a.Name == itemName))
							world.IssueOrder(Order.StartProduction(queue.Actor, itemName, count));
			};

			var pauseBtn = bulkCell.Get<ButtonWidget>("BULK_PAUSE");
			pauseBtn.OnClick = () =>
			{
				foreach (var row in MatchingRows())
					foreach (var queue in row.Queues)
					{
						var current = queue.CurrentItem();
						if (current != null && current.Item == itemName)
							world.IssueOrder(Order.PauseProduction(queue.Actor, itemName, true));
					}
			};

			var clearBtn = bulkCell.Get<ButtonWidget>("BULK_CLEAR");
			clearBtn.OnClick = () =>
			{
				foreach (var row in MatchingRows())
					foreach (var queue in row.Queues)
					{
						var count = queue.AllQueued().Count(i => i.Item == itemName);
						if (count > 0)
							world.IssueOrder(Order.CancelProduction(queue.Actor, itemName, count));
					}
			};
		}

		void RebuildRows()
		{
			gridBody.Children.Clear();

			foreach (var row in MatchingRows())
			{
				var clone = ScrollItemWidget.Setup(rowTemplate, () => false, () => { });

				var actor = row.Actor;
				var queues = row.Queues;
				var current = queues.Select(q => q.CurrentItem()).FirstOrDefault(i => i != null);
				var isIdle = current == null;

				var rowName = DisplayName(actor.Info) + " #" + actor.ActorID;
				clone.Get<LabelWidget>("ROW_NAME").GetText = () => rowName;

				var statusLabel = clone.Get<LabelWidget>("ROW_STATUS");
				if (isIdle)
				{
					statusLabel.GetText = () => "Idle";
					statusLabel.GetColor = () => IdleColor;
				}
				else
				{
					var percent = current.TotalTime <= 1 ? 100 : 100 - 100 * current.RemainingTime / current.TotalTime;
					var itemName = DisplayName(world.Map.Rules.Actors[current.Item]);
					var pausedSuffix = current.Paused ? " (paused)" : "";
					statusLabel.GetText = () => $"{itemName} {percent}%{pausedSuffix}";
					statusLabel.GetColor = () => current.Paused ? PausedColor : BuildingColor;
				}

				var pauseButton = clone.Get<ButtonWidget>("ROW_PAUSE");
				pauseButton.IsDisabled = () => isIdle;
				var pauseText = current != null && current.Paused ? "Resume" : "Pause";
				pauseButton.GetText = () => pauseText;
				if (!isIdle)
				{
					var toPause = !current.Paused;
					pauseButton.OnClick = () => world.IssueOrder(Order.PauseProduction(actor, current.Item, toPause));
				}

				var clearButton = clone.Get<ButtonWidget>("ROW_CLEAR");
				var hasQueue = queues.Any(q => q.AllQueued().Any());
				clearButton.IsDisabled = () => !hasQueue;
				if (hasQueue)
				{
					clearButton.OnClick = () =>
					{
						foreach (var queue in queues)
							foreach (var group in queue.AllQueued().GroupBy(i => i.Item).ToList())
								world.IssueOrder(Order.CancelProduction(queue.Actor, group.Key, group.Count()));
					};
				}

				var cellsHolder = clone.Get<ContainerWidget>("ROW_CELLS");
				for (var i = 0; i < activeColumns.Count; i++)
				{
					var itemActor = activeColumns[i];
					var queuedCount = queues.Sum(q => q.AllQueued().Count(qi => qi.Item == itemActor.Name));
					var isCurrent = current != null && current.Item == itemActor.Name;

					var cell = (ButtonWidget)cellTemplate.Clone();
					cell.Visible = true;
					cell.IsVisible = () => cell.Visible;
					cell.Bounds = new Rectangle(i * (ColumnWidth + ColumnGap), cell.Bounds.Y, ColumnWidth, cell.Bounds.Height);
					cell.Highlighted = isCurrent;

					var text = queuedCount > 0 ? "x" + queuedCount : string.Empty;
					cell.GetText = () => text;

					// A Building/Defence row has two queues; find whichever of them can actually
					// build this column's item (BuildableItems, not just AllItems - respects
					// prerequisites/power/faction so we never queue on a queue that would reject it).
					var itemName = itemActor.Name;
					var queueForItem = queues.FirstOrDefault(q => q.BuildableItems().Any(a => a.Name == itemName));
					if (queueForItem != null)
					{
						cell.OnMouseDown = mi =>
							world.IssueOrder(Order.StartProduction(queueForItem.Actor, itemName, AmountFor(mi)));
					}
					else
						cell.IsDisabled = () => true;

					cellsHolder.AddChild(cell);
				}

				gridBody.AddChild(clone);
			}
		}
	}
}
