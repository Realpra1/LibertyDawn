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

using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public class ExactCountTrackerTest
	{
		const int Target = 25;

		static ExactCountTracker CountUpTo(int count)
		{
			var tracker = new ExactCountTracker(Target);
			for (var i = 0; i < count; i++)
				tracker.Adjust(1);

			return tracker;
		}

		[TestCase(0)]
		[TestCase(1)]
		[TestCase(23)]
		[TestCase(24)]
		[TestCase(26)]
		[TestCase(27)]
		[TestCase(50)]
		public void NotSatisfiedAtAnyOtherCount(int count)
		{
			Assert.That(CountUpTo(count).IsSatisfied, Is.False, $"{count} must not satisfy an exact target of {Target}");
		}

		[Test]
		public void SatisfiedAtExactlyTheTarget()
		{
			Assert.That(CountUpTo(25).IsSatisfied, Is.True);
		}

		[Test]
		public void TwentyFourthActorFlipsItOn()
		{
			var tracker = CountUpTo(24);
			Assert.That(tracker.IsSatisfied, Is.False);
			Assert.That(tracker.Adjust(1), Is.True, "Reaching the target must report a change");
			Assert.That(tracker.IsSatisfied, Is.True);
		}

		[Test]
		public void TwentySixthActorFlipsItOff()
		{
			var tracker = CountUpTo(25);
			Assert.That(tracker.Adjust(1), Is.True, "Exceeding the target must report a change");
			Assert.That(tracker.Count, Is.EqualTo(26));
			Assert.That(tracker.IsSatisfied, Is.False);
		}

		[Test]
		public void LosingOneFromTheTargetFlipsItOff()
		{
			var tracker = CountUpTo(25);
			Assert.That(tracker.Adjust(-1), Is.True);
			Assert.That(tracker.Count, Is.EqualTo(24));
			Assert.That(tracker.IsSatisfied, Is.False);
		}

		[Test]
		public void DroppingFromTwentySixBackToTwentyFiveFlipsItOn()
		{
			var tracker = CountUpTo(26);
			Assert.That(tracker.Adjust(-1), Is.True);
			Assert.That(tracker.IsSatisfied, Is.True);
		}

		[Test]
		public void ItReactsRepeatedly()
		{
			var tracker = CountUpTo(25);
			for (var i = 0; i < 5; i++)
			{
				Assert.That(tracker.IsSatisfied, Is.True);
				Assert.That(tracker.Adjust(1), Is.True);
				Assert.That(tracker.IsSatisfied, Is.False);
				Assert.That(tracker.Adjust(-1), Is.True);
			}

			Assert.That(tracker.IsSatisfied, Is.True);
		}

		[Test]
		public void MovesThatDoNotCrossTheTargetReportNoChange()
		{
			var tracker = CountUpTo(10);
			Assert.That(tracker.Adjust(1), Is.False);
			Assert.That(tracker.Adjust(-1), Is.False);
			Assert.That(tracker.Adjust(5), Is.False);
			Assert.That(tracker.Count, Is.EqualTo(15));
		}

		[Test]
		public void JumpingStraightAcrossTheTargetNeverSatisfies()
		{
			var tracker = CountUpTo(24);
			Assert.That(tracker.Adjust(2), Is.False, "24 -> 26 never passes through a satisfied state");
			Assert.That(tracker.Count, Is.EqualTo(26));
			Assert.That(tracker.IsSatisfied, Is.False);
		}

		[Test]
		public void NoChangeForAZeroDelta()
		{
			var tracker = CountUpTo(25);
			Assert.That(tracker.Adjust(0), Is.False);
			Assert.That(tracker.IsSatisfied, Is.True);
		}

		[Test]
		public void SetReportsWhetherItFlipped()
		{
			var tracker = new ExactCountTracker(Target);
			Assert.That(tracker.Set(24), Is.False);
			Assert.That(tracker.Set(25), Is.True);
			Assert.That(tracker.Set(25), Is.False);
			Assert.That(tracker.Set(26), Is.True);
		}

		[Test]
		public void InitialCountIsHonoured()
		{
			Assert.That(new ExactCountTracker(Target, 25).IsSatisfied, Is.True);
			Assert.That(new ExactCountTracker(Target, 24).IsSatisfied, Is.False);
			Assert.That(new ExactCountTracker(Target, 26).IsSatisfied, Is.False);
		}
	}
}
