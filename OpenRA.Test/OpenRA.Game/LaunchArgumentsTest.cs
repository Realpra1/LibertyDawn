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
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public class LaunchArgumentsTest
	{
		[Test]
		public void ParsesOrderedLobbyCommands()
		{
			var launch = new LaunchArguments(new Arguments(
				"Launch.LobbyCommands=spectate; option gamespeed fastest;slot_bot Multi0 1 skynet"));

			Assert.That(launch.GetLobbyCommands(), Is.EqualTo(new[]
			{
				"spectate",
				"option gamespeed fastest",
				"slot_bot Multi0 1 skynet"
			}));
		}

		[TestCase("spectate;;option gamespeed fastest")]
		[TestCase("startgame")]
		[TestCase("state Ready")]
		public void RejectsUnsafeLobbyCommandSequences(string commands)
		{
			var launch = new LaunchArguments(new Arguments($"Launch.LobbyCommands={commands}"));
			Assert.Throws<ArgumentException>(() => launch.GetLobbyCommands());
		}

		[Test]
		public void RejectsNewlinesInLobbyCommands()
		{
			var launch = new LaunchArguments(null)
			{
				LobbyCommands = "spectate\noption gamespeed fastest"
			};

			Assert.Throws<ArgumentException>(() => launch.GetLobbyCommands());
		}

		[Test]
		public void ParsesAutomatedSaveArguments()
		{
			var launch = new LaunchArguments(new Arguments(
				"Launch.GameSave=test.orasav",
				"Launch.SaveGameAtTick=1234",
				"Launch.SaveGameName=checkpoint.orasav",
				"Launch.RandomSeed=24680"));

			Assert.That(launch.GameSave, Is.EqualTo("test.orasav"));
			Assert.That(launch.SaveGameAtTick, Is.EqualTo(1234));
			Assert.That(launch.SaveGameName, Is.EqualTo("checkpoint.orasav"));
			Assert.That(launch.RandomSeed, Is.EqualTo(24680));
		}

		[Test]
		public void AcceptsHeadlessMaxMapAutomation()
		{
			var launch = new LaunchArguments(new Arguments(
				"Launch.Headless=true",
				"Launch.Map=test.oramap",
				"Launch.LobbyCommands=spectate;option gamespeed max",
				"Launch.ExitAtTick=5000"));

			Assert.That(launch.Headless, Is.True);
			Assert.That(launch.ExitAtTick, Is.EqualTo(5000));
			Assert.That(launch.HeadlessValidationError(), Is.Null);
		}

		[TestCase("Launch.Headless=true", "Launch.Headless requires Launch.Map or Launch.GameSave.")]
		[TestCase("Launch.Headless=true|Launch.Map=test.oramap|Launch.LobbyCommands=spectate;option gamespeed fastest", "Launch.Headless map games require 'option gamespeed max' in Launch.LobbyCommands.")]
		[TestCase("Launch.Headless=true|Launch.Connect=localhost:1234|Launch.Map=test.oramap|Launch.LobbyCommands=option gamespeed max", "Launch.Headless supports local automated games only.")]
		[TestCase("Launch.Headless=true|Launch.Replay=test.orarep", "Launch.Headless does not support replay playback.")]
		[TestCase("Launch.ExitAtTick=5000|Launch.Map=test.oramap", "Launch.ExitAtTick requires Launch.Headless=true.")]
		public void RejectsInvalidHeadlessAutomation(string arguments, string expectedError)
		{
			var launch = new LaunchArguments(new Arguments(arguments.Split('|')));
			Assert.That(launch.HeadlessValidationError(), Is.EqualTo(expectedError));
		}
	}
}
