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

using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	// OPS_GRID_HOST is only defined in mods that register ingame-opsgrid.yaml in their
	// ChromeLayout (currently cnc only), so this must not be wired into the shared
	// LoadIngamePlayerOrObserverUILogic - that class runs for every mod, and Game.LoadWidget
	// throws if the named widget isn't in that mod's WidgetLoader dictionary. Attached as an
	// extra Logic entry on cnc's own Container@INGAME_ROOT instead.
	public class LoadOpsGridLogic : ChromeLogic
	{
		[ObjectCreator.UseCtor]
		public LoadOpsGridLogic(Widget widget, World world)
		{
			var worldRoot = widget.Get("WORLD_ROOT");
			Game.LoadWidget(world, "OPS_GRID_HOST", worldRoot, new WidgetArgs());
		}
	}
}
