using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace StatsGenerator
{
	internal class Program
	{
		public static readonly string[] PATHS =
		{
			"C:\\Dev\\LibertyDawn\\mods\\cnc\\rules\\aircraft.yaml",
			"C:\\Dev\\LibertyDawn\\mods\\cnc\\rules\\vehicles.yaml",
			"C:\\Dev\\LibertyDawn\\mods\\cnc\\rules\\civilian.yaml",
			"C:\\Dev\\LibertyDawn\\mods\\cnc\\rules\\defaults.yaml",
			"C:\\Dev\\LibertyDawn\\mods\\cnc\\rules\\infantry.yaml",
			"C:\\Dev\\LibertyDawn\\mods\\cnc\\rules\\ships.yaml",
			"C:\\Dev\\LibertyDawn\\mods\\cnc\\rules\\structures.yaml",
			"C:\\Dev\\LibertyDawn\\mods\\cnc\\weapons\\missiles.yaml",
			"C:\\Dev\\LibertyDawn\\mods\\cnc\\weapons\\ballistics.yaml",
			"C:\\Dev\\LibertyDawn\\mods\\cnc\\weapons\\superweapons.yaml",
			"C:\\Dev\\LibertyDawn\\mods\\cnc\\weapons\\smallcaliber.yaml",
			"C:\\Dev\\LibertyDawn\\mods\\cnc\\weapons\\other.yaml",
			"C:\\Dev\\LibertyDawn\\mods\\cnc\\weapons\\explosions.yaml"
		};

		private static readonly Random random = new Random();
		private static readonly object syncLock = new object();

		public static int RandomNumber(int min, int max)
		{
			lock (syncLock)
			{
				// synchronize
				return random.Next(min, max);
			}
		}

		static void Main(string[] args)
		{
			foreach (var path in PATHS)
			{
				StringBuilder bld = new StringBuilder();

				var content = File.ReadAllLines(path);
				foreach (var s in content)
				{
					var stringToWrite = s;


					stringToWrite = CheckForSpeciffic(s, "HP", stringToWrite, 10, 50000);
					stringToWrite = CheckForSpeciffic(s, "Ammo", stringToWrite, 1, 100);
					stringToWrite = CheckForSpeciffic(s, "Damage", stringToWrite, 50, 1000);
					stringToWrite = CheckForSpeciffic(s, "Speed", stringToWrite, 3, 300);
					stringToWrite = CheckForSpeciffic(s, "Spread", stringToWrite, 50, 300);
					
					if (s.Contains("Cost"))
					{
						var update = s.Split(':');
						stringToWrite = string.Format("{0}: {1}", update[0], RandomNumber(10, 5000));
					}
					if (s.Contains("ScanRadius"))
					{
						var update = s.Split(':');
						stringToWrite = string.Format("{0}: {1}", update[0], RandomNumber(1, 20));
					}
					
					if (s.Contains("FireDelay"))
					{
						var update = s.Split(':');
						stringToWrite = string.Format("{0}: {1}", update[0], RandomNumber(1, 300));
					}
					if (s.Contains("Spread"))
					{
						var update = s.Split(':');
						stringToWrite = string.Format("{0}: {1}", update[0], RandomNumber(50, 300));
					}
					if (s.Contains("DetonationDelay"))
					{
						var update = s.Split(':');
						stringToWrite = string.Format("{0}: {1}", update[0], RandomNumber(10, 500));
					}
					if (s.Contains("ReloadDelay"))
					{
						var update = s.Split(':');
						stringToWrite = string.Format("{0}: {1}", update[0], RandomNumber(5, 200));
					}
					if (s.Contains("HorizontalRateOfTurn"))
					{
						var update = s.Split(':');
						stringToWrite = string.Format("{0}: {1}", update[0], RandomNumber(1, 200));
					}
					if (s.Contains("BurstDelays"))
					{
						var update = s.Split(':');
						stringToWrite = string.Format("{0}: {1}", update[0], RandomNumber(10, 300));
					}
				

					bld.AppendLine(stringToWrite);
				}

				var pathDic = Path.GetDirectoryName(path);
				var fileName = Path.GetFileNameWithoutExtension(path);
				var newPath = string.Format("{0}\\{1}.yaml",pathDic,fileName);
				File.WriteAllText(newPath, bld.ToString());
			}
		}

		private static string CheckForSpeciffic(string s, string check, string stringToWrite, int lower, int higher)
		{
			if (s.Contains(':')) // HP
			{
				var splitted = s.Split(':');
				if (splitted[0] == check ||
				    splitted[0] == string.Format("\t\t{0}", check) ||
				    splitted[0] == string.Format("\t{0}", check))
				{
					stringToWrite = string.Format("{0}: {1}", splitted[0], RandomNumber(lower, higher));
				}
			}

			return stringToWrite;
		}
	}
}
