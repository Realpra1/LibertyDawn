#region Copyright & License Information
/* Copyright 2007-2021 The OpenRA Developers (see AUTHORS) */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.UtilityCommands
{
	public sealed class CombatThreatAnnotation
	{
		public readonly string ActorType;
		public readonly string ActorName;
		public readonly string Text;

		public CombatThreatAnnotation(string actorType, string actorName, string text)
		{
			ActorType = actorType;
			ActorName = actorName;
			Text = text;
		}
	}

	public sealed class CombatThreatAnnotationsCommand : IUtilityCommand
	{
		sealed class RankedActor
		{
			public ActorInfo Info;
			public string Name;
			public string HeadingName;
			public int Cost;
			public bool IsAir;
			public bool IsImmobile;
		}

		sealed class RankedMatchup
		{
			public RankedActor Opponent;
			public string Description;
			public double EconomicRatio;
			public double IncomingKillTime;
			public bool IsCategoricalWeakness;
		}

		static readonly string[] ClassOrder =
		{
			"Infantry", "Buildings", "Economy", "Light Armor", "Heavy Armor"
		};

		string IUtilityCommand.Name => "--combat-threat-annotations";

		bool IUtilityCommand.ValidateArguments(string[] args) => args.Length == 1 || args.Length == 2;

		[Desc("[ACTOR]", "Generate Markdown combat matchup annotations from the current rules.")]
		void IUtilityCommand.Run(Utility utility, string[] args)
		{
			Game.ModData = utility.ModData;
			var annotations = Generate(utility.ModData, args.Length == 2 ? args[1] : null);
			if (annotations.Count == 0)
				throw new InvalidOperationException("No documented combat actor matched the requested filter.");

			foreach (var annotation in annotations)
				Console.WriteLine("### " + annotation.ActorName + "\n\n" + annotation.Text + "\n");
		}

		public static IReadOnlyList<CombatThreatAnnotation> Generate(ModData modData, string actorFilter = null)
		{
			if (modData == null)
				throw new ArgumentNullException(nameof(modData));

			var calculator = new GeneralizedCombatThreatCalculator(modData.DefaultRules,
				modData.Manifest.Get<MapGrid>().SubCellOffsets);
			var excluded = new HashSet<string> { "pvice", "rapt", "sheep", "steg", "trex", "tric" };
			var actors = modData.DefaultRules.Actors.Values
				.Where(a => !excluded.Contains(a.Name) && IsNormallyPurchasable(a))
				.Where(a => calculator.TryGet(a.Name, a.Name, out _))
				.Select(a =>
				{
					var name = a.TraitInfos<TooltipInfo>().First(t => t.EnabledByDefault).Name;
					return new RankedActor
					{
						Info = a,
						Name = name,
						HeadingName = a.Name == "msam" ? "Mobile SAM" : name,
						Cost = a.TraitInfo<ValuedInfo>().Cost,
						IsAir = a.HasTraitInfo<AircraftInfo>(),
						IsImmobile = !a.HasTraitInfo<MobileInfo>() && !a.HasTraitInfo<AircraftInfo>()
					};
				})
				.OrderBy(a => a.Name, StringComparer.Ordinal)
				.ToArray();
			var targets = modData.DefaultRules.Actors.Values
				.Where(a => !excluded.Contains(a.Name) && IsNormallyPurchasable(a) && ArmorClass(a) != null)
				.ToArray();
			var selected = actors.Where(actor => actorFilter == null ||
				actor.Info.Name.Equals(actorFilter, StringComparison.OrdinalIgnoreCase) ||
				actor.Name.Equals(actorFilter, StringComparison.OrdinalIgnoreCase) ||
				actor.HeadingName.Equals(actorFilter, StringComparison.OrdinalIgnoreCase));

			return selected.Select(actor => Generate(calculator, actors, targets, actor)).ToArray();
		}

		static CombatThreatAnnotation Generate(GeneralizedCombatThreatCalculator calculator,
			IReadOnlyCollection<RankedActor> actors, IReadOnlyCollection<ActorInfo> targets, RankedActor actor)
		{
			var targeting = TargetingLabel(calculator, actors, actor);
			var profiles = ClassOrder.Select(targetClass => ClassProfile(calculator, actor,
				targets.Where(target => target.Name != actor.Info.Name && ArmorClass(target) == targetClass),
				targetClass)).Where(profile => profile.HasValue).Select(profile => profile.Value).ToArray();
			var thirds = GeneralizedCombatThreatCalculator.RankClassKillTimeThirds(profiles);
			var bestClasses = ClassOrder.Where(c => thirds.TryGetValue(c, out var third) &&
				third == GeneralizedCombatThreatCalculator.KillTimeThird.Best).ToArray();
			var worstClasses = ClassOrder.Where(c => thirds.TryGetValue(c, out var third) &&
				third == GeneralizedCombatThreatCalculator.KillTimeThird.Worst).ToArray();
			var matchups = actors.Where(opponent => opponent.Info.Name != actor.Info.Name)
				.Where(opponent => !actor.IsImmobile || !opponent.IsImmobile)
				.Select(opponent => Matchup(calculator, actor, opponent))
				.Where(matchup => matchup != null)
				.ToArray();
			var categorical = matchups.Where(matchup => matchup.IsCategoricalWeakness)
				.OrderBy(matchup => matchup.IncomingKillTime)
				.ThenBy(matchup => matchup.Opponent.Name, StringComparer.Ordinal)
				.ToArray();
			if (categorical.Length > 0)
			{
				var hasAir = categorical.Any(matchup => matchup.Opponent.IsAir);
				var hasGround = categorical.Any(matchup => !matchup.Opponent.IsAir);
				worstClasses = new[]
				{
					hasAir && hasGround ? "All ground and air units" :
						hasAir ? "All air units" : "All ground units"
				};
			}

			var ordinary = matchups.Where(matchup => !matchup.IsCategoricalWeakness).ToArray();
			var best = ordinary.Where(matchup => !double.IsNegativeInfinity(matchup.EconomicRatio))
				.OrderByDescending(matchup => matchup.EconomicRatio)
				.ThenBy(matchup => matchup.Opponent.Name, StringComparer.Ordinal).Take(5).ToArray();
			var ordinaryWorst = ordinary.Where(matchup => !double.IsPositiveInfinity(matchup.EconomicRatio))
				.OrderBy(matchup => matchup.EconomicRatio)
				.ThenBy(matchup => matchup.Opponent.Name, StringComparer.Ordinal);
			var worst = categorical.Cast<RankedMatchup>().Concat(ordinaryWorst)
				.GroupBy(matchup => matchup.Opponent.Info.Name).Select(group => group.First()).Take(5).ToArray();
			var text = "Cost-adjusted matchups — " + targeting + ". Best against: " +
				Label(bestClasses) + ". Worst against: " + Label(worstClasses) + ". Best: " +
				Label(best.Select(matchup => matchup.Description)) + ". Worst: " +
				Label(worst.Select(matchup => matchup.Description)) + ".";
			return new CombatThreatAnnotation(actor.Info.Name, actor.HeadingName, Wrap(text));
		}

		static string TargetingLabel(GeneralizedCombatThreatCalculator calculator,
			IEnumerable<RankedActor> actors, RankedActor actor)
		{
			var canTargetAir = actors.Where(target => target.IsAir)
				.Any(target => calculator.CalculateBaseline(actor.Info.Name, target.Info.Name).Forward.CanTarget);
			var canTargetGround = actors.Where(target => !target.IsAir)
				.Any(target => calculator.CalculateBaseline(actor.Info.Name, target.Info.Name).Forward.CanTarget);
			return !canTargetAir && !canTargetGround ? "Cannot target: Air or Ground" :
				!canTargetAir ? "Cannot target: Air" : !canTargetGround ? "Cannot target: Ground" :
				"Can target: Ground and Air";
		}

		static GeneralizedCombatThreatCalculator.ClassKillTimeProfile? ClassProfile(
			GeneralizedCombatThreatCalculator calculator, RankedActor actor, IEnumerable<ActorInfo> targets,
			string targetClass)
		{
			var values = targets.Select(target => calculator.CalculateBaseline(actor.Info.Name, target.Name).Forward)
				.Where(threat => threat.CanTarget)
				.Select(threat => (CanEngage(threat) ? threat.TimeToKillTicks : double.PositiveInfinity) /
					threat.DefenderHitPoints)
				.OrderBy(value => value)
				.ToArray();
			if (values.Length == 0)
				return null;

			var middle = values.Length / 2;
			var median = values.Length % 2 == 0 ? (values[middle - 1] + values[middle]) / 2 : values[middle];
			return new GeneralizedCombatThreatCalculator.ClassKillTimeProfile(targetClass, median, 1);
		}

		static RankedMatchup Matchup(GeneralizedCombatThreatCalculator calculator,
			RankedActor actor, RankedActor opponent)
		{
			var pair = calculator.CalculateBaseline(opponent.Info.Name, actor.Info.Name);
			var opponentCanEngage = CanEngage(pair.Forward);
			var actorCanEngage = CanEngage(pair.Reverse);
			if (actor.IsImmobile && !opponent.IsImmobile && opponentCanEngage && !pair.Reverse.CanTarget)
				return new RankedMatchup
				{
					Opponent = opponent,
					Description = opponent.Name + " (cannot engage)",
					EconomicRatio = double.NegativeInfinity,
					IncomingKillTime = pair.Forward.TimeToKillTicks,
					IsCategoricalWeakness = true
				};

			if (!pair.Forward.CanTarget || !pair.Reverse.CanTarget || (!opponentCanEngage && !actorCanEngage))
				return null;
			if ((!opponentCanEngage || !actorCanEngage) && !actor.IsImmobile && !opponent.IsImmobile)
				return null;
			if (opponentCanEngage && !actorCanEngage)
				return new RankedMatchup
				{
					Opponent = opponent,
					Description = opponent.Name + " (cannot engage)",
					EconomicRatio = double.NegativeInfinity
				};
			if (!opponentCanEngage && actorCanEngage)
				return new RankedMatchup
				{
					Opponent = opponent,
					Description = opponent.Name + " (immune)",
					EconomicRatio = double.PositiveInfinity
				};

			var attackers = calculator.CalculateCrossover(pair);
			if (attackers.UnitCount > 1)
				return new RankedMatchup
				{
					Opponent = opponent,
					Description = opponent.Name + " (x" + attackers.UnitCount + ")",
					EconomicRatio = attackers.UnitCount * opponent.Cost / (double)actor.Cost
				};

			var defenders = calculator.CalculateCrossover(
				calculator.CalculateBaseline(actor.Info.Name, opponent.Info.Name));
			return new RankedMatchup
			{
				Opponent = opponent,
				Description = opponent.Name + " (/" + defenders.UnitCount + ")",
				EconomicRatio = opponent.Cost / (double)(defenders.UnitCount * actor.Cost)
			};
		}

		static bool CanEngage(GeneralizedCombatThreatCalculator.DirectionalThreat threat)
		{
			return threat.CanTarget && (threat.ContactAttack || threat.RangeCells > 0);
		}

		static string Label(IEnumerable<string> values)
		{
			var array = values.ToArray();
			return array.Length == 0 ? "None" : string.Join(", ", array);
		}

		static string Wrap(string text, int width = 80)
		{
			var lines = new List<string>();
			var line = "";
			foreach (var word in text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
			{
				if (line.Length == 0)
					line = word;
				else if (line.Length + word.Length + 1 <= width)
					line += " " + word;
				else
				{
					lines.Add(line);
					line = word;
				}
			}

			if (line.Length > 0)
				lines.Add(line);

			return string.Join("\n", lines);
		}

		static bool IsNormallyPurchasable(ActorInfo actor)
		{
			return actor.TraitInfoOrDefault<BuildableInfo>() != null &&
				(actor.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 0) > 0 &&
				actor.HasTraitInfo<IHealthInfo>() && actor.HasTraitInfo<ITargetableInfo>();
		}

		static string ArmorClass(ActorInfo actor)
		{
			var armor = actor.TraitInfos<ArmorInfo>().FirstOrDefault(t => t.EnabledByDefault)?.Type;
			switch (armor)
			{
				case "None": return "Infantry";
				case "Wood": return "Buildings";
				case "Tiberium":
				case "TiberiumWood": return "Economy";
				case "Light": return "Light Armor";
				case "Heavy": return "Heavy Armor";
				default: return null;
			}
		}
	}
}
