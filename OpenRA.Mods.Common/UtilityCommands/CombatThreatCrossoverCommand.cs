#region Copyright & License Information
/* Copyright 2007-2021 The OpenRA Developers (see AUTHORS) */
#endregion

using System;
using System.Globalization;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Mods.Common.UtilityCommands
{
	public sealed class CombatThreatCrossoverCommand : IUtilityCommand
	{
		string IUtilityCommand.Name => "--combat-threat-crossover";

		bool IUtilityCommand.ValidateArguments(string[] args) => args.Length == 3 || args.Length == 4;

		[Desc("ATTACKER DEFENDER [MAX-UNITS]", "Estimate and verify the combined-unit threat crossover.")]
		void IUtilityCommand.Run(Utility utility, string[] args)
		{
			Game.ModData = utility.ModData;
			var calculator = new GeneralizedCombatThreatCalculator(utility.ModData.DefaultRules);
			var attacker = args[1].ToLowerInvariant();
			var defender = args[2].ToLowerInvariant();
			var maximum = args.Length == 4 ? int.Parse(args[3], CultureInfo.InvariantCulture) : 10000;
			if (!calculator.TryGet(attacker, defender, out var pair))
				throw new InvalidOperationException($"No cached armed matchup exists for {attacker}/{defender}.");

			var result = calculator.CalculateCrossover(pair, maximum);
			Console.WriteLine("attacker\tdefender\tfound\tinitial_estimate\tcrossover_units\trecommended_plus_10_percent\tevaluations\t" +
				"defender_threat_to_group\tgroup_threat_to_defender");
			Console.WriteLine(string.Join("\t", attacker, defender, result.Found, result.InitialEstimate,
				result.UnitCount, GeneralizedCombatThreatCalculator.AddUnitCountSafetyMargin(result.UnitCount),
				result.Evaluations, F(result.Threat.DefenderThreatToGroup),
				F(result.Threat.GroupThreatToDefender)));
		}

		static string F(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);
	}
}
