using System;
using System.Collections.Generic;
using AbilityKit.Core.Recording.FrameRecord;
using AbilityKit.Game.Flow;
using AbilityKit.Game.Flow.Battle.Replay;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class BattleReplaySessionOwnerTests
    {
        [Test]
        public void TryStart_WhenFactoryFails_RollsBackOwnerState()
        {
            var factory = new FakeReplaySessionFactory(CreateFile())
            {
                StartFailure = new InvalidOperationException("bootstrap failed"),
            };
            var owner = new BattleReplaySessionOwner(factory);

            var started = owner.TryStart(CreatePlan(), "replay.record", out var error);

            Assert.That(started, Is.False);
            Assert.That(error, Does.Contain("bootstrap failed"));
            Assert.That(owner.IsActive, Is.False);
            Assert.That(owner.IsPlaying, Is.False);
            Assert.That(owner.CurrentFrame, Is.Zero);
            Assert.That(owner.LastFrame, Is.Zero);
            Assert.That(owner.ReplayPath, Is.Empty);
            Assert.That(factory.StartCount, Is.EqualTo(1));
        }

        [Test]
        public void TryStart_WhenPreviousRuntimeDisposeFails_ReturnsFailureAndStopsOwner()
        {
            var factory = new FakeReplaySessionFactory(CreateFile());
            var owner = new BattleReplaySessionOwner(factory);
            Assert.That(owner.TryStart(CreatePlan(), "first.record", out var firstError), Is.True, firstError);
            factory.Runtimes[0].DisposeFailure = new InvalidOperationException("dispose failed");

            var started = owner.TryStart(CreatePlan(), "second.record", out var error);

            Assert.That(started, Is.False);
            Assert.That(error, Does.Contain("dispose failed"));
            Assert.That(owner.IsActive, Is.False);
            Assert.That(owner.CurrentFrame, Is.Zero);
            Assert.That(owner.ReplayPath, Is.Empty);
            Assert.That(factory.StartCount, Is.EqualTo(1));
        }

        [Test]
        public void SeekToFrame_Backward_RecreatesRuntimeAndPreservesPlaybackState()
        {
            var factory = new FakeReplaySessionFactory(CreateFile());
            var owner = new BattleReplaySessionOwner(factory);
            Assert.That(owner.TryStart(CreatePlan(), "replay.record", out var error), Is.True, error);

            owner.PlaybackSpeed = 2.5f;
            owner.Play();
            Assert.That(owner.SeekToFrame(6), Is.True);
            var first = factory.Runtimes[0];

            Assert.That(owner.SeekToFrame(2), Is.True);

            Assert.That(factory.StartCount, Is.EqualTo(2));
            Assert.That(first.DisposeCount, Is.EqualTo(1));
            Assert.That(factory.Runtimes[1].PumpedFrames, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(owner.CurrentFrame, Is.EqualTo(2));
            Assert.That(owner.PlaybackSpeed, Is.EqualTo(2.5f));
            Assert.That(owner.IsPlaying, Is.True);
        }

        [Test]
        public void SeekToFrame_WhenPreviousRuntimeDisposeFails_ReturnsFailureAndStopsOwner()
        {
            var factory = new FakeReplaySessionFactory(CreateFile());
            var owner = new BattleReplaySessionOwner(factory);
            Assert.That(owner.TryStart(CreatePlan(), "replay.record", out var error), Is.True, error);
            Assert.That(owner.SeekToFrame(5), Is.True);
            factory.Runtimes[0].DisposeFailure = new InvalidOperationException("dispose failed");

            var sought = owner.SeekToFrame(1);

            Assert.That(sought, Is.False);
            Assert.That(owner.IsActive, Is.False);
            Assert.That(owner.CurrentFrame, Is.Zero);
            Assert.That(owner.ReplayPath, Is.Empty);
            Assert.That(factory.StartCount, Is.EqualTo(1));
            Assert.That(factory.Runtimes[0].DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void SeekToFrame_WhenRestartFails_StopsOwner()
        {
            var factory = new FakeReplaySessionFactory(CreateFile());
            var owner = new BattleReplaySessionOwner(factory);
            Assert.That(owner.TryStart(CreatePlan(), "replay.record", out var error), Is.True, error);
            Assert.That(owner.SeekToFrame(5), Is.True);
            factory.StartFailure = new InvalidOperationException("restart failed");

            var sought = owner.SeekToFrame(1);

            Assert.That(sought, Is.False);
            Assert.That(owner.IsActive, Is.False);
            Assert.That(owner.CurrentFrame, Is.Zero);
            Assert.That(owner.ReplayPath, Is.Empty);
            Assert.That(factory.Runtimes[0].DisposeCount, Is.EqualTo(1));
        }

        private static BattleStartPlan CreatePlan()
        {
            return new BattleStartPlan(
                worldId: "battle-world",
                worldType: "moba",
                clientId: "client-1",
                playerId: "player-1",
                tickRate: 30,
                inputDelayFrames: 0,
                hostMode: default,
                useGatewayTransport: false,
                gatewayHost: string.Empty,
                gatewayPort: 0,
                numericRoomId: 0,
                gatewaySessionToken: string.Empty,
                gatewayRegion: string.Empty,
                gatewayServerId: string.Empty,
                gatewayAutoCreateRoom: false,
                gatewayAutoJoinRoom: false,
                gatewayJoinRoomId: string.Empty,
                gatewayCreateRoomOpCode: 0,
                gatewayJoinRoomOpCode: 0,
                autoConnect: false,
                autoCreateWorld: false,
                autoJoin: false,
                autoReady: false,
                syncMode: default,
                viewEventSourceMode: default,
                enableClientPrediction: false,
                enableConfirmedAuthorityWorld: false,
                enableInputRecording: false,
                inputRecordOutputPath: string.Empty,
                enableInputReplay: true,
                inputReplayPath: "replay.record",
                runMode: default,
                createWorldOpCode: 0,
                createWorldPayload: Array.Empty<byte>());
        }

        private static FrameRecordFile CreateFile()
        {
            return new FrameRecordFile
            {
                Meta = new FrameRecordMeta
                {
                    WorldId = "battle-world",
                    WorldType = "moba",
                    PlayerId = "player-1",
                    TickRate = 30,
                },
                Inputs = new List<FrameRecordInputFrame>
                {
                    new FrameRecordInputFrame { Frame = 10, PlayerId = "player-1" },
                },
                StateHashes = new List<FrameRecordStateHashFrame>(),
                Snapshots = new List<FrameRecordSnapshotFrame>(),
                Index = new List<FrameRecordChunkIndex>(),
            };
        }

        private sealed class FakeReplaySessionFactory : IBattleReplaySessionFactory
        {
            private readonly FrameRecordFile _file;

            public FakeReplaySessionFactory(FrameRecordFile file)
            {
                _file = file;
            }

            public Exception StartFailure { get; set; }
            public int StartCount { get; private set; }
            public List<FakeReplaySessionRuntime> Runtimes { get; } = new List<FakeReplaySessionRuntime>();

            public FrameRecordFile Load(string path)
            {
                return _file;
            }

            public IBattleReplaySessionRuntime Start(BattleStartPlan plan, FrameRecordFile file)
            {
                StartCount++;
                if (StartFailure != null) throw StartFailure;

                var runtime = new FakeReplaySessionRuntime(lastFrame: 10);
                Runtimes.Add(runtime);
                return runtime;
            }
        }

        private sealed class FakeReplaySessionRuntime : IBattleReplaySessionRuntime
        {
            private bool _disposed;
            private bool _isPlaying = true;

            public FakeReplaySessionRuntime(int lastFrame)
            {
                LastFrame = lastFrame;
            }

            public bool IsActive => !_disposed;
            public bool IsPlaying => !_disposed && _isPlaying;
            public int LastFrame { get; }
            public float PlaybackSpeed { get; set; } = 1f;
            public Exception DisposeFailure { get; set; }
            public int DisposeCount { get; private set; }
            public List<int> PumpedFrames { get; } = new List<int>();

            public void Play()
            {
                _isPlaying = true;
            }

            public void Pause()
            {
                _isPlaying = false;
            }

            public void PumpAndTick(int frame, float fixedDeltaSeconds)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(FakeReplaySessionRuntime));
                PumpedFrames.Add(frame);
            }

            public void Dispose()
            {
                DisposeCount++;
                _disposed = true;
                _isPlaying = false;
                if (DisposeFailure != null) throw DisposeFailure;
            }
        }
    }
}
