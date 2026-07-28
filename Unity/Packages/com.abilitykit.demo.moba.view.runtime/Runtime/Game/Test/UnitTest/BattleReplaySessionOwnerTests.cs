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
        public void TryStart_AfterFailureSucceeds_ClearsLastFailure()
        {
            var factory = new FakeReplaySessionFactory(CreateFile())
            {
                StartFailure = new InvalidOperationException("bootstrap failed"),
            };
            var owner = new BattleReplaySessionOwner(factory);
            Assert.That(owner.TryStart(CreatePlan(), "failed.record", out _), Is.False);
            Assert.That(owner.LastFailure, Is.SameAs(factory.StartFailure));
            factory.StartFailure = null;

            var started = owner.TryStart(CreatePlan(), "recovered.record", out var error);

            Assert.That(started, Is.True, error);
            Assert.That(owner.LastFailure, Is.Null);
            Assert.That(owner.IsActive, Is.True);
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
        public void SeekToFrame_Backward_WithCheckpointCapability_RestoresNearestCheckpoint()
        {
            var factory = new FakeReplaySessionFactory(CreateFile())
            {
                SupportsCheckpoints = true,
                LastFrame = 100,
            };
            var owner = new BattleReplaySessionOwner(factory);
            Assert.That(owner.TryStart(CreatePlan(), "replay.record", out var error), Is.True, error);

            owner.PlaybackSpeed = 2.5f;
            owner.Play();
            Assert.That(owner.SeekToFrame(65), Is.True);
            var runtime = (CheckpointReplaySessionRuntime)factory.Runtimes[0];
            runtime.PumpedFrames.Clear();

            Assert.That(owner.SeekToFrame(35), Is.True);

            Assert.That(factory.StartCount, Is.EqualTo(1));
            Assert.That(runtime.RestoredFrames, Is.EqualTo(new[] { 30 }));
            Assert.That(runtime.PumpedFrames, Is.EqualTo(new[] { 31, 32, 33, 34, 35 }));
            Assert.That(owner.CurrentFrame, Is.EqualTo(35));
            Assert.That(owner.PlaybackSpeed, Is.EqualTo(2.5f));
            Assert.That(owner.IsPlaying, Is.True);
        }

        [Test]
        public void SeekToFrame_Backward_WhenCheckpointRestoreFails_StopsOwner()
        {
            var factory = new FakeReplaySessionFactory(CreateFile())
            {
                SupportsCheckpoints = true,
                LastFrame = 100,
            };
            var owner = new BattleReplaySessionOwner(factory);
            Assert.That(owner.TryStart(CreatePlan(), "replay.record", out var error), Is.True, error);
            Assert.That(owner.SeekToFrame(40), Is.True);
            var runtime = (CheckpointReplaySessionRuntime)factory.Runtimes[0];
            runtime.RestoreFailure = new InvalidOperationException("restore failed");

            var sought = owner.SeekToFrame(10);

            Assert.That(sought, Is.False);
            Assert.That(owner.IsActive, Is.False);
            Assert.That(owner.CurrentFrame, Is.Zero);
            Assert.That(owner.ReplayPath, Is.Empty);
            Assert.That(runtime.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void SeekToFrame_Backward_ReleasesCheckpointsFromDiscardedFuture()
        {
            var factory = new FakeReplaySessionFactory(CreateFile())
            {
                SupportsCheckpoints = true,
                LastFrame = 100,
            };
            var owner = new BattleReplaySessionOwner(factory);
            Assert.That(owner.TryStart(CreatePlan(), "replay.record", out var error), Is.True, error);
            Assert.That(owner.SeekToFrame(65), Is.True);
            var runtime = (CheckpointReplaySessionRuntime)factory.Runtimes[0];

            Assert.That(owner.SeekToFrame(35), Is.True);

            Assert.That(runtime.ReleasedFrames, Is.EqualTo(new[] { 60 }));
            Assert.That(runtime.ActiveCheckpointCount, Is.EqualTo(2));
        }

        [Test]
        public void SeekToFrame_WhenCheckpointCacheExceedsLimit_EvictsOldestNonBaselineTokens()
        {
            var factory = new FakeReplaySessionFactory(CreateFile())
            {
                SupportsCheckpoints = true,
                LastFrame = 2000,
            };
            var owner = new BattleReplaySessionOwner(factory);
            Assert.That(owner.TryStart(CreatePlan(), "replay.record", out var error), Is.True, error);

            Assert.That(owner.SeekToFrame(2000), Is.True);
            var runtime = (CheckpointReplaySessionRuntime)factory.Runtimes[0];

            Assert.That(runtime.ActiveCheckpointCount, Is.EqualTo(64));
            Assert.That(runtime.ReleasedFrames, Is.EqualTo(new[] { 30, 60, 90 }));
            Assert.That(runtime.ActiveCheckpointFrames, Does.Contain(0));
        }

        [Test]
        public void TryStart_WhenBaselineCheckpointCaptureFails_DisposesRuntimeAndExposesFailure()
        {
            var captureFailure = new InvalidOperationException("capture failed");
            var factory = new FakeReplaySessionFactory(CreateFile())
            {
                SupportsCheckpoints = true,
                CheckpointCaptureFailure = captureFailure,
            };
            var owner = new BattleReplaySessionOwner(factory);

            var started = owner.TryStart(CreatePlan(), "replay.record", out var error);

            Assert.That(started, Is.False);
            Assert.That(error, Does.Contain("capture failed"));
            Assert.That(owner.LastFailure, Is.SameAs(captureFailure));
            Assert.That(owner.IsActive, Is.False);
            Assert.That(factory.Runtimes[0].DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void Stop_WhenCheckpointReleaseFails_StillDisposesRuntimeAndReclaimsTokens()
        {
            var releaseFailure = new InvalidOperationException("release failed");
            var factory = new FakeReplaySessionFactory(CreateFile())
            {
                SupportsCheckpoints = true,
                LastFrame = 100,
            };
            var owner = new BattleReplaySessionOwner(factory);
            Assert.That(owner.TryStart(CreatePlan(), "replay.record", out var error), Is.True, error);
            Assert.That(owner.SeekToFrame(35), Is.True);
            var runtime = (CheckpointReplaySessionRuntime)factory.Runtimes[0];
            runtime.ReleaseFailure = releaseFailure;

            owner.Stop();

            Assert.That(runtime.DisposeCount, Is.EqualTo(1));
            Assert.That(runtime.ActiveCheckpointCount, Is.Zero);
            Assert.That(owner.LastFailure, Is.SameAs(releaseFailure));
            Assert.That(runtime.CleanupEvents, Is.EqualTo(new[] { "dispose" }));
        }

        [Test]
        public void Stop_WhenReleaseAndDisposeFail_AggregatesFailuresInCleanupOrder()
        {
            var releaseFailure = new InvalidOperationException("release failed");
            var disposeFailure = new InvalidOperationException("dispose failed");
            var factory = new FakeReplaySessionFactory(CreateFile())
            {
                SupportsCheckpoints = true,
            };
            var owner = new BattleReplaySessionOwner(factory);
            Assert.That(owner.TryStart(CreatePlan(), "replay.record", out var error), Is.True, error);
            var runtime = (CheckpointReplaySessionRuntime)factory.Runtimes[0];
            runtime.ReleaseFailure = releaseFailure;
            runtime.DisposeFailure = disposeFailure;

            owner.Stop();

            var aggregate = owner.LastFailure as AggregateException;
            Assert.That(aggregate, Is.Not.Null);
            Assert.That(aggregate.InnerExceptions, Is.EqualTo(new[] { releaseFailure, disposeFailure }));
            Assert.That(runtime.DisposeCount, Is.EqualTo(1));
            Assert.That(runtime.ActiveCheckpointCount, Is.Zero);
        }

        [Test]
        public void Stop_ReleasesAllCheckpointsBeforeDisposingRuntime()
        {
            var factory = new FakeReplaySessionFactory(CreateFile())
            {
                SupportsCheckpoints = true,
                LastFrame = 100,
            };
            var owner = new BattleReplaySessionOwner(factory);
            Assert.That(owner.TryStart(CreatePlan(), "replay.record", out var error), Is.True, error);
            Assert.That(owner.SeekToFrame(65), Is.True);
            var runtime = (CheckpointReplaySessionRuntime)factory.Runtimes[0];

            owner.Stop();

            Assert.That(runtime.ReleasedFrames, Is.EqualTo(new[] { 0, 30, 60 }));
            Assert.That(runtime.ActiveCheckpointCount, Is.Zero);
            Assert.That(runtime.CleanupEvents, Is.EqualTo(new[]
            {
                "release:0", "release:30", "release:60", "dispose",
            }));
        }

        [Test]
        public void Tick_WithLargeDelta_AdvancesAtMostConfiguredFrameBudget()
        {
            var factory = new FakeReplaySessionFactory(CreateFile()) { LastFrame = 100 };
            var owner = new BattleReplaySessionOwner(factory);
            Assert.That(owner.TryStart(CreatePlan(), "replay.record", out var error), Is.True, error);
            owner.Play();

            Assert.That(owner.Tick(10f), Is.True);

            Assert.That(owner.CurrentFrame, Is.EqualTo(8));
            Assert.That(factory.Runtimes[0].PumpedFrames, Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void Tick_WithNonFiniteDelta_DoesNotAdvance(float deltaSeconds)
        {
            var factory = new FakeReplaySessionFactory(CreateFile()) { LastFrame = 100 };
            var owner = new BattleReplaySessionOwner(factory);
            Assert.That(owner.TryStart(CreatePlan(), "replay.record", out var error), Is.True, error);
            owner.Play();

            Assert.That(owner.Tick(deltaSeconds), Is.False);
            Assert.That(owner.CurrentFrame, Is.Zero);
            Assert.That(owner.IsActive, Is.True);
        }

        [Test]
        public void Tick_WhenRuntimeFails_StopsAndExposesFailure()
        {
            var factory = new FakeReplaySessionFactory(CreateFile()) { LastFrame = 100 };
            var owner = new BattleReplaySessionOwner(factory);
            Assert.That(owner.TryStart(CreatePlan(), "replay.record", out var error), Is.True, error);
            var runtime = factory.Runtimes[0];
            runtime.PumpFailure = new InvalidOperationException("pump failed");
            owner.Play();

            Assert.That(owner.Tick(1f), Is.False);

            Assert.That(owner.IsActive, Is.False);
            Assert.That(owner.LastFailure, Is.SameAs(runtime.PumpFailure));
            Assert.That(runtime.DisposeCount, Is.EqualTo(1));
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
            public Exception CheckpointCaptureFailure { get; set; }
            public bool SupportsCheckpoints { get; set; }
            public int LastFrame { get; set; } = 10;
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

                FakeReplaySessionRuntime runtime;
                if (SupportsCheckpoints)
                {
                    runtime = new CheckpointReplaySessionRuntime(LastFrame)
                    {
                        CaptureFailure = CheckpointCaptureFailure,
                    };
                }
                else
                {
                    runtime = new FakeReplaySessionRuntime(LastFrame);
                }
                Runtimes.Add(runtime);
                return runtime;
            }
        }

        private class FakeReplaySessionRuntime : IBattleReplaySessionRuntime
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
            public Exception PumpFailure { get; set; }
            public int DisposeCount { get; private set; }
            public List<int> PumpedFrames { get; } = new List<int>();
            public List<string> CleanupEvents { get; } = new List<string>();

            public void Play()
            {
                _isPlaying = true;
            }

            public void Pause()
            {
                _isPlaying = false;
            }

            public virtual void PumpAndTick(int frame, float fixedDeltaSeconds)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(FakeReplaySessionRuntime));
                if (PumpFailure != null) throw PumpFailure;
                PumpedFrames.Add(frame);
            }

            public virtual void Dispose()
            {
                DisposeCount++;
                CleanupEvents.Add("dispose");
                _disposed = true;
                _isPlaying = false;
                if (DisposeFailure != null) throw DisposeFailure;
            }
        }

        private sealed class CheckpointReplaySessionRuntime :
            FakeReplaySessionRuntime,
            IBattleReplayCheckpointRuntime
        {
            private readonly HashSet<FakeReplayCheckpoint> _activeCheckpoints =
                new HashSet<FakeReplayCheckpoint>();
            private int _currentFrame;

            public CheckpointReplaySessionRuntime(int lastFrame)
                : base(lastFrame)
            {
            }

            public Exception RestoreFailure { get; set; }
            public Exception CaptureFailure { get; set; }
            public Exception ReleaseFailure { get; set; }
            public List<int> RestoredFrames { get; } = new List<int>();
            public List<int> ReleasedFrames { get; } = new List<int>();
            public int ActiveCheckpointCount => _activeCheckpoints.Count;
            public IEnumerable<int> ActiveCheckpointFrames
            {
                get
                {
                    foreach (var checkpoint in _activeCheckpoints) yield return checkpoint.Frame;
                }
            }

            public override void PumpAndTick(int frame, float fixedDeltaSeconds)
            {
                base.PumpAndTick(frame, fixedDeltaSeconds);
                _currentFrame = frame;
            }

            public IBattleReplayCheckpoint CaptureCheckpoint()
            {
                if (CaptureFailure != null) throw CaptureFailure;
                var checkpoint = new FakeReplayCheckpoint(_currentFrame);
                _activeCheckpoints.Add(checkpoint);
                return checkpoint;
            }

            public void RestoreCheckpoint(IBattleReplayCheckpoint checkpoint)
            {
                if (RestoreFailure != null) throw RestoreFailure;
                var fakeCheckpoint = (FakeReplayCheckpoint)checkpoint;
                _currentFrame = fakeCheckpoint.Frame;
                RestoredFrames.Add(fakeCheckpoint.Frame);
            }

            public void ReleaseCheckpoint(IBattleReplayCheckpoint checkpoint)
            {
                var fakeCheckpoint = (FakeReplayCheckpoint)checkpoint;
                if (!_activeCheckpoints.Contains(fakeCheckpoint)) return;
                if (ReleaseFailure != null) throw ReleaseFailure;
                _activeCheckpoints.Remove(fakeCheckpoint);
                ReleasedFrames.Add(fakeCheckpoint.Frame);
                CleanupEvents.Add($"release:{fakeCheckpoint.Frame}");
            }

            public override void Dispose()
            {
                try
                {
                    base.Dispose();
                }
                finally
                {
                    _activeCheckpoints.Clear();
                }
            }
        }

        private sealed class FakeReplayCheckpoint : IBattleReplayCheckpoint
        {
            public FakeReplayCheckpoint(int frame)
            {
                Frame = frame;
            }

            public int Frame { get; }
        }
    }
}
