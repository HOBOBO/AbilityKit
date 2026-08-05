using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Requests;
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

        private static FramePacket CreatePacket(int frame)
        {
            return new FramePacket(
                new WorldId("remote-frame-streams"),
                new FrameIndex(frame),
                Array.Empty<PlayerInputCommand>(),
                snapshot: null);
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
