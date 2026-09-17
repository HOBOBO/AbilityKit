using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Requests;
using AbilityKit.Game.Battle.Testing;
using AbilityKit.Network.Runtime.Conditioning;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class RemoteFrameStreamHubTests
    {
        [Test]
        public void OnFrameReceived_PublishesInputAndSnapshotFrames()
        {
            using var streams = RemoteFrameStreamsFactory.Create();
            var packet = CreatePacket(12);

            streams.OnFrameReceived(packet);

            Assert.That(streams.InputFrames.TryGet(12, out var inputFrame), Is.True);
            Assert.That(inputFrame.Frame.Value, Is.EqualTo(12));
            Assert.That(inputFrame.Commands, Is.Empty);
            Assert.That(streams.SnapshotFrames.TryGet(12, out var snapshotFrame), Is.True);
            Assert.That(snapshotFrame.Frame.Value, Is.EqualTo(12));
            Assert.That(snapshotFrame.Envelopes, Is.Empty);
            Assert.That(streams.InputSink, Is.SameAs(streams.InputFrames));
            Assert.That(streams.SnapshotSink, Is.SameAs(streams.SnapshotFrames));
        }

        [Test]
        public void OnFrameReceived_TrimsFramesOutsideRetentionWindow()
        {
            using var streams = RemoteFrameStreamsFactory.Create();
            streams.OnFrameReceived(CreatePacket(1));
            streams.OnFrameReceived(CreatePacket(258));

            Assert.That(streams.InputFrames.TryGet(1, out _), Is.False);
            Assert.That(streams.SnapshotFrames.TryGet(1, out _), Is.False);
            Assert.That(streams.InputFrames.TryGet(258, out _), Is.True);
            Assert.That(streams.SnapshotFrames.TryGet(258, out _), Is.True);
        }

        [Test]
        public void BattleLogicSession_Dispose_DisposesInjectedStreams()
        {
            var streams = new RecordingRemoteFrameStreams();
            var session = new BattleLogicSession(
                new BattleLogicSessionOptions
                {
                    Mode = BattleLogicMode.Remote,
                    WorldId = new WorldId("remote-frame-stream-lifecycle"),
                    AutoConnect = false,
                    AutoCreateWorld = false,
                    AutoJoin = false,
                },
                new NoopBattleLogicTransport(),
                new MobaRollbackRegistryFactory(),
                new MobaBattleLogicRuntimeFactory(),
                streams);

            session.Dispose();

            Assert.That(streams.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void VirtualFrameRunner_ReconnectCatchUpInjectsSessionStreamsBeforeBroadcast()
        {
            using var session = new BattleLogicSession(
                new BattleLogicSessionOptions
                {
                    Mode = BattleLogicMode.Remote,
                    WorldId = new WorldId("virtual-session-battle"),
                    AutoConnect = false,
                    AutoCreateWorld = false,
                    AutoJoin = false,
                },
                new NoopBattleLogicTransport());

            var observedFrames = new List<int>();
            var streamsReadyBeforeBroadcast = true;
            session.FrameReceived += packet =>
            {
                observedFrames.Add(packet.Frame.Value);
                streamsReadyBeforeBroadcast &=
                    session.RemoteInputFrames.TryGet(packet.Frame.Value, out var inputFrame) &&
                    inputFrame.Commands.Length == 1 &&
                    session.RemoteSnapshotFrames.TryGet(packet.Frame.Value, out var snapshotFrame) &&
                    snapshotFrame.Envelopes.Length == 1;
            };

            var plan = VirtualNetworkScenarioPlan.Compile(
                CreateVirtualFrameCommands(),
                NetworkConditionProfile.Ideal);
            var result = new MobaVirtualFrameSessionRunner().Run(
                session.InjectRemoteFrame,
                plan,
                ResolveVirtualFrame,
                seed: 47,
                timeoutMs: 1000,
                advanceSimulationByMs: deltaMs => session.Tick(deltaMs / 1000f),
                captureFinalState: () => string.Join(",", observedFrames));

            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, observedFrames);
            Assert.That(streamsReadyBeforeBroadcast, Is.True);
            Assert.That(result.FinishedAtMs, Is.EqualTo(60));
            Assert.That(result.SimulationFinishedAtMs, Is.EqualTo(60));
            Assert.That(result.FinalStateFingerprint, Is.EqualTo("1,2,3,4"));
            Assert.That(result.LinkEvents.Count(entry =>
                entry.Kind == VirtualNetworkLinkEventKind.BlockedInbound), Is.EqualTo(2));
            Assert.That(result.DeterminismFingerprint, Is.Not.Empty);
        }

        private static FramePacket CreatePacket(int frame)
        {
            return new FramePacket(
                new WorldId("remote-frame-streams"),
                new FrameIndex(frame),
                Array.Empty<PlayerInputCommand>(),
                snapshot: null);
        }

        private static VirtualNetworkCommand[] CreateVirtualFrameCommands()
        {
            return new[]
            {
                new VirtualNetworkCommand(0, VirtualNetworkScenarioPlayer.PhaseCommand,
                    new Dictionary<string, string>
                    {
                        ["until"] = "1000",
                        ["direction"] = "inbound",
                        ["opCode"] = "5202",
                        ["latency"] = "10",
                    }),
                CreateVirtualPacket(0, 1, 1),
                new VirtualNetworkCommand(15, VirtualNetworkScenarioPlayer.DisconnectCommand),
                CreateVirtualPacket(20, 2, 2),
                CreateVirtualPacket(30, 3, 3),
                new VirtualNetworkCommand(40, VirtualNetworkScenarioPlayer.ReconnectCommand),
                CreateVirtualPacket(40, 102, 2),
                CreateVirtualPacket(40, 103, 3),
                CreateVirtualPacket(50, 4, 4),
            };
        }

        private static VirtualNetworkCommand CreateVirtualPacket(int atMs, int sequence, int frame)
        {
            return new VirtualNetworkCommand(
                atMs,
                VirtualNetworkScenarioPlayer.PacketCommand,
                new Dictionary<string, string>
                {
                    ["direction"] = "inbound",
                    ["opCode"] = "5202",
                    ["seq"] = sequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["frame"] = frame.ToString(System.Globalization.CultureInfo.InvariantCulture),
                });
        }

        private static FramePacket ResolveVirtualFrame(VirtualNetworkCommand command)
        {
            var frame = int.Parse(
                command.RequireParameter("frame"),
                System.Globalization.CultureInfo.InvariantCulture);
            var frameIndex = new FrameIndex(frame);
            return new FramePacket(
                new WorldId("virtual-session-battle"),
                frameIndex,
                new[]
                {
                    new PlayerInputCommand(
                        frameIndex,
                        new PlayerId("p1"),
                        4101,
                        BitConverter.GetBytes(frame)),
                },
                new WorldStateSnapshot(7101, BitConverter.GetBytes(frame * 31)));
        }

        private sealed class RecordingRemoteFrameStreams : IRemoteFrameStreams
        {
            private readonly RemoteFrameStreamHub _inner = new RemoteFrameStreamHub();

            public int DisposeCount { get; private set; }
            public AbilityKit.Network.Abstractions.IRemoteFrameSource<RemoteInputFrame> InputFrames => _inner.InputFrames;
            public AbilityKit.Network.Abstractions.IRemoteFrameSink<RemoteInputFrame> InputSink => _inner.InputSink;
            public AbilityKit.Network.Abstractions.IRemoteFrameSource<RemoteSnapshotFrame> SnapshotFrames => _inner.SnapshotFrames;
            public AbilityKit.Network.Abstractions.IRemoteFrameSink<RemoteSnapshotFrame> SnapshotSink => _inner.SnapshotSink;

            public void OnFrameReceived(FramePacket packet) => _inner.OnFrameReceived(packet);

            public void Dispose()
            {
                DisposeCount++;
                _inner.Dispose();
            }
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
