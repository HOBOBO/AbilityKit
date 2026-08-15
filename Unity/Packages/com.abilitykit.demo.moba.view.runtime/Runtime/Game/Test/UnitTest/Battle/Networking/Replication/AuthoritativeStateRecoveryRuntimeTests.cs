using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Flow;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Battle;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Network.Sdk;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class AuthoritativeStateRecoveryRuntimeTests
    {
        [Test]
        public void FullBaseline_ImportsWorldAndEnablesGameplayInput()
        {
            using var fixture = new Fixture();
            var firstFrames = 0;
            fixture.BeginGeneration(firstFrameReceived: () => firstFrames++);
            var snapshot = FullSnapshot(frame: 10, eventWatermark: 4);

            fixture.Runtime.HandleSnapshot(snapshot);

            Assert.That(fixture.World.ImportedFrames, Is.EqualTo(new[] { 10 }));
            Assert.That(fixture.Runtime.PendingStateImport, Is.False);
            Assert.That(fixture.Context.CanSubmitGameplayInput, Is.True);
            Assert.That(firstFrames, Is.EqualTo(1));
        }

        [Test]
        public void ImportFailure_KeepsInputBlockedAndRequestsFullState()
        {
            using var fixture = new Fixture();
            fixture.World.ImportSucceeds = false;
            fixture.BeginGeneration();

            fixture.Runtime.HandleSnapshot(FullSnapshot(frame: 12));

            Assert.That(fixture.Transport.FullStateRequests, Has.Count.EqualTo(1));
            Assert.That(fixture.Runtime.PendingStateImport, Is.True);
            Assert.That(fixture.Context.CanSubmitGameplayInput, Is.False);
            Assert.That(fixture.Transport.FullStateRequests[0].Reason, Is.EqualTo("state-import-failed"));
            Assert.That(fixture.Transport.FullStateRequests[0].Frame, Is.EqualTo(12));
        }

        [Test]
        public void IncrementalSnapshot_AppliesRemovedActorsAfterBaseline()
        {
            using var fixture = new Fixture();
            fixture.BeginGeneration();
            fixture.Runtime.HandleSnapshot(FullSnapshot(frame: 20));
            var incremental = new GatewayStateSyncSnapshot(
                1UL,
                21,
                0d,
                false,
                Array.Empty<GatewayStateSyncActorSnapshot>(),
                GatewayStateSyncSnapshot.CurrentSchemaVersion,
                new[] { 7, 9 },
                0L,
                "epoch-1");

            fixture.Runtime.HandleSnapshot(incremental);

            Assert.That(fixture.World.RemovedActorBatches, Has.Count.EqualTo(1));
            Assert.That(fixture.World.RemovedActorBatches[0], Is.EqualTo(new[] { 7, 9 }));
        }

        [Test]
        public void Reconnect_ResetInvalidatesTimelineAndClearsInputAck()
        {
            using var fixture = new Fixture();
            fixture.BeginGeneration();
            fixture.Runtime.HandleSnapshot(FullSnapshot(frame: 30));
            fixture.Replication.LastServerAckFrame = 29;

            fixture.Runtime.HandleConnectionClosed();
            fixture.Runtime.HandleConnectionEstablished();

            Assert.That(fixture.Transport.FullStateRequests, Has.Count.EqualTo(1));
            Assert.That(fixture.World.ResetCount, Is.EqualTo(1));
            Assert.That(fixture.Runtime.PendingStateImport, Is.True);
            Assert.That(fixture.Context.CanSubmitGameplayInput, Is.False);
            Assert.That(fixture.Replication.LastServerAckFrame, Is.Zero);
            Assert.That(
                fixture.Transport.FullStateRequests[0].Reason,
                Is.EqualTo("connection-re-established"));
            Assert.That(
                fixture.Runtime.RecoveryDecision.Action,
                Is.EqualTo(NetworkSessionRecoveryAction.RequestFullSnapshot));
        }

        [Test]
        public void FrameworkRecoverySignal_RoutesToMobaFullStateRequest()
        {
            using var fixture = new Fixture();
            fixture.BeginGeneration();
            var signal = new NetworkSessionRecoverySignal(
                NetworkSessionRecoverySignalKind.SnapshotResyncRequired,
                SyncHealthSeverity.Error,
                frame: 18,
                detail: "framework-requested-resync");

            Assert.That(
                fixture.Runtime.TryReport(in signal, out var decision),
                Is.True);
            var execution = fixture.Runtime.ExecuteRecoveryDecisionAsync(decision)
                .GetAwaiter()
                .GetResult();

            Assert.That(execution.Status, Is.EqualTo(NetworkSessionRecoveryExecutionStatus.Executed));
            Assert.That(execution.HasValue, Is.True);
            Assert.That(execution.Value, Is.True);
            Assert.That(fixture.Transport.FullStateRequests, Has.Count.EqualTo(1));
            Assert.That(
                fixture.Transport.FullStateRequests[0],
                Is.EqualTo(("framework-requested-resync", 18)));
        }

        [Test]
        public void ReplacedGeneration_StaleFrameworkDecisionCannotMutateActiveGeneration()
        {
            using var fixture = new Fixture();
            fixture.BeginGeneration();
            fixture.Runtime.HandleConnectionClosed();
            var correlationContext =
                fixture.Runtime.RecoveryDecision.Signal.CorrelationContext;
            var signal = new NetworkSessionRecoverySignal(
                NetworkSessionRecoverySignalKind.SnapshotResyncRequired,
                SyncHealthSeverity.Error,
                frame: 22,
                correlationContext: correlationContext,
                detail: "stale-resync");
            Assert.That(
                fixture.Runtime.TryReport(in signal, out var staleDecision),
                Is.True);

            var activeTransport = new ControllableTransport();
            fixture.BeginGeneration(activeTransport);
            var execution = fixture.Runtime.ExecuteRecoveryDecisionAsync(staleDecision)
                .GetAwaiter()
                .GetResult();

            Assert.That(execution.Status, Is.EqualTo(NetworkSessionRecoveryExecutionStatus.Executed));
            Assert.That(execution.Value, Is.False);
            Assert.That(activeTransport.FullStateRequests, Is.Empty);
            Assert.That(fixture.Runtime.PendingStateImport, Is.True);
        }

        [Test]
        public void GenerationLifecycle_OwnsRemoteInterpolationFlag()
        {
            using var fixture = new Fixture();

            fixture.BeginGeneration();

            Assert.That(fixture.Context.EnableRemoteInterpolation, Is.True);

            fixture.Runtime.Dispose();

            Assert.That(fixture.Context.EnableRemoteInterpolation, Is.False);
        }

        [Test]
        public void BeginGenerationFailure_RollsBackRecoveryOwnershipAndKeepsInputBlocked()
        {
            using var fixture = new Fixture();
            fixture.World.ThrowOnConfigure = true;

            var exception = Assert.Throws<InvalidOperationException>(() => fixture.BeginGeneration());

            Assert.That(exception.Message, Is.EqualTo("configure-failure"));
            Assert.That(fixture.Runtime.PendingStateImport, Is.True);
            Assert.That(fixture.Context.CanSubmitGameplayInput, Is.False);
            Assert.That(fixture.Context.EnableRemoteInterpolation, Is.False);
        }

        [Test]
        public void ReliableEventInitializationFailure_RollsBackAndAllowsRetry()
        {
            using var fixture = new Fixture();

            Assert.Throws<InvalidOperationException>(() =>
                fixture.BeginGeneration(checkpointStore: new ThrowingCheckpointStore()));

            Assert.That(fixture.Runtime.PendingStateImport, Is.True);
            Assert.That(fixture.Context.CanSubmitGameplayInput, Is.False);
            Assert.That(fixture.Context.EnableRemoteInterpolation, Is.False);

            fixture.BeginGeneration();

            Assert.That(fixture.Context.EnableRemoteInterpolation, Is.True);
        }

        [Test]
        public void ReplacedGeneration_StaleFullStateCompletionCannotAffectActiveGeneration()
        {
            using var fixture = new Fixture();
            var staleCompletion = fixture.Transport.EnqueuePendingFullStateRequest();
            fixture.World.ImportSucceeds = false;
            fixture.BeginGeneration();
            fixture.Runtime.HandleSnapshot(FullSnapshot(frame: 40));
            Assert.That(fixture.Transport.FullStateRequests, Has.Count.EqualTo(1));

            var activeTransport = new ControllableTransport();
            fixture.World.ImportSucceeds = true;
            fixture.BeginGeneration(activeTransport);
            staleCompletion.SetResult(false);
            Task.Delay(50).GetAwaiter().GetResult();

            LogAssert.NoUnexpectedReceived();
            Assert.That(fixture.Runtime.PendingStateImport, Is.True);
            Assert.That(fixture.Context.CanSubmitGameplayInput, Is.False);
            Assert.That(activeTransport.FullStateRequests, Is.Empty);
            Assert.That(fixture.World.ResetCount, Is.Zero);
        }

        private static GatewayStateSyncSnapshot FullSnapshot(
            int frame,
            long eventWatermark = 0L)
        {
            return new GatewayStateSyncSnapshot(
                1UL,
                frame,
                0d,
                true,
                Array.Empty<GatewayStateSyncActorSnapshot>(),
                GatewayStateSyncSnapshot.CurrentSchemaVersion,
                Array.Empty<int>(),
                eventWatermark,
                "epoch-1");
        }

        private sealed class Fixture : IDisposable
        {
            private readonly NetworkTransport _networkTransport;
            private readonly ReliableBattleEventDeliveryRuntime _reliableEvents;

            internal Fixture()
            {
                Context = BattleContext.Rent();
                Replication = new BattleReplicationRuntime();
                _networkTransport = new NetworkTransport(new NetworkTransportOptions
                {
                    ConnectionFactory = () => new StubConnection()
                });
                Replication.Build(
                    _networkTransport,
                    30,
                    1UL,
                    "battle-1",
                    default,
                    _ => { },
                    _ => { },
                    () => { },
                    () => { });
                _reliableEvents = new ReliableBattleEventDeliveryRuntime(_ => Task.CompletedTask);
                World = new TrackingWorldRecoveryPort();
                Transport = new ControllableTransport();
                Runtime = new AuthoritativeStateRecoveryRuntime(
                    Replication,
                    _reliableEvents,
                    World);
            }

            internal BattleContext Context { get; }
            internal BattleReplicationRuntime Replication { get; }
            internal TrackingWorldRecoveryPort World { get; }
            internal ControllableTransport Transport { get; }
            internal AuthoritativeStateRecoveryRuntime Runtime { get; }

            internal void BeginGeneration(
                ControllableTransport transport = null,
                Action firstFrameReceived = null,
                IMobaReliableBattleEventCheckpointStore checkpointStore = null)
            {
                var plan = default(BattleStartPlan);
                Runtime.BeginGeneration(
                    transport ?? Transport,
                    checkpointStore,
                    Context,
                    in plan,
                    1f / 30f,
                    _ => 0,
                    () => false,
                    _ => { },
                    firstFrameReceived);
            }

            public void Dispose()
            {
                Runtime.Dispose();
                Replication.Dispose();
                _networkTransport.Dispose();
                BattleContext.Return(Context);
            }
        }

        private sealed class TrackingWorldRecoveryPort : IBattleAuthoritativeWorldRecoveryPort
        {
            internal bool ImportSucceeds { get; set; } = true;
            internal bool ThrowOnConfigure { get; set; }
            internal List<int> ImportedFrames { get; } = new List<int>();
            internal List<int[]> RemovedActorBatches { get; } = new List<int[]>();
            internal int ResetCount { get; private set; }

            public void Configure(
                in BattleStartPlan plan,
                BattleContext context,
                float fixedDeltaSeconds,
                Func<WorldId, int> resolveIdealFrameLimit,
                Func<bool> shouldForceHashMismatch)
            {
                if (ThrowOnConfigure)
                {
                    throw new InvalidOperationException("configure-failure");
                }
            }

            public bool TryImport(in GatewayStateSyncSnapshot snapshot)
            {
                ImportedFrames.Add(snapshot.Frame);
                return ImportSucceeds;
            }

            public int ApplyRemovedActors(in GatewayStateSyncSnapshot snapshot)
            {
                var actorIds = snapshot.RemovedActorIds ?? Array.Empty<int>();
                RemovedActorBatches.Add((int[])actorIds.Clone());
                return actorIds.Length;
            }

            public void ResetAfterReconnect()
            {
                ResetCount++;
            }
        }

        private sealed class ThrowingCheckpointStore : IMobaReliableBattleEventCheckpointStore
        {
            public bool TryLoad(
                string battleId,
                out MobaReliableBattleEventCheckpoint checkpoint)
            {
                throw new InvalidOperationException("checkpoint-load-failure");
            }

            public void Save(in MobaReliableBattleEventCheckpoint checkpoint)
            {
            }
        }

        private sealed class ControllableTransport : IBattleRecoveryTransportOperations
        {
            private readonly Queue<Func<Task<bool>>> _fullStateRequests =
                new Queue<Func<Task<bool>>>();

            internal List<(string Reason, int Frame)> FullStateRequests { get; } =
                new List<(string Reason, int Frame)>();

            internal TaskCompletionSource<bool> EnqueuePendingFullStateRequest()
            {
                var completion = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _fullStateRequests.Enqueue(() => completion.Task);
                return completion;
            }

            public Task<long> AcknowledgeReliableEventsAsync(string epoch, long sequence)
            {
                return Task.FromResult(sequence);
            }

            public Task<bool> RequestFullStateSyncAsync(
                string reason,
                int lastAuthoritativeFrame)
            {
                FullStateRequests.Add((reason, lastAuthoritativeFrame));
                return _fullStateRequests.Count > 0
                    ? _fullStateRequests.Dequeue().Invoke()
                    : Task.FromResult(true);
            }

            public void Disconnect()
            {
            }
        }

        private sealed class StubConnection : IConnection
        {
            public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
            public bool IsConnected => State == ConnectionState.Connected;

            public event Action Connected;
            public event Action Disconnected;
            public event Action<Exception> Error;
            public event Action<uint, uint, ArraySegment<byte>> PacketReceived;
            public event Action<uint, ArraySegment<byte>> ServerPushReceived;
            public event Action<string, string> Kicked;

            public void Open(string host, int port)
            {
                State = ConnectionState.Connected;
                Connected?.Invoke();
            }

            public void Close()
            {
                State = ConnectionState.Disconnected;
                Disconnected?.Invoke();
            }

            public void Tick(float deltaTime)
            {
            }

            public void Send(
                uint opCode,
                ArraySegment<byte> payload,
                ushort flags = 0,
                uint seq = 0)
            {
            }

            public void Dispose()
            {
                Close();
            }
        }
    }
}
