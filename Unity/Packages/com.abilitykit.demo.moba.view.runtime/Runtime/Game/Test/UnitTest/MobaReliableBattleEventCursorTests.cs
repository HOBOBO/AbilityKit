using System.Collections.Generic;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Protocol.Room;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class MobaReliableBattleEventCursorTests
    {
        [Test]
        public void ContiguousBatch_CommitsDeliveryThenAcknowledgement()
        {
            var cursor = new MobaReliableBattleEventCursor("battle-1");
            var push = Push("epoch-1", Event(1), Event(2));

            var result = cursor.Admit(in push);

            Assert.That(result.Status, Is.EqualTo(MobaReliableBattleEventBatchStatus.Accepted));
            Assert.That(result.Events, Has.Length.EqualTo(2));
            Assert.That(result.CommitSequence, Is.EqualTo(2));
            Assert.That(cursor.LastDeliveredSequence, Is.Zero);
            Assert.That(cursor.LastAcknowledgedSequence, Is.Zero);

            Assert.That(cursor.CommitDelivered(result.Epoch, result.CommitSequence), Is.True);
            Assert.That(cursor.ConfirmAcknowledged(result.Epoch, 2), Is.True);
            Assert.That(cursor.Epoch, Is.EqualTo("epoch-1"));
            Assert.That(cursor.LastDeliveredSequence, Is.EqualTo(2));
            Assert.That(cursor.LastAcknowledgedSequence, Is.EqualTo(2));
        }

        [Test]
        public void Replay_FiltersDeliveredEventsAndReturnsOnlyContiguousTail()
        {
            var cursor = DeliveredThrough(2);
            var replay = Push("epoch-1", Event(1), Event(2), Event(3), Event(4));

            var result = cursor.Admit(in replay);

            Assert.That(result.Status, Is.EqualTo(MobaReliableBattleEventBatchStatus.Accepted));
            Assert.That(result.Events, Has.Length.EqualTo(2));
            Assert.That(result.Events[0].Sequence, Is.EqualTo(3));
            Assert.That(result.Events[1].Sequence, Is.EqualTo(4));
            Assert.That(result.CommitSequence, Is.EqualTo(4));
        }

        [Test]
        public void DuplicateOnlyReplay_DoesNotAdvanceCursor()
        {
            var cursor = DeliveredThrough(2);
            var replay = Push("epoch-1", Event(1), Event(2));

            var result = cursor.Admit(in replay);

            Assert.That(result.Status, Is.EqualTo(MobaReliableBattleEventBatchStatus.DuplicateOnly));
            Assert.That(result.Events, Is.Empty);
            Assert.That(cursor.LastDeliveredSequence, Is.EqualTo(2));
        }

        [Test]
        public void SequenceGap_RequiresFullResyncAndDoesNotAdvance()
        {
            var cursor = DeliveredThrough(1);
            var push = Push("epoch-1", Event(3));

            var result = cursor.Admit(in push);

            Assert.That(result.Status, Is.EqualTo(MobaReliableBattleEventBatchStatus.SequenceGap));
            Assert.That(result.ShouldRequestFullResync, Is.True);
            Assert.That(result.ExpectedSequence, Is.EqualTo(2));
            Assert.That(result.ReceivedSequence, Is.EqualTo(3));
            Assert.That(cursor.LastDeliveredSequence, Is.EqualTo(1));
        }

        [Test]
        public void RetentionGap_RequiresFullResync()
        {
            var cursor = DeliveredThrough(2);
            var push = Push("epoch-1", Event(5));
            push.RetentionGap = true;
            push.FirstAvailableSequence = 5;

            var result = cursor.Admit(in push);

            Assert.That(result.Status, Is.EqualTo(MobaReliableBattleEventBatchStatus.RetentionGap));
            Assert.That(result.ShouldRequestFullResync, Is.True);
            Assert.That(result.ReceivedSequence, Is.EqualTo(5));
        }

        [Test]
        public void EpochChange_RequiresFullResyncAndPreservesAcknowledgedCursor()
        {
            var cursor = DeliveredThrough(2);
            Assert.That(cursor.ConfirmAcknowledged("epoch-1", 2), Is.True);
            var push = Push("epoch-2", Event(1, "epoch-2"));

            var result = cursor.Admit(in push);

            Assert.That(result.Status, Is.EqualTo(MobaReliableBattleEventBatchStatus.EpochChanged));
            Assert.That(cursor.Epoch, Is.EqualTo("epoch-1"));
            Assert.That(cursor.LastAcknowledgedSequence, Is.EqualTo(2));
        }

        [Test]
        public void OutOfOrderAckResponses_AreIdempotent()
        {
            var cursor = DeliveredThrough(3);

            Assert.That(cursor.ConfirmAcknowledged("epoch-1", 3), Is.True);
            Assert.That(cursor.ConfirmAcknowledged("epoch-1", 1), Is.True);
            Assert.That(cursor.LastAcknowledgedSequence, Is.EqualTo(3));
        }

        [Test]
        public void AdoptAuthoritativeBaseline_NewEpoch_ReplacesDeliveryAndClearsAck()
        {
            var cursor = DeliveredThrough(3);
            Assert.That(cursor.ConfirmAcknowledged("epoch-1", 3), Is.True);

            Assert.That(cursor.AdoptAuthoritativeBaseline("epoch-2", 8), Is.True);

            Assert.That(cursor.Epoch, Is.EqualTo("epoch-2"));
            Assert.That(cursor.LastDeliveredSequence, Is.EqualTo(8));
            Assert.That(cursor.LastAcknowledgedSequence, Is.Zero);
        }

        [Test]
        public void AdoptAuthoritativeBaseline_SameEpoch_PreservesAckWithinWatermark()
        {
            var cursor = DeliveredThrough(3);
            Assert.That(cursor.ConfirmAcknowledged("epoch-1", 3), Is.True);

            Assert.That(cursor.AdoptAuthoritativeBaseline("epoch-1", 5), Is.True);

            Assert.That(cursor.LastDeliveredSequence, Is.EqualTo(5));
            Assert.That(cursor.LastAcknowledgedSequence, Is.EqualTo(3));
        }

        [Test]
        public void AdoptAuthoritativeBaseline_LowerSameEpochWatermark_ClampsAck()
        {
            var cursor = DeliveredThrough(3);
            Assert.That(cursor.ConfirmAcknowledged("epoch-1", 3), Is.True);

            Assert.That(cursor.AdoptAuthoritativeBaseline("epoch-1", 2), Is.True);

            Assert.That(cursor.LastDeliveredSequence, Is.EqualTo(2));
            Assert.That(cursor.LastAcknowledgedSequence, Is.EqualTo(2));
        }

        [Test]
        public void ReplayAfterAuthoritativeBaseline_FiltersCoveredEventsAndReturnsTail()
        {
            var cursor = new MobaReliableBattleEventCursor("battle-1");
            Assert.That(cursor.AdoptAuthoritativeBaseline("epoch-2", 5), Is.True);
            var replay = Push(
                "epoch-2",
                Event(4, "epoch-2"),
                Event(5, "epoch-2"),
                Event(6, "epoch-2"),
                Event(7, "epoch-2"));

            var result = cursor.Admit(in replay);

            Assert.That(result.Status, Is.EqualTo(MobaReliableBattleEventBatchStatus.Accepted));
            Assert.That(result.Events, Has.Length.EqualTo(2));
            Assert.That(result.Events[0].Sequence, Is.EqualTo(6));
            Assert.That(result.Events[1].Sequence, Is.EqualTo(7));
            Assert.That(result.CommitSequence, Is.EqualTo(7));
        }

        [TestCase(null, 0)]
        [TestCase("", 0)]
        [TestCase("epoch-1", -1)]
        public void AdoptAuthoritativeBaseline_InvalidBaseline_IsRejected(
            string epoch,
            long watermark)
        {
            var cursor = DeliveredThrough(2);

            Assert.That(cursor.AdoptAuthoritativeBaseline(epoch, watermark), Is.False);
            Assert.That(cursor.Epoch, Is.EqualTo("epoch-1"));
            Assert.That(cursor.LastDeliveredSequence, Is.EqualTo(2));
        }

        [Test]
        public void Reset_ClearsReconnectSubscriptionCursor()
        {
            var cursor = DeliveredThrough(2);
            Assert.That(cursor.ConfirmAcknowledged("epoch-1", 2), Is.True);

            cursor.Reset();

            Assert.That(cursor.Epoch, Is.Empty);
            Assert.That(cursor.LastDeliveredSequence, Is.Zero);
            Assert.That(cursor.LastAcknowledgedSequence, Is.Zero);
        }

        private static MobaReliableBattleEventCursor DeliveredThrough(long sequence)
        {
            var cursor = new MobaReliableBattleEventCursor("battle-1");
            var events = new WireReliableBattleEvent[(int)sequence];
            for (var i = 0; i < events.Length; i++)
            {
                events[i] = Event(i + 1);
            }

            var push = Push("epoch-1", events);
            var result = cursor.Admit(in push);
            Assert.That(result.Accepted, Is.True);
            Assert.That(cursor.CommitDelivered(result.Epoch, result.CommitSequence), Is.True);
            return cursor;
        }

        private static WireReliableBattleEventPush Push(
            string epoch,
            params WireReliableBattleEvent[] events)
        {
            return new WireReliableBattleEventPush
            {
                BattleId = "battle-1",
                Epoch = epoch,
                FirstAvailableSequence = events.Length > 0 ? events[0].Sequence : 0,
                Watermark = events.Length > 0 ? events[events.Length - 1].Sequence : 0,
                Events = new List<WireReliableBattleEvent>(events)
            };
        }

        private static WireReliableBattleEvent Event(
            long sequence,
            string epoch = "epoch-1")
        {
            return new WireReliableBattleEvent
            {
                EventId = $"event-{sequence}",
                BattleId = "battle-1",
                Epoch = epoch,
                Sequence = sequence,
                SourceFrame = (int)sequence,
                EventType = 1
            };
        }
    }
}
