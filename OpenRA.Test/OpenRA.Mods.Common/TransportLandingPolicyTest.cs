#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is made available under the GNU General Public License
 * version 3 or later. See COPYING for details.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class TransportLandingPolicyTest
	{
		[Test]
		public void CandidateOrderIsStableAcrossInputEnumerationOrder()
		{
			var objective = new CPos(10, 10);
			var cells = new[]
			{
				new CPos(11, 10), new CPos(10, 9), new CPos(9, 10), new CPos(10, 11),
				new CPos(10, 10), new CPos(11, 10),
			};
			var expected = new[]
			{
				new CPos(10, 10), new CPos(9, 10), new CPos(10, 9), new CPos(10, 11), new CPos(11, 10),
			};

			Assert.That(TransportLandingPolicy.OrderedCandidates(cells, objective), Is.EqualTo(expected));
			Assert.That(TransportLandingPolicy.OrderedCandidates(cells.Reverse(), objective), Is.EqualTo(expected));
		}

		[Test]
		public void PositiveDamageRequiresPositiveApplicableArmorModifier()
		{
			var versus = new Dictionary<string, int> { { "Light", 0 }, { "Heavy", 50 } };

			Assert.That(TransportLandingPolicy.DealsPositiveDamage(100, versus, new[] { "Light" }), Is.False);
			Assert.That(TransportLandingPolicy.DealsPositiveDamage(100, versus, new[] { "Heavy" }), Is.True);
			Assert.That(TransportLandingPolicy.DealsPositiveDamage(0, versus, new[] { "Heavy" }), Is.False);
			Assert.That(TransportLandingPolicy.DealsPositiveDamage(100, versus, new[] { "Wood" }), Is.True);
		}

		[Test]
		public void ExactPassengerExitsRoundTripInStableActorOrder()
		{
			var exits = new[]
			{
				new KeyValuePair<uint, CPos>(9, new CPos(4, 5)),
				new KeyValuePair<uint, CPos>(2, new CPos(-3, 7, 1)),
			};
			var encoded = TransportLandingPolicy.EncodeExactExits(exits);

			Assert.That(encoded, Is.EqualTo($"2:{new CPos(-3, 7, 1).Bits};9:{new CPos(4, 5).Bits}"));
			Assert.That(TransportLandingPolicy.TryDecodeExactExits(encoded, out var decoded), Is.True);
			Assert.That(decoded[2], Is.EqualTo(new CPos(-3, 7, 1)));
			Assert.That(decoded[9], Is.EqualTo(new CPos(4, 5)));
			Assert.That(TransportLandingPolicy.TryDecodeExactExits("2:3;2:4", out _), Is.False);
			Assert.That(TransportLandingPolicy.TryDecodeExactExits("invalid", out _), Is.False);
		}
	}
}
