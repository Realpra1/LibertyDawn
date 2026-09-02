#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Lint;
using OpenRA.Mods.Common.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	[ChromeLogicArgsHotkeys("ToggleStealthSquadOverlayKey")]
	public sealed class StealthSquadOverlayHotkeyLogic : ChromeLogic
	{
		[ObjectCreator.UseCtor]
		public StealthSquadOverlayHotkeyLogic(Widget widget, World world, ModData modData,
			Dictionary<string, MiniYaml> logicArgs)
		{
			var hotkey = new HotkeyReference();
			if (logicArgs.TryGetValue("ToggleStealthSquadOverlayKey", out var yaml))
				hotkey = modData.Hotkeys[yaml.Value];

			widget.Get<LogicKeyListenerWidget>("OBSERVER_KEY_LISTENER").AddHandler(input =>
			{
				if (input.Event != KeyInputEvent.Down || !hotkey.IsActivatedBy(input))
					return false;
				var overlays = world.Players.Select(player =>
					player.PlayerActor.TraitOrDefault<StealthSquadOverlay>())
					.Where(overlay => overlay != null).ToArray();
				var enabled = !overlays.Any(overlay => overlay.Enabled);
				foreach (var overlay in overlays)
					overlay.Enabled = enabled;
				return true;
			});
		}
	}
}
