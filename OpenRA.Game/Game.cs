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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime;
using System.Threading;
using OpenRA.Graphics;
using OpenRA.Network;
using OpenRA.Primitives;
using OpenRA.Server;
using OpenRA.Support;
using OpenRA.Widgets;

namespace OpenRA
{
	public static class Game
	{
		public const int TimestepJankThreshold = 250; // Don't catch up for delays larger than 250ms

		public static InstalledMods Mods { get; private set; }
		public static ExternalMods ExternalMods { get; private set; }

		public static ModData ModData;
		public static Settings Settings;
		public static CursorManager Cursor;
		public static bool HideCursor;

		static WorldRenderer worldRenderer;
		static string modLaunchWrapper;

		internal static OrderManager OrderManager;
		static Server.Server server;

		public static MersenneTwister CosmeticRandom = new MersenneTwister(); // not synced

		public static Renderer Renderer;
		public static Sound Sound;

		public static string EngineVersion { get; private set; }
		public static LocalPlayerProfile LocalPlayerProfile;

		static bool takeScreenshot = false;
		static Benchmark benchmark = null;
		public static bool IsBenchmarking => benchmark != null;
		static int automatedSaveTick = -1;
		static string automatedSaveName;
		static int automatedExitTick = -1;
		static bool automatedExitRequested;
		public static bool IsAutomatedGameSaveLoad { get; private set; }
		public static bool IsHeadlessAutomationRequested { get; private set; }
		public static bool IsHeadlessAutomation { get; private set; }
		public static bool IsPacedAutomation { get; private set; }
		static bool startupDiagnosticsEnabled;
		static readonly Stopwatch StartupDiagnosticsTimer = Stopwatch.StartNew();

		public static event Action OnShellmapLoaded = () => { };

		public static OrderManager JoinServer(ConnectionTarget endpoint, string password, bool recordReplay = true)
		{
			var newConnection = new NetworkConnection(endpoint);
			if (recordReplay)
				newConnection.StartRecording(() => { return TimestampedFilename(); });

			var om = new OrderManager(newConnection);
			JoinInner(om);
			CurrentServerSettings.Password = password;
			CurrentServerSettings.Target = endpoint;

			lastConnectionState = ConnectionState.PreConnecting;
			ConnectionStateChanged(OrderManager, password, newConnection);

			return om;
		}

		public static string TimestampedFilename(bool includemilliseconds = false, string extra = "")
		{
			var format = includemilliseconds ? "yyyy-MM-ddTHHmmssfffZ" : "yyyy-MM-ddTHHmmssZ";
			return ModData.Manifest.Id + extra + "-" + DateTime.UtcNow.ToString(format, CultureInfo.InvariantCulture);
		}

		static void JoinInner(OrderManager om)
		{
			// HACK: The shellmap World and OrderManager are owned by the main menu's WorldRenderer instead of Game.
			// This allows us to switch Game.OrderManager from the shellmap to the new network connection when joining
			// a lobby, while keeping the OrderManager that runs the shellmap intact.
			// A matching check in World.Dispose (which is called by WorldRenderer.Dispose) makes sure that we dispose
			// the shellmap's OM when a lobby game actually starts.
			if (OrderManager?.World == null || OrderManager.World.Type != WorldType.Shellmap)
				OrderManager?.Dispose();

			OrderManager = om;
		}

		public static void JoinReplay(string replayFile)
		{
			JoinInner(new OrderManager(new ReplayConnection(replayFile)));
		}

		static void JoinLocal()
		{
			JoinInner(new OrderManager(new EchoConnection()));

			// Add a spectator client for the local player
			// On the shellmap this player is controlling the map via scripted orders
			OrderManager.LobbyInfo.Clients.Add(new Session.Client
			{
				Index = OrderManager.Connection.LocalClientId,
				Name = Settings.Player.Name,
				PreferredColor = Settings.Player.Color,
				Color = Settings.Player.Color,
				Faction = "Random",
				SpawnPoint = 0,
				Team = 0,
				State = Session.ClientState.Ready
			});
		}

		// More accurate replacement for Environment.TickCount
		static readonly Stopwatch stopwatch = Stopwatch.StartNew();
		public static long RunTime => stopwatch.ElapsedMilliseconds;

		public static int RenderFrame = 0;
		public static int NetFrameNumber => OrderManager.NetFrameNumber;
		public static int LocalTick => OrderManager.LocalFrameNumber;

		public static event Action<ConnectionTarget> OnRemoteDirectConnect = _ => { };
		public static event Action<OrderManager, string, NetworkConnection> ConnectionStateChanged = (om, pass, conn) => { };
		static ConnectionState lastConnectionState = ConnectionState.PreConnecting;
		public static int LocalClientId => OrderManager.Connection.LocalClientId;

		public static void RemoteDirectConnect(ConnectionTarget endpoint)
		{
			OnRemoteDirectConnect(endpoint);
		}

		// Hacky workaround for orderManager visibility
		public static Widget OpenWindow(World world, string widget)
		{
			return Ui.OpenWindow(widget, new WidgetArgs() { { "world", world }, { "orderManager", OrderManager }, { "worldRenderer", worldRenderer } });
		}

		// Who came up with the great idea of making these things
		// impossible for the things that want them to access them directly?
		public static Widget OpenWindow(string widget, WidgetArgs args)
		{
			return Ui.OpenWindow(widget, new WidgetArgs(args)
			{
				{ "world", worldRenderer.World },
				{ "orderManager", OrderManager },
				{ "worldRenderer", worldRenderer },
			});
		}

		// Load a widget with world, orderManager, worldRenderer args, without adding it to the widget tree
		public static Widget LoadWidget(World world, string id, Widget parent, WidgetArgs args)
		{
			return ModData.WidgetLoader.LoadWidget(new WidgetArgs(args)
			{
				{ "world", world },
				{ "orderManager", OrderManager },
				{ "worldRenderer", worldRenderer },
			}, parent, id);
		}

		public static event Action LobbyInfoChanged = () => { };

		internal static void SyncLobbyInfo()
		{
			LobbyInfoChanged();
		}

		public static event Action BeforeGameStart = () => { };
		internal static void StartGame(string mapUID, WorldType type)
		{
			// Dispose of the old world before creating a new one.
			worldRenderer?.Dispose();

			Cursor.SetCursor(null);
			BeforeGameStart();

			Map map;

			using (new PerfTimer("PrepareMap"))
				map = ModData.PrepareMap(mapUID);

			using (new PerfTimer("NewWorld"))
				OrderManager.World = new World(ModData, map, OrderManager, type);

			OrderManager.World.GameOver += FinishBenchmark;

			worldRenderer = new WorldRenderer(ModData, OrderManager.World);

			// Proactively collect memory during loading to reduce peak memory.
			GC.Collect();

			using (new PerfTimer("LoadComplete"))
				OrderManager.World.LoadComplete(worldRenderer);

			// Proactively collect memory during loading to reduce peak memory.
			GC.Collect();

			if (OrderManager.GameStarted)
				return;

			Ui.MouseFocusWidget = null;
			Ui.KeyboardFocusWidget = null;

			OrderManager.StartGame();
			if (IsHeadlessAutomation || IsPacedAutomation)
			{
				var bots = OrderManager.LobbyInfo.Clients
					.Where(client => client.IsBot)
					.OrderBy(client => client.Index)
					.Select(client => string.Format(CultureInfo.InvariantCulture,
						"{0}: bot={1}, faction={2}, team={3}, spawn={4}", client.Name, client.Bot,
						client.Faction, client.Team, client.SpawnPoint));
				Log.Write("debug", "{0} automation started map '{1}' with bots: {2}.",
					IsHeadlessAutomation ? "Headless MAX" : "Paced rendered", map.Title, string.Join("; ", bots));
			}

			worldRenderer.RefreshPalette();
			Cursor.SetCursor(ChromeMetrics.Get<string>("DefaultCursor"));

			// Now loading is completed, now is the ideal time to run a GC and compact the LOH.
			// - All the temporary garbage created during loading can be collected.
			// - Live objects are likely to live for the length of the game or longer,
			//   thus promoting them into a higher generation is not an issue.
			// - We can remove any fragmentation in the LOH caused by temporary loading garbage.
			// - A loading screen is visible, so a delay won't matter to the user.
			//   Much better to clean up now then to drop frames during gameplay for GC pauses.
			GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
			GC.Collect();
		}

		public static void RestartGame()
		{
			var replay = OrderManager.Connection as ReplayConnection;
			var replayName = replay?.Filename;
			var lobbyInfo = OrderManager.LobbyInfo;

			// Reseed the RNG so this isn't an exact repeat of the last game
			lobbyInfo.GlobalSettings.RandomSeed = CosmeticRandom.Next();

			var orders = new[]
			{
					Order.Command($"sync_lobby {lobbyInfo.Serialize()}"),
					Order.Command("startgame")
			};

			// Disconnect from the current game
			Disconnect();
			Ui.ResetAll();

			// Restart the game with the same replay/mission
			if (replay != null)
				JoinReplay(replayName);
			else
				CreateAndStartLocalServer(lobbyInfo.GlobalSettings.Map, orders);
		}

		public static void CreateAndStartLocalServer(string mapUID, IEnumerable<Order> setupOrders, int? randomSeed = null)
		{
			OrderManager om = null;

			Action lobbyReady = null;
			lobbyReady = () =>
			{
				LobbyInfoChanged -= lobbyReady;
				foreach (var o in setupOrders)
					om.IssueOrder(o);
			};

			LobbyInfoChanged += lobbyReady;

			om = JoinServer(CreateLocalServer(mapUID, randomSeed), "");
		}

		public static bool IsHost
		{
			get
			{
				var id = OrderManager.Connection.LocalClientId;
				var client = OrderManager.LobbyInfo.ClientWithIndex(id);
				return client != null && client.IsAdmin;
			}
		}

		static Modifiers modifiers;
		public static Modifiers GetModifierKeys() { return modifiers; }
		internal static void HandleModifierKeys(Modifiers mods) { modifiers = mods; }

		public static void InitializeSettings(Arguments args)
		{
			Settings = new Settings(Path.Combine(Platform.SupportDir, "settings.yaml"), args);
		}

		public static RunStatus InitializeAndRun(string[] args)
		{
			Initialize(new Arguments(args));

			// Proactively collect memory during loading to reduce peak memory.
			GC.Collect();
			return Run();
		}

		static void Initialize(Arguments args)
		{
			startupDiagnosticsEnabled = FieldLoader.GetValue<bool>("Debug.StartupDiagnostics",
				args.GetValue("Debug.StartupDiagnostics", "False"));
			IsHeadlessAutomationRequested = FieldLoader.GetValue<bool>("Launch.Headless",
				args.GetValue("Launch.Headless", "False"));

			var engineDirArg = args.GetValue("Engine.EngineDir", null);
			if (!string.IsNullOrEmpty(engineDirArg))
				Platform.OverrideEngineDir(engineDirArg);

			var supportDirArg = args.GetValue("Engine.SupportDir", null);
			if (!string.IsNullOrEmpty(supportDirArg))
				Platform.OverrideSupportDir(supportDirArg);

			StartupDiagnostic("paths", "engine={0} support={1} content={2} content-exists={3}",
				Platform.EngineDir, Platform.SupportDir, Path.Combine(Platform.SupportDir, "Content"),
				Directory.Exists(Path.Combine(Platform.SupportDir, "Content")));

			Console.WriteLine("Platform is {0}", Platform.CurrentPlatform);

			// Load the engine version as early as possible so it can be written to exception logs
			try
			{
				EngineVersion = File.ReadAllText(Path.Combine(Platform.EngineDir, "VERSION")).Trim();
			}
			catch { }

			if (string.IsNullOrEmpty(EngineVersion))
				EngineVersion = "Unknown";

			Console.WriteLine("Engine version is {0}", EngineVersion);
			Console.WriteLine("Runtime: {0}", Platform.RuntimeVersion);

			// Special case handling of Game.Mod argument: if it matches a real filesystem path
			// then we use this to override the mod search path, and replace it with the mod id
			var modID = args.GetValue("Game.Mod", null);
			var explicitModPaths = Array.Empty<string>();
			if (modID != null && (File.Exists(modID) || Directory.Exists(modID)))
			{
				explicitModPaths = new[] { modID };
				modID = Path.GetFileNameWithoutExtension(modID);
			}

			InitializeSettings(args);

			Log.AddChannel("perf", "perf.log");
			Log.AddChannel("debug", "debug.log");
			Log.AddChannel("server", "server.log", true);
			Log.AddChannel("sound", "sound.log");
			Log.AddChannel("graphics", "graphics.log");
			Log.AddChannel("geoip", "geoip.log");
			Log.AddChannel("nat", "nat.log");
			Log.AddChannel("client", "client.log");

			var platforms = new[] { Settings.Game.Platform, "Default", null };
			foreach (var p in platforms)
			{
				if (p == null)
					throw new InvalidOperationException("Failed to initialize platform-integration library. Check graphics.log for details.");

				Settings.Game.Platform = p;
				try
				{
					var rendererPath = Path.Combine(Platform.BinDir, "OpenRA.Platforms." + p + ".dll");

#if !MONO
					var loader = new AssemblyLoader(rendererPath);
					var platformType = loader.LoadDefaultAssembly().GetTypes().SingleOrDefault(t => typeof(IPlatform).IsAssignableFrom(t));

#else
					// NOTE: This is currently the only use of System.Reflection in this file, so would give an unused using error if we import it above
					var assembly = System.Reflection.Assembly.LoadFile(rendererPath);
					var platformType = assembly.GetTypes().SingleOrDefault(t => typeof(IPlatform).IsAssignableFrom(t));
#endif

					if (platformType == null)
						throw new InvalidOperationException("Platform dll must include exactly one IPlatform implementation.");

					var platform = (IPlatform)platformType.GetConstructor(Type.EmptyTypes).Invoke(null);
					Renderer = new Renderer(platform, Settings.Graphics, IsHeadlessAutomationRequested);
					Sound = new Sound(platform, Settings.Sound);

					break;
				}
				catch (Exception e)
				{
					Log.Write("graphics", "{0}", e);
					Console.WriteLine("Renderer initialization failed. Check graphics.log for details.");

					Renderer?.Dispose();

					Sound?.Dispose();
				}
			}

			Nat.Initialize();

			var modSearchArg = args.GetValue("Engine.ModSearchPaths", null);
			var modSearchPaths = modSearchArg != null ?
				FieldLoader.GetValue<string[]>("Engine.ModsPath", modSearchArg) :
				new[] { Path.Combine(Platform.EngineDir, "mods") };

			Mods = new InstalledMods(modSearchPaths, explicitModPaths);
			Console.WriteLine("Internal mods:");
			foreach (var mod in Mods)
				Console.WriteLine("\t{0}: {1} ({2})", mod.Key, mod.Value.Metadata.Title, mod.Value.Metadata.Version);

			modLaunchWrapper = args.GetValue("Engine.LaunchWrapper", null);

			ExternalMods = new ExternalMods();

			if (modID != null && Mods.TryGetValue(modID, out _))
			{
				var launchPath = args.GetValue("Engine.LaunchPath", null);
				var launchArgs = new List<string>();

				// Sanitize input from platform-specific launchers
				// Process.Start requires paths to not be quoted, even if they contain spaces
				if (launchPath != null && launchPath.First() == '"' && launchPath.Last() == '"')
					launchPath = launchPath.Substring(1, launchPath.Length - 2);

				// Metadata registration requires an explicit launch path
				if (launchPath != null)
					ExternalMods.Register(Mods[modID], launchPath, launchArgs, ModRegistration.User);

				ExternalMods.ClearInvalidRegistrations(ModRegistration.User);
			}

			Console.WriteLine("External mods:");
			foreach (var mod in ExternalMods)
				Console.WriteLine("\t{0}: {1} ({2})", mod.Key, mod.Value.Title, mod.Value.Version);

			InitializeMod(modID, args);
			Ui.InitializeTranslation();
		}

		public static void InitializeMod(string mod, Arguments args)
		{
			// Clear static state if we have switched mods
			LobbyInfoChanged = () => { };
			ConnectionStateChanged = (om, p, conn) => { };
			BeforeGameStart = () => { };
			OnRemoteDirectConnect = endpoint => { };
			delayedActions = new ActionQueue();

			Ui.ResetAll();

			worldRenderer?.Dispose();
			worldRenderer = null;
			server?.Shutdown();
			OrderManager?.Dispose();

			if (ModData != null)
			{
				ModData.ModFiles.UnmountAll();
				ModData.Dispose();
			}

			ModData = null;

			if (mod == null)
				throw new InvalidOperationException("Game.Mod argument missing.");

			if (!Mods.ContainsKey(mod))
				throw new InvalidOperationException($"Unknown or invalid mod '{mod}'.");

			Console.WriteLine("Loading mod: {0}", mod);
			StartupDiagnostic("mod", "begin id={0} package={1}", mod, Mods[mod].Package.Name);

			Sound.StopVideo();
			StartupDiagnostic("sound", "video-stopped");

			StartupDiagnostic("mod-data", "construct-enter");
			ModData = new ModData(Mods[mod], Mods, true);
			StartupDiagnostic("mod-data", "construct-exit");

			StartupDiagnostic("profile", "load-enter");
			LocalPlayerProfile = new LocalPlayerProfile(Path.Combine(Platform.SupportDir, Settings.Game.AuthProfile), ModData.Manifest.Get<PlayerDatabase>());
			StartupDiagnostic("profile", "load-exit");

			StartupDiagnostic("load-screen", "before-load-enter");
			if (!ModData.LoadScreen.BeforeLoad())
			{
				StartupDiagnostic("load-screen", "before-load-aborted");
				return;
			}

			StartupDiagnostic("load-screen", "before-load-exit");

			StartupDiagnostic("asset-loaders", "initialize-enter");
			ModData.InitializeLoaders(ModData.DefaultFileSystem);
			StartupDiagnostic("asset-loaders", "initialize-exit");
			StartupDiagnostic("fonts", "initialize-enter");
			Renderer.InitializeFonts(ModData);
			StartupDiagnostic("fonts", "initialize-exit");

			StartupDiagnostic("maps", "load-enter folders={0}", ModData.Manifest.MapFolders.Count);
			using (new PerfTimer("LoadMaps"))
				ModData.MapCache.LoadMaps();
			StartupDiagnostic("maps", "load-exit locations={0}", ModData.MapCache.MapLocations.Count);

			var grid = ModData.Manifest.Contains<MapGrid>() ? ModData.Manifest.Get<MapGrid>() : null;
			Renderer.InitializeDepthBuffer(grid);

			Cursor?.Dispose();
			Cursor = new CursorManager(ModData.CursorProvider);

			var metadata = ModData.Manifest.Metadata;
			if (!string.IsNullOrEmpty(metadata.WindowTitle))
				Renderer.Window.SetWindowTitle(metadata.WindowTitle);

			PerfHistory.Items["render"].HasNormalTick = false;
			PerfHistory.Items["batches"].HasNormalTick = false;
			PerfHistory.Items["render_world"].HasNormalTick = false;
			PerfHistory.Items["render_widgets"].HasNormalTick = false;
			PerfHistory.Items["render_flip"].HasNormalTick = false;
			PerfHistory.Items["terrain_lighting"].HasNormalTick = false;

			JoinLocal();

			StartupDiagnostic("load-screen", "start-game-enter");
			ModData.LoadScreen.StartGame(args);
			StartupDiagnostic("load-screen", "start-game-exit");
		}

		internal static void StartupDiagnostic(string stage, string format, params object[] args)
		{
			if (!startupDiagnosticsEnabled)
				return;

			Console.WriteLine("Startup diagnostic: elapsed={0}ms thread={1} stage={2} {3}",
				StartupDiagnosticsTimer.ElapsedMilliseconds, Environment.CurrentManagedThreadId,
				stage, string.Format(CultureInfo.InvariantCulture, format, args));
			Console.Out.Flush();
		}

		public static void LoadEditor(string mapUid)
		{
			JoinLocal();
			StartGame(mapUid, WorldType.Editor);
		}

		public static void LoadShellMap()
		{
			var shellmap = ChooseShellmap();
			using (new PerfTimer("StartGame"))
			{
				StartGame(shellmap, WorldType.Shellmap);
				OnShellmapLoaded();
			}
		}

		static string ChooseShellmap()
		{
			var shellmaps = ModData.MapCache
				.Where(m => m.Status == MapStatus.Available && m.Visibility.HasFlag(MapVisibility.Shellmap))
				.Select(m => m.Uid);

			if (!shellmaps.Any())
				throw new InvalidDataException("No valid shellmaps available");

			return shellmaps.Random(CosmeticRandom);
		}

		public static void SwitchToExternalMod(ExternalMod mod, string[] launchArguments = null, Action onFailed = null)
		{
			try
			{
				var path = mod.LaunchPath;
				var args = launchArguments != null ? mod.LaunchArgs.Append(launchArguments) : mod.LaunchArgs;
				if (modLaunchWrapper != null)
				{
					path = modLaunchWrapper;
					args = new[] { mod.LaunchPath }.Concat(args);
				}

				var p = Process.Start(path, args.Select(a => "\"" + a + "\"").JoinWith(" "));
				if (p == null || p.HasExited)
					onFailed();
				else
				{
					p.Close();
					Exit();
				}
			}
			catch (Exception e)
			{
				Log.Write("debug", "Failed to switch to external mod.");
				Log.Write("debug", "Error was: " + e.Message);
				onFailed();
			}
		}

		static RunStatus state = RunStatus.Running;
		public static event Action OnQuit = () => { };

		// Note: These delayed actions should only be used by widgets or disposing objects
		// - things that depend on a particular world should be queuing them on the world actor.
		static volatile ActionQueue delayedActions = new ActionQueue();

		public static void RunAfterTick(Action a) { delayedActions.Add(a, RunTime); }
		public static void RunAfterDelay(int delayMilliseconds, Action a) { delayedActions.Add(a, RunTime + delayMilliseconds); }

		static void TakeScreenshotInner()
		{
			using (new PerfTimer("Renderer.SaveScreenshot"))
			{
				var mod = ModData.Manifest.Metadata;
				var directory = Path.Combine(Platform.SupportDir, "Screenshots", ModData.Manifest.Id, mod.Version);
				Directory.CreateDirectory(directory);

				var filename = TimestampedFilename(true);
				var path = Path.Combine(directory, string.Concat(filename, ".png"));
				Log.Write("debug", "Taking screenshot " + path);

				Renderer.SaveScreenshot(path);
				TextNotificationsManager.Debug("Saved screenshot " + filename);
			}
		}

		static bool InnerLogicTick(OrderManager orderManager, bool forceWorldTick = false)
		{
			var tick = RunTime;
			var worldTicked = false;

			// Benchmark.Tick records the active world only. Keep the phase attribution on
			// that same world when a shellmap is also being ticked during a transition.
			var recordLogicPhases = benchmark != null && ReferenceEquals(orderManager, Game.OrderManager);

			var world = orderManager.World;

			if (Ui.LastTickTime.ShouldAdvance(tick))
			{
				Ui.LastTickTime.AdvanceTickTime(tick);
				Sync.RunUnsynced(world, Ui.Tick);
				Cursor.Tick();
			}

			if (forceWorldTick || orderManager.LastTickTime.ShouldAdvance(tick))
			{
				using (new PerfSample("tick_time"))
				{
					if (forceWorldTick)
						orderManager.LastTickTime.Value = tick;
					else
						orderManager.LastTickTime.AdvanceTickTime(tick);

					var phaseStart = recordLogicPhases ? Stopwatch.GetTimestamp() : 0;
					Sound.Tick();
					Sync.RunUnsynced(world, orderManager.TickImmediate);
					if (recordLogicPhases)
						benchmark.LogicPhase(LocalTick, "immediate", ElapsedMilliseconds(phaseStart));

					if (world == null)
						return false;

					if (orderManager.TryTick())
					{
						worldTicked = true;
						phaseStart = recordLogicPhases ? Stopwatch.GetTimestamp() : 0;
						Sync.RunUnsynced(world, () =>
						{
							world.OrderGenerator.Tick(world);
						});
						if (recordLogicPhases)
							benchmark.LogicPhase(LocalTick, "order-generator", ElapsedMilliseconds(phaseStart));

						phaseStart = recordLogicPhases ? Stopwatch.GetTimestamp() : 0;
						world.Tick();
						if (recordLogicPhases)
							benchmark.LogicPhase(LocalTick, "world", ElapsedMilliseconds(phaseStart));
						if (ReferenceEquals(orderManager, OrderManager))
							TryAutomatedSave(world);

						PerfHistory.Tick();
					}

					// Wait until we have done our first world Tick before TickRendering
					if (orderManager.LocalFrameNumber > 0 && !IsHeadlessAutomation)
					{
						phaseStart = recordLogicPhases ? Stopwatch.GetTimestamp() : 0;
						Sync.RunUnsynced(world, () => world.TickRender(worldRenderer));
						if (recordLogicPhases)
							benchmark.LogicPhase(LocalTick, "tick-render", ElapsedMilliseconds(phaseStart));
					}
				}

				benchmark?.Tick(LocalTick, world);
				if (worldTicked && ReferenceEquals(orderManager, OrderManager))
					TryAutomatedExit(world);
			}

			return worldTicked;
		}

		static double ElapsedMilliseconds(long start)
		{
			return 1000.0 * Math.Max(0, Stopwatch.GetTimestamp() - start) / Stopwatch.Frequency;
		}

		static bool LogicTick(bool forceCurrentWorldTick = false)
		{
			PerformDelayedActions();

			if (OrderManager.Connection is NetworkConnection nc && nc.ConnectionState != lastConnectionState)
			{
				lastConnectionState = nc.ConnectionState;
				ConnectionStateChanged(OrderManager, null, nc);
			}

			var worldTicked = InnerLogicTick(OrderManager, forceCurrentWorldTick);
			if (worldRenderer != null && OrderManager.World != worldRenderer.World)
				InnerLogicTick(worldRenderer.World.OrderManager);

			return worldTicked;
		}

		public static void PerformDelayedActions()
		{
			delayedActions.PerformActions(RunTime);
		}

		public static void TakeScreenshot()
		{
			takeScreenshot = true;
		}

		static void RenderTick()
		{
			using (new PerfSample("render"))
			{
				++RenderFrame;

				// Prepare renderables (i.e. render voxels) before calling BeginFrame
				using (new PerfSample("render_prepare"))
				{
					Renderer.WorldModelRenderer.BeginFrame();

					// World rendering is disabled while the loading screen is displayed
					if (worldRenderer != null && !worldRenderer.World.IsLoadingGameSave)
					{
						worldRenderer.Viewport.Tick();
						worldRenderer.PrepareRenderables();
					}

					Ui.PrepareRenderables();
					Renderer.WorldModelRenderer.EndFrame();
				}

				// worldRenderer is null during the initial install/download screen
				// World rendering is disabled while the loading screen is displayed
				// Use worldRenderer.World instead of OrderManager.World to avoid a rendering mismatch while processing orders
				if (worldRenderer != null && !worldRenderer.World.IsLoadingGameSave)
				{
					Renderer.BeginWorld(worldRenderer.Viewport.Rectangle);
					Sound.SetListenerPosition(worldRenderer.Viewport.CenterPosition);
					using (new PerfSample("render_world"))
						worldRenderer.Draw();
				}

				using (new PerfSample("render_widgets"))
				{
					Renderer.BeginUI();

					if (worldRenderer != null && !worldRenderer.World.IsLoadingGameSave)
						worldRenderer.DrawAnnotations();

					Ui.Draw();

					if (ModData != null && ModData.CursorProvider != null)
					{
						if (HideCursor)
							Cursor.SetCursor(null);
						else
						{
							Cursor.SetCursor(Ui.Root.GetCursorOuter(Viewport.LastMousePos) ?? "default");
							Cursor.Render(Renderer);
						}
					}
				}

				using (new PerfSample("render_flip"))
					Renderer.EndFrame(new DefaultInputHandler(OrderManager.World));

				if (takeScreenshot)
				{
					takeScreenshot = false;
					TakeScreenshotInner();
				}
			}

			PerfHistory.Items["render"].Tick();
			PerfHistory.Items["batches"].Tick();
			PerfHistory.Items["render_world"].Tick();
			PerfHistory.Items["render_widgets"].Tick();
			PerfHistory.Items["render_flip"].Tick();
			PerfHistory.Items["terrain_lighting"].Tick();
			benchmark?.Render(RenderFrame);
		}

		static void Loop()
		{
			// The game loop mainly does two things: logic updates and
			// drawing on the screen.
			// ---
			// We ideally want the logic to run every 'Timestep' ms and
			// rendering to be done at 'MaxFramerate', so 1000 / MaxFramerate ms.
			// Any additional free time is used in 'Sleep' so we don't
			// consume more CPU/GPU resources than necessary.
			// ---
			// In case logic or rendering takes more time than the ideal
			// and we're getting behind, we can skip rendering some frames
			// but there's a fail-safe minimum FPS to make sure the screen
			// gets updated at least that often.
			// ---
			// TODO: Separate world/UI rendering
			// It would be nice to separate the world rendering from the UI rendering
			// so that we can update the UI more often than the world. This would
			// help make the game playable (mouse/controls) even in low world
			// framerates.
			// It's not possible at the moment because the render buffer is cleared
			// before rendering and we don't keep the last rendered world buffer.

			// When the logic has fallen behind by this much, skip the pending
			// updates and start fresh.
			// For example, if we want to update logic every 10 ms but each loop
			// temporarily takes 100 ms, the 'nextLogic' timestamp will be too low
			// and the current timestamp ('now') will have moved on. Even if the
			// update time returns to normal, it will take a long time to catch up
			// (if ever).
			// This also means that the 'logicInterval' cannot be longer than this
			// value.
			const int MaxLogicTicksBehind = 250;

			// Try to maintain at least this many FPS during replays, even if it slows down logic.
			// However, if the user has enabled a framerate limit that is even lower
			// than this, then that limit will be used.
			const int MinReplayFps = 10;

			// Timestamps for when the next logic and rendering should run
			var nextLogic = RunTime;
			var nextRender = RunTime;
			var forcedNextRender = RunTime;
			var renderBeforeNextTick = false;
			var maximumSpeedActive = false;
			var maximumSpeedNextProgressTick = 0;
			var maximumSpeedLastProgress = RunTime;
			var maximumSpeedLastWarning = RunTime;
			var maximumSpeedLastWorldTick = -1;
			var headlessNextInputPump = RunTime;

			while (state == RunStatus.Running)
			{
				var logicInterval = Ui.Timestep;
				var logicWorld = worldRenderer?.World;
				var runAtMaximumSpeed = logicWorld != null && logicWorld == OrderManager.World &&
					logicWorld.GameSpeed.UsesMaximumSpeed(server?.Type == ServerType.Local,
						logicWorld.IsReplay, logicWorld.IsLoadingGameSave);

				if (runAtMaximumSpeed != maximumSpeedActive)
				{
					maximumSpeedActive = runAtMaximumSpeed;
					Log.Write("debug", "MAX game speed {0} at world tick {1}.",
						runAtMaximumSpeed ? "enabled" : "disabled", logicWorld != null ? logicWorld.WorldTick : -1);
					if (runAtMaximumSpeed)
					{
						maximumSpeedNextProgressTick = logicWorld.WorldTick + 5000;
						maximumSpeedLastWorldTick = logicWorld.WorldTick;
						maximumSpeedLastProgress = maximumSpeedLastWarning = RunTime;
					}
				}

				// ReplayTimestep = 0 means the replay is paused: we need to keep logicInterval as UI.Timestep to avoid breakage
				if (logicWorld != null && !(logicWorld.IsReplay && logicWorld.ReplayTimestep == 0))
					logicInterval = logicWorld == OrderManager.World ? OrderManager.SuggestedTimestep : logicWorld.Timestep;

				if (runAtMaximumSpeed)
					logicInterval = 1;

				// Ideal time between screen updates
				var maxFramerate = Settings.Graphics.CapFramerate ? Settings.Graphics.MaxFramerate.Clamp(1, 1000) : 1000;
				var renderInterval = 1000 / maxFramerate;

				// Tick as fast as possible while restoring game saves, capping rendering at 5 FPS
				if (OrderManager.World != null && OrderManager.World.IsLoadingGameSave)
				{
					logicInterval = 1;
					renderInterval = 200;
				}

				var now = RunTime;
				if (runAtMaximumSpeed)
					nextLogic = now;

				// If the logic has fallen behind too much, skip it and catch up
				if (now - nextLogic > MaxLogicTicksBehind)
					nextLogic = now;

				// A regular load may request a final render before headless automation is activated.
				// Headless mode deliberately has no render path to clear that gate, so discard it here.
				if (IsHeadlessAutomation)
					renderBeforeNextTick = false;

				// When's the next update (logic or render)
				var nextUpdate = IsHeadlessAutomation ? nextLogic : Math.Min(nextLogic, nextRender);
				if (now >= nextUpdate)
				{
					var forceRender = renderBeforeNextTick || now >= forcedNextRender;
					var worldTicked = false;

					if (now >= nextLogic && !renderBeforeNextTick)
					{
						nextLogic = runAtMaximumSpeed ? now : nextLogic + logicInterval;

						worldTicked = LogicTick(runAtMaximumSpeed);

						// Force at least one render per tick during regular gameplay
						if (!runAtMaximumSpeed && OrderManager.World != null &&
							!OrderManager.World.IsLoadingGameSave && !OrderManager.World.IsReplay)
							renderBeforeNextTick = true;
					}

					if (runAtMaximumSpeed)
					{
						var progressTime = RunTime;
						if (logicWorld.WorldTick != maximumSpeedLastWorldTick)
						{
							maximumSpeedLastWorldTick = logicWorld.WorldTick;
							maximumSpeedLastProgress = progressTime;
							if (logicWorld.WorldTick >= maximumSpeedNextProgressTick)
							{
								Log.Write("debug", "MAX progress: world={0}, local={1}, net={2}, queued-orders={3}.",
									logicWorld.WorldTick, OrderManager.LocalFrameNumber,
									OrderManager.NetFrameNumber, OrderManager.OrderQueueLength);
								maximumSpeedNextProgressTick = logicWorld.WorldTick + 5000;
							}
						}
						else if (progressTime - maximumSpeedLastProgress >= 5000 &&
							progressTime - maximumSpeedLastWarning >= 5000)
						{
							maximumSpeedLastWarning = progressTime;
							Log.Write("debug", "MAX waiting for world progress: world={0}, local={1}, net={2}, queued-orders={3}, paused={4}.",
								logicWorld.WorldTick, OrderManager.LocalFrameNumber,
								OrderManager.NetFrameNumber, OrderManager.OrderQueueLength, logicWorld.Paused);
						}
					}

					var haveSomeTimeUntilNextLogic = now < nextLogic;
					var isTimeToRender = now >= nextRender;
					if (!IsHeadlessAutomation && !Renderer.WindowIsSuspended &&
						((isTimeToRender && haveSomeTimeUntilNextLogic) || forceRender))
					{
						nextRender = now + renderInterval;

						// Pick the minimum allowed FPS (the lower between 'minReplayFPS'
						// and the user's max frame rate) and convert it to maximum time
						// allowed between screen updates.
						// We do this before rendering to include the time rendering takes
						// in this interval.
						var maxRenderInterval = Math.Max(1000 / MinReplayFps, renderInterval);
						forcedNextRender = now + maxRenderInterval;

						RenderTick();
						renderBeforeNextTick = false;
					}

					// Simulate a render tick if it was time to render but we skip actually rendering
					if (!IsHeadlessAutomation && Renderer.WindowIsSuspended && isTimeToRender)
					{
						// Make sure that nextUpdate is set to a proper minimum interval
						nextRender = now + renderInterval;

						// Still process SDL events to allow a restore to come through
						Renderer.Window.PumpInput(new NullInputHandler());

						// Ensure that we still logic tick despite not rendering
						renderBeforeNextTick = false;
					}

					if (IsHeadlessAutomation && now >= headlessNextInputPump)
					{
						Renderer.Window.PumpInput(new NullInputHandler());
						headlessNextInputPump = now + 250;
					}

					// The local server runs on another thread. Yield when it has not supplied the next order frame yet.
					if (runAtMaximumSpeed && !worldTicked)
						Thread.Yield();
				}
				else
					Thread.Sleep((int)(nextUpdate - now));
			}
		}

		static RunStatus Run()
		{
			if (Settings.Graphics.MaxFramerate < 1)
			{
				Settings.Graphics.MaxFramerate = new GraphicSettings().MaxFramerate;
				Settings.Graphics.CapFramerate = false;
			}

			try
			{
				Loop();
			}
			finally
			{
				// Ensure that the active replay is properly saved
				OrderManager?.Dispose();
			}

			worldRenderer?.Dispose();
			ModData.Dispose();
			ChromeProvider.Deinitialize();

			Sound.Dispose();
			Renderer.Dispose();

			OnQuit();

			return state;
		}

		public static void Exit()
		{
			state = RunStatus.Success;
		}

		public static void Disconnect()
		{
			OrderManager.World?.TraitDict.PrintReport();

			OrderManager.Dispose();
			CloseServer();
			JoinLocal();
		}

		public static void CloseServer()
		{
			server?.Shutdown();
		}

		public static T CreateObject<T>(string name)
		{
			return ModData.ObjectCreator.CreateObject<T>(name);
		}

		public static ConnectionTarget CreateServer(ServerSettings settings)
		{
			var endpoints = new List<IPEndPoint>
			{
				new IPEndPoint(IPAddress.IPv6Any, settings.ListenPort),
				new IPEndPoint(IPAddress.Any, settings.ListenPort)
			};
			server = new Server.Server(endpoints, settings, ModData, ServerType.Multiplayer);

			return server.GetEndpointForLocalConnection();
		}

		public static ConnectionTarget CreateLocalServer(string map, int? randomSeed = null)
		{
			var settings = new ServerSettings()
			{
				Name = "Skirmish Game",
				Map = map,
				AdvertiseOnline = false
			};

			// Always connect to local games using the same loopback connection
			// Exposing multiple endpoints introduces a race condition on the client's PlayerIndex (sometimes 0, sometimes 1)
			// This would break the Restart button, which relies on the PlayerIndex always being the same for local servers
			var endpoints = new List<IPEndPoint>
			{
				new IPEndPoint(IPAddress.Loopback, 0)
			};
			server = new Server.Server(endpoints, settings, ModData, ServerType.Local, randomSeed);

			return server.GetEndpointForLocalConnection();
		}

		public static bool IsCurrentWorld(World world)
		{
			return OrderManager != null && OrderManager.World == world && !world.Disposing;
		}

		public static bool SetClipboardText(string text)
		{
			return Renderer.Window.SetClipboardText(text);
		}

		public static void BenchmarkMode(string prefix)
		{
			benchmark = new Benchmark(prefix);
		}

		public static void RecordBotModuleSample(int playerIndex, string module, double milliseconds, int queuedOrders)
		{
			benchmark?.BotModule(LocalTick, playerIndex, module, milliseconds, queuedOrders);
		}

		public static void ConfigureHeadlessAutomation()
		{
			if (!IsHeadlessAutomationRequested)
				throw new InvalidOperationException("Headless automation was not requested during engine startup.");

			IsHeadlessAutomation = true;
			Log.Write("debug", "Headless MAX automation enabled: game rendering suppressed; input events remain bounded.");
		}

		public static void ConfigurePacedAutomation()
		{
			IsPacedAutomation = true;
			Log.Write("debug", "Paced rendered automation enabled: normal rendering and presentation remain active.");
		}

		public static void ConfigureAutomatedSave(int worldTick, string filename)
		{
			automatedSaveTick = Math.Max(0, worldTick);
			automatedSaveName = string.IsNullOrWhiteSpace(filename) ? "automated-test.orasav" : filename;
		}

		public static void ConfigureAutomatedExit(int worldTick)
		{
			automatedExitTick = Math.Max(0, worldTick);
		}

		static void TryAutomatedExit(World world)
		{
			if (automatedExitTick < 0 || world.WorldTick < automatedExitTick || world.IsLoadingGameSave)
				return;

			automatedExitTick = -1;
			Log.Write("debug", "{0} automation reached configured exit at world tick {1}; exiting.",
				IsHeadlessAutomation ? "Headless MAX" : "Paced rendered", world.WorldTick);
			FinishBenchmark();
		}

		static void TryAutomatedSave(World world)
		{
			if (automatedSaveTick < 0 || world.WorldTick < automatedSaveTick || world.IsReplay || world.IsLoadingGameSave)
				return;

			var saveTick = automatedSaveTick;
			automatedSaveTick = -1;
			if (!world.LobbyInfo.GlobalSettings.GameSavesEnabled)
			{
				Log.Write("debug", "Automated save at tick {0} skipped because game saves are disabled.", saveTick);
				return;
			}

			if (!automatedSaveName.EndsWith(".orasav", StringComparison.OrdinalIgnoreCase))
				automatedSaveName += ".orasav";

			Log.Write("debug", "Requesting automated game save '{0}' at world tick {1}.", automatedSaveName, world.WorldTick);
			world.RequestGameSave(automatedSaveName);
		}

		public static void LoadGameSave(string savePath)
		{
			if (!Path.IsPathRooted(savePath))
				savePath = Path.Combine(Platform.SupportDir, "Saves", ModData.Manifest.Id,
					ModData.Manifest.Metadata.Version, savePath);

			var save = new GameSave(savePath);
			var map = ModData.MapCache[save.GlobalSettings.Map];
			if (map.Status != MapStatus.Available)
				throw new InvalidDataException($"Map '{save.GlobalSettings.Map}' required by game save is unavailable.");

			var orders = new[]
			{
				Order.FromTargetString("LoadGameSave", Path.GetFileName(savePath), true),
				Order.Command($"state {Session.ClientState.Ready}")
			};

			IsAutomatedGameSaveLoad = true;
			CreateAndStartLocalServer(map.Uid, orders);
		}

		public static void CompleteAutomatedGameSaveLoad()
		{
			IsAutomatedGameSaveLoad = false;
		}

		public static void LoadMap(string launchMap, IEnumerable<string> lobbyCommands = null, int? randomSeed = null)
		{
			var orders = new List<Order> { Order.Command("option gamespeed default") };
			if (lobbyCommands != null)
				orders.AddRange(lobbyCommands.Select(Order.Command));

			// Readiness is deliberately controlled here so custom setup cannot start the game
			// before every lobby command has been processed.
			orders.Add(Order.Command($"state {Session.ClientState.Ready}"));

			var map = ModData.MapCache.SingleOrDefault(m => m.Uid == launchMap || Path.GetFileName(m.Package.Name) == launchMap);
			if (map == null)
				map = ModData.MapCache.LoadExternalMap(launchMap);

			if (map == null)
				throw new ArgumentException($"Could not find map '{launchMap}'.");

			CreateAndStartLocalServer(map.Uid, orders, randomSeed);
		}

		public static void FinishBenchmark()
		{
			if (automatedExitRequested || (benchmark == null && !IsHeadlessAutomation && !IsPacedAutomation))
				return;

			automatedExitRequested = true;
			benchmark?.Write();
			if (IsHeadlessAutomation)
				Log.Write("debug", "Headless MAX automation reached natural game over; exiting.");
			else if (IsPacedAutomation)
				Log.Write("debug", "Paced rendered automation reached natural game over; exiting.");

			Exit();
		}
	}

	public static class CurrentServerSettings
	{
		public static string Password;
		public static ConnectionTarget Target;
		public static ExternalMod ServerExternalMod;
	}
}
