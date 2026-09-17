using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Requests;
using AbilityKit.Game.Battle.Testing;
using AbilityKit.Game.Flow;
using AbilityKit.Network.Runtime.Conditioning;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.BattleFlow;
using AbilityKit.Demo.Moba.EnvironmentModel;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class BattleSyncVirtualRouteTests
    {
        [Test]
        public void SharedDsl_UnityRouteUsesRealPredictionAndMatchesHeadlessState()
        {
            const string dsl = @"
seed 83
network packet inbound opcode=5202 seq=1 frame=1 at=0
network disconnect at=34
network packet inbound opcode=5202 seq=2 frame=2 at=68
network packet inbound opcode=5202 seq=3 frame=3 at=102
network reconnect at=136
network packet inbound opcode=5202 seq=102 frame=2 hash=999 at=136
network packet inbound opcode=5202 seq=103 frame=3 hash=6 at=136
network packet inbound opcode=5202 seq=104 frame=4 at=238
assert-sync predictedHashes gte 3
assert-sync mismatch eq 1
assert-sync rollback eq 1
assert-sync mismatchFrame eq 2
assert-sync rollbackFrame eq 1
assert-sync confirmedFrame eq 3
assert-sync predictedFrame eq 3
assert-sync wasReplaying eq true
assert-sync replaying eq false
assert-sync finalHash eq 6
";
            var headlessScenario = BattleFlowCompiler.Compile("shared-dsl",
                MobaBattleFlowDslParser.Parse("sync-backend headless\n" + dsl));
            var unityScenario = BattleFlowCompiler.Compile("shared-dsl",
                MobaBattleFlowDslParser.Parse("sync-backend unity-route\n" + dsl));
            var headless = MobaBattleFlowPredictionScenarioRunner.Run(headlessScenario);
            var backend = new UnityRouteMobaPredictionScenarioBackend();
            var first = MobaBattleFlowPredictionScenarioRunner.Run(unityScenario, backend);
            var second = MobaBattleFlowPredictionScenarioRunner.Run(unityScenario, backend);
            var verdict = MobaBattleFlowPredictionScenarioRunner.Verify(
                ((MobaBattleFlowAssertions)unityScenario.Expectations).Prediction, first);

            Assert.That(verdict.Passed, Is.True, string.Join("\n", verdict.Failures));
            CollectionAssert.AreEqual(headless.StateTrace, first.StateTrace);
            CollectionAssert.AreEqual(first.StateTrace, second.StateTrace);
            Assert.That(first.DeterminismFingerprint, Is.EqualTo(second.DeterminismFingerprint));
            Assert.That(first.DeterminismFingerprint, Is.Not.EqualTo(headless.DeterminismFingerprint));
            Assert.That(first.PredictedHashes, Is.EqualTo(headless.PredictedHashes));
            Assert.That(first.BackendId, Is.EqualTo("unity-route"));
            Assert.That(first.Rollbacks, Is.EqualTo(1));
            Assert.That(first.Mismatches, Is.EqualTo(1));
        }

        [Test]
        public void UnityRoute_DisposeIsIdempotentAndRejectsLateDelivery()
        {
            var backend = new UnityRouteMobaPredictionScenarioBackend();
            var worldId = new WorldId("virtual-battle-sync");
            var target = new RecordingReconcileTarget();
            for (var i = 0; i < 3; i++)
            {
                var route = backend.CreateRoute(target, worldId, 30);
                var packet = ResolveFrame(CreatePacket(0, i, 1, 1), worldId);
                try { route.Feed(packet); }
                finally { route.Dispose(); }
                route.Dispose();
                Assert.Throws<ObjectDisposedException>(() => route.Feed(packet));
            }
            Assert.That(target.Hashes.Count, Is.EqualTo(3));
        }

        [Test]
        public void DisconnectCatchUp_StateHashesUseBattleSyncRouteDeterministically()
        {
            var first = RunScenario();
            var second = RunScenario();

            CollectionAssert.AreEqual(
                new[] { "1:1", "2:999", "3:6", "4:10" },
                first.ObservedHashes);
            CollectionAssert.AreEqual(first.ObservedHashes, second.ObservedHashes);
            Assert.That(first.DeterminismFingerprint, Is.EqualTo(second.DeterminismFingerprint));
            Assert.That(first.BlockedInbound, Is.EqualTo(2));
            Assert.That(first.RuntimeWorldId, Is.EqualTo("virtual-battle-sync"));
        }

        private static ScenarioObservation RunScenario()
        {
            var worldId = new WorldId("virtual-battle-sync");
            var context = BattleContext.Rent();
            using var session = new BattleLogicSession(
                new BattleLogicSessionOptions
                {
                    Mode = BattleLogicMode.Remote,
                    WorldId = worldId,
                    AutoConnect = false,
                    AutoCreateWorld = false,
                    AutoJoin = false,
                },
                new NoopBattleLogicTransport());
            var target = new RecordingReconcileTarget();

            try
            {
                context.Plan = BattleStartPlanBuilder
                    .ForWorld(worldId.Value, "battle", "virtual-client", "p1", 30, 0)
                    .WithSync(BattleSyncMode.HybridPredictReconcile, BattleViewEventSourceMode.SnapshotOnly)
                    .Build();
                context.PredictionRuntime.Bind(null, target, null, null);

                using var route = new MobaVirtualBattleSyncRoute(context, session);
                var plan = VirtualNetworkScenarioPlan.Compile(
                    CreateCommands(),
                    NetworkConditionProfile.Ideal);
                var run = route.Run(
                    plan,
                    command => ResolveFrame(command, worldId),
                    seed: 83,
                    timeoutMs: 1000,
                    serializeFrame: packet => new ArraySegment<byte>(
                        packet.Snapshot.HasValue
                            ? packet.Snapshot.Value.Payload
                            : Array.Empty<byte>()),
                    captureFinalState: () => string.Join(",", target.Hashes));

                return new ScenarioObservation
                {
                    ObservedHashes = target.Hashes.ToArray(),
                    DeterminismFingerprint = run.DeterminismFingerprint,
                    BlockedInbound = run.LinkEvents.Count(entry =>
                        entry.Kind == VirtualNetworkLinkEventKind.BlockedInbound),
                    RuntimeWorldId = context.HasRuntimeWorldId
                        ? context.RuntimeWorldId.Value
                        : string.Empty,
                };
            }
            finally
            {
                BattleContext.Return(context);
            }
        }

        private static VirtualNetworkCommand[] CreateCommands()
        {
            return new[]
            {
                CreatePacket(0, 1, 1, 1),
                new VirtualNetworkCommand(10, VirtualNetworkScenarioPlayer.DisconnectCommand),
                CreatePacket(20, 2, 2, 999),
                CreatePacket(30, 3, 3, 6),
                new VirtualNetworkCommand(40, VirtualNetworkScenarioPlayer.ReconnectCommand),
                CreatePacket(40, 102, 2, 999),
                CreatePacket(40, 103, 3, 6),
                CreatePacket(50, 104, 4, 10),
            };
        }

        private static VirtualNetworkCommand CreatePacket(
            long atMs,
            int sequence,
            int frame,
            uint hash)
        {
            return new VirtualNetworkCommand(
                atMs,
                VirtualNetworkScenarioPlayer.PacketCommand,
                new Dictionary<string, string>
                {
                    ["direction"] = "inbound",
                    ["opCode"] = "5202",
                    ["seq"] = sequence.ToString(CultureInfo.InvariantCulture),
                    ["frame"] = frame.ToString(CultureInfo.InvariantCulture),
                    ["hash"] = hash.ToString(CultureInfo.InvariantCulture),
                });
        }

        private static FramePacket ResolveFrame(VirtualNetworkCommand command, WorldId worldId)
        {
            var frame = int.Parse(command.RequireParameter("frame"), CultureInfo.InvariantCulture);
            var hash = uint.Parse(command.RequireParameter("hash"), CultureInfo.InvariantCulture);
            return new FramePacket(
                worldId,
                new FrameIndex(frame),
                Array.Empty<PlayerInputCommand>(),
                new WorldStateSnapshot(
                    MobaOpCodes.Snapshot.StateHash,
                    MobaStateHashSnapshotCodec.Serialize(frame, hash)));
        }

        private sealed class RecordingReconcileTarget : IClientPredictionReconcileTarget
        {
            public List<string> Hashes { get; } = new List<string>();

            public void OnAuthoritativeStateHash(
                WorldId worldId,
                FrameIndex frame,
                WorldStateHash hash)
            {
                Assert.That(worldId.Value, Is.EqualTo("virtual-battle-sync"));
                Hashes.Add($"{frame.Value}:{hash.Value}");
            }
        }

        private sealed class ScenarioObservation
        {
            public string[] ObservedHashes { get; set; }
            public string DeterminismFingerprint { get; set; }
            public int BlockedInbound { get; set; }
            public string RuntimeWorldId { get; set; }
        }

        private sealed class NoopBattleLogicTransport : IBattleLogicTransport
        {
            public event Action<FramePacket> FramePushed;

            public void Connect() { }
            public void Disconnect() { }
            public void SendCreateWorld(CreateWorldRequest request) { }
            public void SendJoin(JoinWorldRequest request) { }
            public void SendLeave(LeaveWorldRequest request) { }
            public void SendInput(SubmitInputRequest request) { }
        }
    }
}
