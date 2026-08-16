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

using System.IO;
using System.Runtime.Serialization;
using NUnit.Framework;
using OpenRA.Network;
using OpenRA.Server;
using OpenRA.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public class OrderTest
	{
		byte[] RoundTripOrder(byte[] bytes)
		{
			return Order.Deserialize(null, new BinaryReader(new MemoryStream(bytes))).Serialize();
		}

		[TestCase(TestName = "Order data persists over serialization (empty)")]
		public void SerializeEmpty()
		{
			var o = new Order().Serialize();
			Assert.That(RoundTripOrder(o), Is.EqualTo(o));
		}

		[TestCase(TestName = "Order data persists over serialization (unqueued)")]
		public void SerializeUnqueued()
		{
			var o = new Order("Test", null, false).Serialize();
			Assert.That(RoundTripOrder(o), Is.EqualTo(o));
		}

		[TestCase(TestName = "Order data persists over serialization (queued)")]
		public void SerializeQueued()
		{
			var o = new Order("Test", null, true).Serialize();
			Assert.That(RoundTripOrder(o), Is.EqualTo(o));
		}

		[TestCase(TestName = "Order data persists over serialization (pos target)")]
		public void SerializePos()
		{
			var o = new Order("Test", null, Target.FromPos(new WPos(int.MinValue, 0, int.MaxValue)), false).Serialize();
			Assert.That(RoundTripOrder(o), Is.EqualTo(o));
		}

		[TestCase(TestName = "Order data persists over serialization (invalid target)")]
		public void SerializeInvalid()
		{
			var o = new Order("Test", null, Target.Invalid, false).Serialize();
			Assert.That(RoundTripOrder(o), Is.EqualTo(o));
		}

		[TestCase(TestName = "Order data persists over serialization (extra fields)")]
		public void SerializeExtra()
		{
			var o = new Order("Test", null, Target.Invalid, true)
			{
				TargetString = "TargetString",
				ExtraLocation = new CPos(2047, 2047, 128),
				ExtraData = uint.MaxValue,
				IsImmediate = true,
			}.Serialize();
			Assert.That(RoundTripOrder(o), Is.EqualTo(o));
		}

		[TestCase(TestName = "Actor generation history resolves exact net-frame state")]
		public void ActorGenerationHistoryResolvesExactFrame()
		{
			var history = new ActorGenerationHistory();
			history.Record(5, 1);
			history.Record(9, 2);

			Assert.That(history.AtNetFrame(4), Is.EqualTo(0));
			Assert.That(history.AtNetFrame(5), Is.EqualTo(1));
			Assert.That(history.AtNetFrame(8), Is.EqualTo(1));
			Assert.That(history.AtNetFrame(9), Is.EqualTo(2));
		}

		[TestCase(TestName = "Legacy replay latency follows recorded server type")]
		public void LegacyReplayLatencyUsesLocalServerContract()
		{
			Assert.That(ReplayConnection.LegacyOrderLatency(false, 6), Is.EqualTo(1));
			Assert.That(ReplayConnection.LegacyOrderLatency(true, 6), Is.EqualTo(6));
		}

		[TestCase(TestName = "Actor-generation wire format bumps orders protocol")]
		public void ActorGenerationWireFormatHasProtocolVersion()
		{
			Assert.That(ProtocolVersion.Orders, Is.EqualTo(19));
			Assert.That(ProtocolVersion.ActorTargetGeneration, Is.EqualTo(19));
			Assert.That(ProtocolVersion.RecordedBotPolicy, Is.EqualTo(19));
			Assert.That(ProtocolVersion.HasActorTargetGeneration(18), Is.False);
			Assert.That(ProtocolVersion.HasActorTargetGeneration(19), Is.True);
			Assert.That(ProtocolVersion.HasRecordedBotPolicy(18), Is.False);
			Assert.That(ProtocolVersion.HasRecordedBotPolicy(19), Is.True);

			var actor = (Actor)FormatterServices.GetUninitializedObject(typeof(Actor));
			actor.Generation = 7;
			var bytes = new Order("Test", null, Target.FromActor(actor), false).Serialize();
			using (var reader = new BinaryReader(new MemoryStream(bytes)))
			{
				Assert.That((OrderType)reader.ReadByte(), Is.EqualTo(OrderType.Fields));
				Assert.That(reader.ReadString(), Is.EqualTo("Test"));
				var fields = (OrderFields)reader.ReadInt16();
				Assert.That(fields.HasField(OrderFields.TargetActorGeneration), Is.True);
				Assert.That((TargetType)reader.ReadByte(), Is.EqualTo(TargetType.Actor));
				Assert.That(reader.ReadUInt32(), Is.EqualTo(0));
				Assert.That(reader.ReadInt32(), Is.EqualTo(7));
			}
		}
	}
}
