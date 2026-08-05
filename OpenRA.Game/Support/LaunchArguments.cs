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
using OpenRA.Network;

namespace OpenRA
{
	public class LaunchArguments
	{
		[Desc("Connect to the following server given as IP:PORT on startup.")]
		public string Connect;

		[Desc("Connect to the unified resource identifier openra://IP:PORT on startup.")]
		public string URI;

		[Desc("Automatically start playing the given replay file.")]
		public string Replay;

		[Desc("Dump performance data into cpu.csv and render.csv in the logs folder with the given prefix.")]
		public string Benchmark;

		[Desc("Run an automated local MAX game without showing or rendering the game window.")]
		public bool Headless;

		[Desc("Automatically start playing the given map.")]
		public string Map;

		[Desc("Automatically load the given local game save.")]
		public string GameSave;

		[Desc("Automatically create a local game save at the given world tick. Disabled when negative.")]
		public int SaveGameAtTick = -1;

		[Desc("Automatically exit a headless local game at the given world tick. Disabled when negative.")]
		public int ExitAtTick = -1;

		[Desc("Deterministic random seed for an automatically started local map. Disabled when omitted.")]
		public int RandomSeed = int.MinValue;

		[Desc("Filename used by SaveGameAtTick.")]
		public string SaveGameName = "automated-test.orasav";

		[Desc("Semicolon-separated local lobby commands to execute before automatically starting a map.")]
		public string LobbyCommands;

		public LaunchArguments(Arguments args)
		{
			if (args == null)
				return;

			foreach (var f in GetType().GetFields())
				if (args.Contains("Launch" + "." + f.Name))
					FieldLoader.LoadField(this, f.Name, args.GetValue("Launch" + "." + f.Name, ""));
		}

		public ConnectionTarget GetConnectEndPoint()
		{
			try
			{
				Uri uri;
				if (!string.IsNullOrEmpty(URI))
					uri = new Uri(URI);
				else if (!string.IsNullOrEmpty(Connect))
					uri = new Uri("tcp://" + Connect);
				else
					return null;

				if (uri.IsAbsoluteUri)
					return new ConnectionTarget(uri.Host, uri.Port);
				else
					return null;
			}
			catch (Exception ex)
			{
				Log.Write("client", "Failed to parse Launch.URI or Launch.Connect: {0}", ex.Message);
				return null;
			}
		}

		public IReadOnlyList<string> GetLobbyCommands()
		{
			if (string.IsNullOrWhiteSpace(LobbyCommands))
				return Array.Empty<string>();

			if (LobbyCommands.IndexOfAny(new[] { '\r', '\n' }) >= 0)
				throw new ArgumentException("Launch.LobbyCommands must not contain newlines.");

			var commands = new List<string>();
			foreach (var command in LobbyCommands.Split(';'))
			{
				var trimmed = command.Trim();
				if (trimmed.Length == 0)
					throw new ArgumentException("Launch.LobbyCommands must not contain empty commands.");

				var commandName = trimmed.Split(' ')[0];
				if (commandName == "state" || commandName == "startgame")
					throw new ArgumentException($"Launch.LobbyCommands cannot control game startup using '{commandName}'.");

				commands.Add(trimmed);
			}

			return commands;
		}

		public string HeadlessValidationError()
		{
			if (ExitAtTick >= 0 && !Headless)
				return "Launch.ExitAtTick requires Launch.Headless=true.";

			if (!Headless)
				return null;

			if (!string.IsNullOrEmpty(Connect) || !string.IsNullOrEmpty(URI))
				return "Launch.Headless supports local automated games only.";

			if (!string.IsNullOrEmpty(Replay))
				return "Launch.Headless does not support replay playback.";

			if (string.IsNullOrEmpty(Map) && string.IsNullOrEmpty(GameSave))
				return "Launch.Headless requires Launch.Map or Launch.GameSave.";

			if (!string.IsNullOrEmpty(Map) && !GetLobbyCommands().Any(c =>
				c.Equals("option gamespeed max", StringComparison.OrdinalIgnoreCase)))
				return "Launch.Headless map games require 'option gamespeed max' in Launch.LobbyCommands.";

			return null;
		}
	}
}
