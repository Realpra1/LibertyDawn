#region Copyright & License Information
/*
 * Copyright 2007,2009,2010 Chris Forbes, Robert Pepperell, Matthew Bowra-Dean, Paul Chote, Alli Witheford.
 * This file is part of OpenRA.
 * 
 *  OpenRA is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 * 
 *  OpenRA is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 * 
 *  You should have received a copy of the GNU General Public License
 *  along with OpenRA.  If not, see <http://www.gnu.org/licenses/>.
 */
#endregion

using System;
using System.Drawing;

namespace OpenRA.Widgets
{
	class ColorBlockWidget : Widget 
	{
		public Func<int> GetPaletteIndex;
		
		public ColorBlockWidget()
			: base()
		{
			GetPaletteIndex = () => 0;
		}
		
		public ColorBlockWidget(Widget widget)
			:base(widget)
		{
			GetPaletteIndex = (widget as ColorBlockWidget).GetPaletteIndex;
		}
		
		public override Widget Clone()
		{	
			return new ColorBlockWidget(this);
		}
		
		public override void Draw(World world)
		{
			if (!IsVisible())
			{
				base.Draw(world);
				return;
			}
			
			var pos = DrawPosition();
			var paletteRect = new RectangleF(pos.X + Game.viewport.Location.X, pos.Y + Game.viewport.Location.Y, Bounds.Width, Bounds.Height);
			Game.chrome.lineRenderer.FillRect(paletteRect, Player.PlayerColors(Game.world)[GetPaletteIndex() % Player.PlayerColors(Game.world).Count].c);
			
			base.Draw(world);
		}
	}
}
