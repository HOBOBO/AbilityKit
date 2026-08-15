using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Flow;
using AbilityKit.Protocol.Room;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class ReliableBattleEventDeliveryRuntimeTests
    {
        [Test]
        public void SinkFailure_DoesNotCommitOrAcknowledge()
        {
            var cursor = new MobaReliableBattleEventCursor("battle-1");
            var transport = new ControllableTransport();
            var runtime = CreateRuntime();
            runtime.BeginGeneration(
                cursor,
                transport,
                null,
                _ => throw new InvalidOperationException("sink failed"),
                _ => { });
            Assert.That(runtime.AdoptAuthoritativeBaseline("epoch-1", 0), Is.True);
            var push = Push(Event(1));

            runtime.Handle(in push);

            Assert.That(cursor.LastDeliveredSequence, Is.Zero);
            Assert.That(cursor.LastAcknowledgedSequence, Is.Zero);
            Assert.That(transport.Acknowledgements, Is.Empty);
        }

        [Test]
        public void AckFailure_RetriesAndPersistsOnlyAfterSuccess()
        {
            var cursor = new MobaReliableBattleEventCursor("battle-1");
            var transport = new ControllableTransport();
            transport.EnqueueAckFailure(new InvalidOperationException("first"));
            transport.EnqueueAckFailure(new InvalidOperationException("second"));
            transport.EnqueueAckResult(1);
            var store = new TrackingCheckpointStore();
            var runtime = CreateRuntime();
            runtime.BeginGeneration(cursor, transport, store, _ => { }, _ => { });
            Assert.That(runtime.AdoptAuthoritativeBaseline("epoch-1", 0), Is.True);
            var push = Push(Event(1));

            runtime.Handle(in push);
            WaitUntilAsync(() => cursor.LastAcknowledgedSequence == 1)
                .GetAwaiter()
                .GetResult();

            Assert.That(transport.Acknowledgements, Has.Count.EqualTo(3));
            Assert.That(store.Saved, Has.Count.EqualTo(2));
            Assert.That(store.Saved[0].LastAcknowledgedSequence, Is.Zero);
            Assert.That(store.Saved[1].LastAcknowledgedSequence, Is.EqualTo(1));
        }

        [Test]
        public void ReplacedGeneration_StaleAckCannotMutateCursorOrCheckpoint()
        {
            var staleCursor = new MobaReliableBattleEventCursor("battle-1");
            var staleTransport = new ControllableTransport();
            var staleAck = staleTransport.EnqueuePendingAck();
            var staleStore = new TrackingCheckpointStore();
            var runtime = CreateRuntime();
            runtime.BeginGeneration(staleCursor, staleTransport, staleStore, _ => { }, _ => { });
            Assert.That(runtime.AdoptAuthoritativeBaseline("epoch-1", 0), Is.True);
            var push = Push(Event(1));
            runtime.Handle(in push);
            WaitUntilAsync(() => staleTransport.Acknowledgements.Count == 1)
                .GetAwaiter()
                .GetResult();
            var savesBeforeCompletion = staleStore.Saved.Count;

            var activeCursor = new MobaReliableBattleEventCursor("battle-1");
            runtime.BeginGeneration(
                activeCursor,
                new ControllableTransport(),
                new TrackingCheckpointStore(),
                _ => { },
                _ => { });
            staleAck.SetResult(1);
            Task.Delay(50).GetAwaiter().GetResult();

            Assert.That(staleCursor.LastAcknowledgedSequence, Is.Zero);
            Assert.That(staleStore.Saved, Has.Count.EqualTo(savesBeforeCompletion));
            Assert.That(activeCursor.LastAcknowledgedSequence, Is.Zero);
        }

        [Test]
        public void PendingBatches_AreDeliveredInOrderAfterAuthoritativeBaseline()
        {
            var delivered = new List<long>();
            var cursor = new MobaReliableBattleEventCursor("battle-1");
            var transport = new ControllableTransport();
            transport.EnqueueAckResult(7);
            var runtime = CreateRuntime();
            runtime.BeginGeneration(cursor, transport, null, value => delivered.Add(value.Sequence), _ => { });
            var first = Push(Event(6));
            var second = Push(Event(7));

            runtime.Handle(in first);
            runtime.Handle(in second);
            Assert.That(delivered, Is.Empty);

            Assert.That(runtime.AdoptAuthoritativeBaseline("epoch-1", 5), Is.True);

            Assert.That(delivered, Is.EqualTo(new long[] { 6, 7 }));
            Assert.That(cursor.LastDeliveredSequence, Is.EqualTo(7));
            Assert.That(runtime.PendingBatchCount, Is.Zero);
        }

        [Test]
        public void Dispose_IsIdempotentAndRejectsLateDelivery()
        {
            var delivered = 0;
            var runtime = CreateRuntime();
            runtime.BeginGeneration(
                new MobaReliableBattleEventCursor("battle-1"),
                new ControllableTransport(),
                null,
                _ => delivered++,
                _ => { });
            runtime.Dispose();
            runtime.Dispose();
            var push = Push(Event(1));

            runtime.Handle(in push);

            Assert.That(delivered, Is.Zero);
            Assert.That(runtime.PendingBatchCount, Is.Zero);
        }

        private static ReliableBattleEventDeliveryRuntime CreateRuntime()
        {
            return new ReliableBattleEventDeliveryRuntime(_ => Task.CompletedTask);
        }

        private static WireReliableBattleEventPush Push(params WireReliableBattleEvent[] events)
        {
            return new WireReliableBattleEventPush
            {
                BattleId = "battle-1",
                Epoch = "epoch-1",
                FirstAvailableSequence = events.Length > 0 ? events[0].Sequence : 0,
                Watermark = events.Length > 0 ? events[events.Length - 1].Sequence : 0,
                Events = new List<WireReliableBattleEvent>(events)
            };
        }

        private static WireReliableBattleEvent Event(long sequence)
        {
            return new WireReliableBattleEvent
            {
                EventId = $"event-{sequence}",
                BattleId = "battle-1",
                Epoch = "epoch-1",
                Sequence = sequence,
                SourceFrame = (int)sequence,
                EventType = 1
            };
        }

        private static async Task WaitUntilAsync(Func<bool> predicate)
        {
            for (var i = 0; i < 100; i++)
            {
                if (predicate()) return;
                await Task.Delay(10);
            }

            Assert.Fail("Condition was not satisfied before timeout.");
        }

        private sealed class TrackingCheckpointStore : IMobaReliableBattleEventCheckpointStore
        {
            internal List<MobaReliableBattleEventCheckpoint> Saved { get; } =
                new List<MobaReliableBattleEventCheckpoint>();

            public bool TryLoad(
                string battleId,
                out MobaReliableBattleEventCheckpoint checkpoint)
            {
                checkpoint = default;
                return false;
            }

            public void Save(in MobaReliableBattleEventCheckpoint checkpoint)
            {
                Saved.Add(checkpoint);
            }
        }

        private sealed class ControllableTransport : IBattleRecoveryTransportOperations
        {
            private readonly Queue<Func<Task<long>>> _acknowledgements =
                new Queue<Func<Task<long>>>();

            internal List<(string Epoch, long Sequence)> Acknowledgements { get; } =
                new List<(string Epoch, long Sequence)>();

            internal void EnqueueAckResult(long sequence)
            {
                _acknowledgements.Enqueue(() => Task.FromResult(sequence));
            }

            internal void EnqueueAckFailure(Exception exception)
            {
                _acknowledgements.Enqueue(() => Task.FromException<long>(exception));
            }

            internal TaskCompletionSource<long> EnqueuePendingAck()
            {
                var completion = new TaskCompletionSource<long>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _acknowledgements.Enqueue(() => completion.Task);
                return completion;
            }

            public Task<long> AcknowledgeReliableEventsAsync(string epoch, long sequence)
            {
                Acknowledgements.Add((epoch, sequence));
                return _acknowledgements.Count > 0
                    ? _acknowledgements.Dequeue().Invoke()
                    : Task.FromResult(sequence);
            }

            public Task<bool> RequestFullStateSyncAsync(
                string reason,
                int lastAuthoritativeFrame)
            {
                return Task.FromResult(true);
            }

            public void Disconnect()
            {
            }
        }
    }
}
