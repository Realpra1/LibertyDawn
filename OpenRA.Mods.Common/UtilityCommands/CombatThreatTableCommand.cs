#region Copyright & License Information
/* Copyright 2007-2021 The OpenRA Developers (see AUTHORS) */
#endregion

using System;
using System.Globalization;
using System.Linq;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Mods.Common.UtilityCommands
{
	public sealed class CombatThreatTableCommand : IUtilityCommand
	{
		string IUtilityCommand.Name => "--combat-threat-table";

		bool IUtilityCommand.ValidateArguments(string[] args) => args.Length >= 1 && args.Length <= 3;

		[Desc("[ATTACKER [DEFENDER]]", "Print the complete cached generalized combat-threat table, optionally filtered by actor type.")]
		void IUtilityCommand.Run(Utility utility, string[] args)
		{
			Game.ModData = utility.ModData;
			var calculator = new GeneralizedCombatThreatCalculator(utility.ModData.DefaultRules,
				utility.ModData.Manifest.Get<MapGrid>().SubCellOffsets);
			var attacker = args.Length > 1 ? args[1].ToLowerInvariant() : null;
			var defender = args.Length > 2 ? args[2].ToLowerInvariant() : null;

			Console.WriteLine("# EXPECTED_HIT_CHANCE = min(1, max(defender-hit-radius, splash-radius) / inaccuracy)");
			Console.WriteLine("# SPLASH_FACTOR = max(1, sum(area-weighted integral of linearly interpolated damage over each non-overlapping radius^2 annulus))");
			Console.WriteLine("# INFANTRY_SPLASH_FACTOR adds a 50% expected same-cell target at actual subcell falloff and treats other affected cells as 1.5 targets");
			Console.WriteLine("# SPLASH_AND_INACCURACY_MULTIPLIER = EXPECTED_HIT_CHANCE * SPLASH_FACTOR");
			Console.WriteLine("# DAMAGE_CYCLE caps each shot's combined direct damage at defender HP before adding uncapped splash damage");
			Console.WriteLine("# CONTACT_ATTACK models valid capture, sabotage, and demolition at zero weapon range; approach delay accounts for exposure inside a defender's valid range");
			Console.WriteLine("# FULL_AMMO_TICKS = first relevant pool capacity / max(0, full consumption rate - reload rate)");
			Console.WriteLine("# RELOADING_DAMAGE_TICK = full damage rate scaled by sustained reload / consumption throughput");
			Console.WriteLine("attacker\tdefender\tcan_target\tcontact_attack\tinstant_defeat\tsingle_use\thp\tarmor\teffective_range_cells\tnominal_range_cells\tminimum_range_cells\tprojectile_speed_cells_tick\ttarget_speed_cells_tick\thit_radius_cells\tinaccuracy_cells\texpected_hit_chance\tsplash_factor\tsplash_inaccuracy_multiplier\tdamage_cycle\tcycle_ticks\tdamage_tick\tfull_ammo_ticks\treloading_damage_tick\tdefender_healing_tick\tengagement_delay_ticks\ttime_to_kill_ticks\trange_multiplier\teffective_damage_tick\tkill_rate\tdefender_threat_attacker_equivalents\tsplash_zones");

			var rows = calculator.OrderedPairs().Select(p => p.Forward)
				.Where(d => attacker == null || d.Attacker == attacker)
				.Where(d => defender == null || d.Defender == defender)
				.OrderBy(d => d.Attacker, StringComparer.Ordinal).ThenBy(d => d.Defender, StringComparer.Ordinal);
			var count = 0;
			foreach (var d in rows)
			{
				calculator.TryGet(d.Attacker, d.Defender, out var pair);
				var zones = string.Join(";", d.SplashZones.Select(z => F(z.InnerRadiusCells) + "-" + F(z.OuterRadiusCells) + "@" +
					F(z.InnerDamageFraction) + "-" + F(z.OuterDamageFraction) + ":" + F(z.WeightedAffectedCells)));
				Console.WriteLine(string.Join("\t", d.Attacker, d.Defender, d.CanTarget, d.ContactAttack,
					d.InstantDefeat, d.SingleUse,
					d.DefenderHitPoints, d.DefenderArmor,
					F(d.RangeCells), F(d.NominalRangeCells), F(d.MinimumRangeCells), F(d.ProjectileSpeedCellsPerTick),
					F(d.TargetSpeedCellsPerTick), F(d.DefenderHitRadiusCells), F(d.InaccuracyCells), F(d.ExpectedHitChance), F(d.SplashFactor),
					F(d.SplashAndInaccuracyMultiplier), F(d.DamagePerCycle), F(d.CycleTicks), F(d.DamagePerTick),
					F(d.FullAmmoTicks), F(d.ReloadingDamagePerTick), F(d.DefenderHealingPerTick), F(d.EngagementDelayTicks), F(d.TimeToKillTicks),
					F(d.RangeMultiplier), F(d.EffectiveDamagePerTick), F(d.KillRate),
					F(pair.DefenderThreatInAttackerEquivalents), zones));
				count++;
			}

			Console.Error.WriteLine($"combat-threat directional-rows={count} calculated-unordered-pairs={calculator.Cache.Count}");
		}

		static string F(double value) => double.IsPositiveInfinity(value) ? "inf" : value.ToString("0.######", CultureInfo.InvariantCulture);
	}
}
