using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Ability.Host;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Flow;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Battle;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Conditioning;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Demo.Common.Rooms;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class SessionDiagnosticsReplicationTests
    {
        [Test]
        public void Diagnostics_Dispose_ClearsOwnedPublicationsAndIsIdempotent()
        {
            var diagnostics = new BattleSessionDiagnostics(
                new BattleReplicationRuntime());
            var jitter = new JitterBufferStatsSnapshot { DelayFrames = 3 };
            var timeSync = new TimeSyncStatsSnapshot { Samples = 5 };
            var timeSyncByWorld = new Dictionary<string, TimeSyncStatsSnapshot>
            {
                ["world-a"] = timeSync,
            };

            diagnostics.PublishJitterBuffer(jitter);
            diagnostics.PublishTimeSync(timeSync, timeSyncByWorld);
            diagnostics.InitializeConfirmedAuthority("world-a");
            diagnostics.UpdateConfirmedAuthority(
                10,
                12,
                13,
                14,
                15,
                2,
                new[] { "spawn", "hit" });
            var authority = BattleFlowDebugProvider.ConfirmedAuthorityWorldStats;

            Assert.That(BattleFlowDebugProvider.JitterBufferStats, Is.SameAs(jitter));
            Assert.That(BattleFlowDebugProvider.TimeSyncStats, Is.SameAs(timeSync));
            Assert.That(BattleFlowDebugProvider.TimeSyncStatsByWorld, Is.SameAs(timeSyncByWorld));
            Assert.That(authority.WorldId, Is.EqualTo("world-a"));
            Assert.That(authority.ConfirmedFrame, Is.EqualTo(10));
            Assert.That(authority.RecentViewEvents, Is.EqualTo(new[] { "spawn", "hit" }));

            diagnostics.Dispose();
            diagnostics.Dispose();

            Assert.That(BattleFlowDebugProvider.JitterBufferStats, Is.Null);
            Assert.That(BattleFlowDebugProvider.TimeSyncStats, Is.Null);
            Assert.That(BattleFlowDebugProvider.TimeSyncStatsByWorld, Is.Null);
            Assert.That(BattleFlowDebugProvider.ConfirmedAuthorityWorldStats, Is.Null);
        }

        [Test]
        public void Diagnostics_StaleOwnerDispose_DoesNotClearReplacementPublications()
        {
            var stale = new BattleSessionDiagnostics(new BattleReplicationRuntime());
            var active = new BattleSessionDiagnostics(new BattleReplicationRuntime());
            var staleJitter = new JitterBufferStatsSnapshot { DelayFrames = 1 };
            var activeJitter = new JitterBufferStatsSnapshot { DelayFrames = 2 };
            var staleTimeSync = new TimeSyncStatsSnapshot { Samples = 1 };
            var activeTimeSync = new TimeSyncStatsSnapshot { Samples = 2 };
            var staleByWorld = new Dictionary<string, TimeSyncStatsSnapshot>
            {
                ["stale"] = staleTimeSync,
            };
            var activeByWorld = new Dictionary<string, TimeSyncStatsSnapshot>
            {
                ["active"] = activeTimeSync,
            };

            stale.PublishJitterBuffer(staleJitter);
            stale.PublishTimeSync(staleTimeSync, staleByWorld);
            stale.InitializeConfirmedAuthority("stale");
            active.PublishJitterBuffer(activeJitter);
            active.PublishTimeSync(activeTimeSync, activeByWorld);
            active.InitializeConfirmedAuthority("active");
            var activeAuthority = BattleFlowDebugProvider.ConfirmedAuthorityWorldStats;

            stale.Dispose();

            Assert.That(BattleFlowDebugProvider.JitterBufferStats, Is.SameAs(activeJitter));
            Assert.That(BattleFlowDebugProvider.TimeSyncStats, Is.SameAs(activeTimeSync));
            Assert.That(BattleFlowDebugProvider.TimeSyncStatsByWorld, Is.SameAs(activeByWorld));
            Assert.That(BattleFlowDebugProvider.ConfirmedAuthorityWorldStats, Is.SameAs(activeAuthority));

            active.Dispose();
        }

        [Test]
        public void Diagnostics_SeparateSessions_PublishIndependentSnapshots()
        {
            var first = new BattleSessionRuntime();
            var second = new BattleSessionRuntime();
            var firstJitter = new JitterBufferStatsSnapshot { DelayFrames = 4 };
            var secondJitter = new JitterBufferStatsSnapshot { DelayFrames = 7 };

            first.Diagnostics.PublishJitterBuffer(firstJitter);
            second.Diagnostics.PublishJitterBuffer(secondJitter);

            Assert.That(first.Diagnostics, Is.Not.SameAs(second.Diagnostics));
            Assert.That(BattleFlowDebugProvider.JitterBufferStats, Is.SameAs(secondJitter));

            first.Diagnostics.Dispose();

            Assert.That(BattleFlowDebugProvider.JitterBufferStats, Is.SameAs(secondJitter));

            second.Diagnostics.Dispose();
            Assert.That(BattleFlowDebugProvider.JitterBufferStats, Is.Null);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Test]
        public void Diagnostics_DebugControlFacade_UpdatesActiveSessionOwner()
        {
            var diagnostics = new BattleSessionDiagnostics(new BattleReplicationRuntime());
            try
            {
                BattleSessionFeature.DebugForceClientHashMismatch = false;
                diagnostics.PublishDebugControls();

                BattleSessionFeature.DebugForceClientHashMismatch = true;

                Assert.That(diagnostics.ShouldForceClientHashMismatch, Is.True);
            }
            finally
            {
                diagnostics.Dispose();
                BattleSessionFeature.DebugForceClientHashMismatch = false;
            }
        }

        [Test]
        public void Diagnostics_DebugControlReplacement_AdoptsLatestCompatibilityValue()
        {
            var stale = new BattleSessionDiagnostics(new BattleReplicationRuntime());
            var active = new BattleSessionDiagnostics(new BattleReplicationRuntime());
            try
            {
                BattleSessionFeature.DebugForceClientHashMismatch = true;
                stale.PublishDebugControls();
                active.PublishDebugControls();

                Assert.That(stale.ShouldForceClientHashMismatch, Is.True);
                Assert.That(active.ShouldForceClientHashMismatch, Is.True);
            }
            finally
            {
                stale.Dispose();
                active.Dispose();
                BattleSessionFeature.DebugForceClientHashMismatch = false;
            }
        }

        [Test]
        public void Diagnostics_DebugControlStaleOwnerDispose_DoesNotAffectReplacement()
        {
            var stale = new BattleSessionDiagnostics(new BattleReplicationRuntime());
            var active = new BattleSessionDiagnostics(new BattleReplicationRuntime());
            try
            {
                BattleSessionFeature.DebugForceClientHashMismatch = false;
                stale.PublishDebugControls();
                active.PublishDebugControls();
                BattleSessionFeature.DebugForceClientHashMismatch = true;

                stale.Dispose();

                Assert.That(active.ShouldForceClientHashMismatch, Is.True);
                Assert.That(BattleSessionFeature.DebugForceClientHashMismatch, Is.True);
            }
            finally
            {
                stale.Dispose();
                active.Dispose();
                BattleSessionFeature.DebugForceClientHashMismatch = false;
            }
        }
#endif

        [Test]
        public void Diagnostics_HealthFacade_ReflectsReplicationState()
        {
            var replication = new BattleReplicationRuntime();
            var diagnostics = new BattleSessionDiagnostics(replication);
            var health = new MobaSynchronizationHealthSnapshot(
                MobaSynchronizationHealthLevel.Degraded,
                3,
                2,
                0,
                1,
                0,
                0,
                1,
                default);
            var report = new SyncHealthReport(
                3,
                1,
                1,
                1,
                0,
                0,
                null,
                null);

            replication.SynchronizationHealth = health;
            replication.SynchronizationHealthReport = report;

            Assert.That(diagnostics.SynchronizationHealth.PressureScore, Is.EqualTo(3));
            Assert.That(diagnostics.SynchronizationHealth.Level, Is.EqualTo(MobaSynchronizationHealthLevel.Degraded));
            Assert.That(diagnostics.SynchronizationHealthReport, Is.SameAs(report));
        }

        [Test]
        public void ReplicationRuntime_BuildAndDispose_OwnsBindingsAndRestoresOptions()
        {
            var transport = CreateNetworkTransport();
            var options = transport.Options;
            Func<string> previousEpoch = () => "previous";
            Func<long> previousSequence = () => 17L;
            Action<int> previousAck = _ => { };
            options.GetReliableEventEpoch = previousEpoch;
            options.GetReliableEventLastAcknowledgedSequence = previousSequence;
            options.OnSubmitInputAck = previousAck;
            var owner = new BattleReplicationRuntime();
            var connected = 0;
            var disconnected = 0;

            var checkpointAccepted = owner.Build(
                transport,
                30,
                42UL,
                "battle",
                default,
                _ => { },
                _ => { },
                () => disconnected++,
                () => connected++);

            Assert.That(checkpointAccepted, Is.True);
            Assert.That(owner.IsBuilt, Is.True);
            Assert.That(owner.Transport, Is.SameAs(transport));
            Assert.That(owner.InterpolationController, Is.Not.Null);
            Assert.That(owner.ReplicationPipeline, Is.Not.Null);
            Assert.That(owner.SnapshotAdmission, Is.Not.Null);
            Assert.That(owner.AuthoritativeSnapshotState, Is.Not.Null);
            Assert.That(owner.ReliableEventCursor, Is.Not.Null);
            Assert.That(owner.PendingStateImport, Is.True);
            Assert.That(options.GetReliableEventEpoch, Is.Not.SameAs(previousEpoch));
            Assert.That(options.GetReliableEventLastAcknowledgedSequence, Is.Not.SameAs(previousSequence));
            Assert.That(options.OnSubmitInputAck, Is.Not.SameAs(previousAck));

            options.OnSubmitInputAck(12);
            InvokePrivate(transport, "OnConnected");
            InvokePrivate(transport, "OnDisconnected");

            Assert.That(owner.LastServerAckFrame, Is.EqualTo(12));
            Assert.That(connected, Is.EqualTo(1));
            Assert.That(disconnected, Is.EqualTo(1));

            owner.Dispose();
            owner.Dispose();

            Assert.That(owner.IsBuilt, Is.False);
            Assert.That(owner.InterpolationController, Is.Null);
            Assert.That(owner.PendingReliableEventBatches, Is.Empty);
            Assert.That(options.GetReliableEventEpoch, Is.SameAs(previousEpoch));
            Assert.That(options.GetReliableEventLastAcknowledgedSequence, Is.SameAs(previousSequence));
            Assert.That(options.OnSubmitInputAck, Is.SameAs(previousAck));
            transport.Dispose();
        }

        [Test]
        public void ReplicationRuntime_Rebuild_DetachesOldGeneration()
        {
            var firstTransport = CreateNetworkTransport();
            var secondTransport = CreateNetworkTransport();
            var owner = new BattleReplicationRuntime();
            var firstConnected = 0;
            var secondConnected = 0;

            owner.Build(
                firstTransport, 30, 1UL, "first", default,
                _ => { }, _ => { }, () => { }, () => firstConnected++);
            owner.Build(
                secondTransport, 30, 2UL, "second", default,
                _ => { }, _ => { }, () => { }, () => secondConnected++);

            InvokePrivate(firstTransport, "OnConnected");
            InvokePrivate(secondTransport, "OnConnected");

            Assert.That(firstConnected, Is.Zero);
            Assert.That(secondConnected, Is.EqualTo(1));
            Assert.That(owner.Transport, Is.SameAs(secondTransport));

            owner.Dispose();
            firstTransport.Dispose();
            secondTransport.Dispose();
        }

        [Test]
        public void ReplicationRuntime_InvalidRebuild_PreservesCurrentGeneration()
        {
            var transport = CreateNetworkTransport();
            var owner = new BattleReplicationRuntime();
            var connected = 0;
            owner.Build(
                transport, 30, 1UL, "battle", default,
                _ => { }, _ => { }, () => { }, () => connected++);

            Assert.Throws<ArgumentNullException>(() => owner.Build(
                transport, 30, 1UL, "battle", default,
                null, _ => { }, () => { }, () => { }));
            InvokePrivate(transport, "OnConnected");

            Assert.That(owner.IsBuilt, Is.True);
            Assert.That(owner.Transport, Is.SameAs(transport));
            Assert.That(connected, Is.EqualTo(1));

            owner.Dispose();
            transport.Dispose();
        }

        [Test]
        public void ReplicationRuntime_Dispose_DoesNotOverwriteExternallyReplacedOptions()
        {
            var transport = CreateNetworkTransport();
            var owner = new BattleReplicationRuntime();
            owner.Build(
                transport, 30, 1UL, "battle", default,
                _ => { }, _ => { }, () => { }, () => { });
            Func<string> replacementEpoch = () => "replacement";
            Func<long> replacementSequence = () => 99L;
            Action<int> replacementAck = _ => { };
            transport.Options.GetReliableEventEpoch = replacementEpoch;
            transport.Options.GetReliableEventLastAcknowledgedSequence = replacementSequence;
            transport.Options.OnSubmitInputAck = replacementAck;

            owner.Dispose();

            Assert.That(transport.Options.GetReliableEventEpoch, Is.SameAs(replacementEpoch));
            Assert.That(transport.Options.GetReliableEventLastAcknowledgedSequence, Is.SameAs(replacementSequence));
            Assert.That(transport.Options.OnSubmitInputAck, Is.SameAs(replacementAck));
            transport.Dispose();
        }

        private static NetworkTransport CreateNetworkTransport()
        {
            return new NetworkTransport(new NetworkTransportOptions
            {
                ConnectionFactory = () => new TrackingConnection()
            });
        }

        private static void InvokePrivate(object target, string methodName)
        {
            var method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Private method '{methodName}' was not found.");
            method.Invoke(target, null);
        }

        private sealed class TrackingConnection : IConnection
        {
            public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
            public bool IsConnected => State == ConnectionState.Connected;
            public int OpenCount { get; private set; }
            public int TickCount { get; private set; }
            public int DisposeCount { get; private set; }
            public string LastHost { get; private set; }
            public int LastPort { get; private set; }
            public float LastDeltaTime { get; private set; }

            public event Action Connected;
            public event Action Disconnected;
            public event Action<Exception> Error;
            public event Action<uint, uint, ArraySegment<byte>> PacketReceived;
            public event Action<uint, ArraySegment<byte>> ServerPushReceived;
            public event Action<string, string> Kicked;

            public void Open(string host, int port)
            {
                LastHost = host;
                LastPort = port;
                OpenCount++;
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
                LastDeltaTime = deltaTime;
                TickCount++;
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
                DisposeCount++;
                Close();
            }
        }
    }
}
