using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Recording.FrameRecord;
using AbilityKit.Game.Flow;
using AbilityKit.Game.Flow.Battle.Replay;
using AbilityKit.Network.Battle;
using AbilityKit.Protocol.Moba.Generated.GatewayFrameSync;
using NUnit.Framework;
using UnityEngine.TestTools;

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

        [Test]
        public void RecordWriter_DisposeIsIdempotentAndClearsPublishedMirror()
        {
            var runtime = CreateReplayRuntime();
            var context = new BattleContext();
            var writer = new TrackingFrameRecordWriter();
            runtime.BindRecordWriter(context, writer);

            Assert.That(context.InputRecordWriter, Is.SameAs(writer));
            runtime.DisposeRecordWriter();
            runtime.DisposeRecordWriter();

            Assert.That(writer.DisposeCount, Is.EqualTo(1));
            Assert.That(context.InputRecordWriter, Is.Null);
        }

        [Test]
        public void RecordWriter_RebindReplacesWriterAndMovesSameWriterBetweenContexts()
        {
            var runtime = CreateReplayRuntime();
            var firstContext = new BattleContext();
            var secondContext = new BattleContext();
            var firstWriter = new TrackingFrameRecordWriter();
            var secondWriter = new TrackingFrameRecordWriter();
            runtime.BindRecordWriter(firstContext, firstWriter);

            runtime.BindRecordWriter(firstContext, secondWriter);
            runtime.BindRecordWriter(secondContext, secondWriter);

            Assert.That(firstWriter.DisposeCount, Is.EqualTo(1));
            Assert.That(secondWriter.DisposeCount, Is.Zero);
            Assert.That(firstContext.InputRecordWriter, Is.Null);
            Assert.That(secondContext.InputRecordWriter, Is.SameAs(secondWriter));

            runtime.DisposeRecordWriter();
            Assert.That(secondWriter.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void RecordWriter_DisposeDoesNotClearStaleContextReplacement()
        {
            var runtime = CreateReplayRuntime();
            var context = new BattleContext();
            var ownedWriter = new TrackingFrameRecordWriter();
            var replacement = new TrackingFrameRecordWriter();
            runtime.BindRecordWriter(context, ownedWriter);
            context.InputRecordWriter = replacement;

            runtime.DisposeRecordWriter();

            Assert.That(ownedWriter.DisposeCount, Is.EqualTo(1));
            Assert.That(replacement.DisposeCount, Is.Zero);
            Assert.That(context.InputRecordWriter, Is.SameAs(replacement));
        }

        [Test]
        public void RecordWriter_WhenReplacementCleanupFails_DisposesCandidateAndCanRetryOwnerCleanup()
        {
            var replacementFailure = new InvalidOperationException("replacement failed");
            var runtime = CreateReplayRuntime();
            var context = new BattleContext();
            var current = new TrackingFrameRecordWriter { DisposeFailure = replacementFailure };
            var candidate = new TrackingFrameRecordWriter();
            runtime.BindRecordWriter(context, current);

            var thrown = Assert.Throws<InvalidOperationException>(() =>
                runtime.BindRecordWriter(context, candidate));

            Assert.That(thrown, Is.SameAs(replacementFailure));
            Assert.That(current.DisposeCount, Is.EqualTo(1));
            Assert.That(candidate.DisposeCount, Is.EqualTo(1));
            Assert.That(context.InputRecordWriter, Is.SameAs(current));

            current.DisposeFailure = null;
            runtime.DisposeRecordWriter();
            Assert.That(current.DisposeCount, Is.EqualTo(2));
            Assert.That(context.InputRecordWriter, Is.Null);
        }

        [Test]
        public void RecordWriter_WhenCurrentAndCandidateCleanupFail_AggregatesInOwnershipOrder()
        {
            var currentFailure = new InvalidOperationException("current failed");
            var candidateFailure = new InvalidOperationException("candidate failed");
            var runtime = CreateReplayRuntime();
            var context = new BattleContext();
            var current = new TrackingFrameRecordWriter { DisposeFailure = currentFailure };
            var candidate = new TrackingFrameRecordWriter { DisposeFailure = candidateFailure };
            runtime.BindRecordWriter(context, current);

            var thrown = Assert.Throws<AggregateException>(() =>
                runtime.BindRecordWriter(context, candidate));

            Assert.That(thrown.InnerExceptions, Is.EqualTo(new[] { currentFailure, candidateFailure }));
            Assert.That(current.DisposeCount, Is.EqualTo(1));
            Assert.That(candidate.DisposeCount, Is.EqualTo(1));
            Assert.That(context.InputRecordWriter, Is.SameAs(current));
        }

        [Test]
        public void RecordWriter_SeparateRuntimesOwnIndependentResources()
        {
            var firstRuntime = CreateReplayRuntime();
            var secondRuntime = CreateReplayRuntime();
            var firstContext = new BattleContext();
            var secondContext = new BattleContext();
            var firstWriter = new TrackingFrameRecordWriter();
            var secondWriter = new TrackingFrameRecordWriter();
            firstRuntime.BindRecordWriter(firstContext, firstWriter);
            secondRuntime.BindRecordWriter(secondContext, secondWriter);

            firstRuntime.DisposeRecordWriter();

            Assert.That(firstWriter.DisposeCount, Is.EqualTo(1));
            Assert.That(secondWriter.DisposeCount, Is.Zero);
            Assert.That(firstContext.InputRecordWriter, Is.Null);
            Assert.That(secondContext.InputRecordWriter, Is.SameAs(secondWriter));

            secondRuntime.DisposeRecordWriter();
            Assert.That(secondWriter.DisposeCount, Is.EqualTo(1));
        }

        private static BattleReplayRuntime CreateReplayRuntime()
        {
            return new BattleReplayRuntime(new BattleReplaySessionOwner(
                new FakeReplaySessionFactory(CreateFile())));
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

        private sealed class TrackingFrameRecordWriter : IFrameRecordWriter
        {
            public Exception DisposeFailure { get; set; }
            public int DisposeCount { get; private set; }

            public void Append(in PlayerInputCommand cmd)
            {
            }

            public void AppendStateHash(int frame, int version, uint hash)
            {
            }

            public void AppendSnapshot(int frame, int opCode, byte[] payload)
            {
            }

            public void Dispose()
            {
                DisposeCount++;
                if (DisposeFailure != null) throw DisposeFailure;
            }
        }

        public sealed class SpectatorSessionRuntimeTests
        {
            [UnityTest]
            public IEnumerator StartAsync_PublishesOnlyAfterSubscribeCompletes()
            {
                yield return AwaitTask(StartAsync_PublishesOnlyAfterSubscribeCompletesCore());
            }

            private static async Task StartAsync_PublishesOnlyAfterSubscribeCompletesCore()
            {
                var client = new ControllableNetworkClient();
                var world = new TrackingWorld("spectator-a");
                var runtime = new SpectatorSessionRuntime();

                var startTask = runtime.StartAsync(client, 17UL, () => world);
                var request = await client.WaitForRequestAsync();

                Assert.That(request.OpCode, Is.EqualTo(OpCodes.SpectatorSubscribe));
                Assert.That(runtime.IsStarting, Is.True);
                Assert.That(runtime.Driver, Is.Null);
                Assert.That(client.PushSubscriberCount, Is.EqualTo(1));

                request.Complete(CreateMetricsResponse(worldId: 71UL, currentFrame: 0));
                await AwaitWithTimeoutAsync(startTask);

                Assert.That(runtime.IsStarting, Is.False);
                Assert.That(runtime.IsSpectating, Is.True);
                Assert.That(runtime.World, Is.SameAs(world));

                runtime.Stop();
                Assert.That(world.DisposeCount, Is.EqualTo(1));
                Assert.That(client.PushSubscriberCount, Is.Zero);
            }

            [UnityTest]
            public IEnumerator Stop_CancelsPendingSubscribeAndRejectsLateCompletion()
            {
                yield return AwaitTask(Stop_CancelsPendingSubscribeAndRejectsLateCompletionCore());
            }

            private static async Task Stop_CancelsPendingSubscribeAndRejectsLateCompletionCore()
            {
                var client = new ControllableNetworkClient();
                var world = new TrackingWorld("spectator-cancel");
                var runtime = new SpectatorSessionRuntime();

                var startTask = runtime.StartAsync(client, 18UL, () => world);
                var request = await client.WaitForRequestAsync();

                runtime.Stop();

                Assert.That(request.CancellationToken.IsCancellationRequested, Is.True);
                Assert.That(client.PushSubscriberCount, Is.Zero);
                request.Complete(CreateMetricsResponse(worldId: 72UL, currentFrame: 0));

                Assert.That(await IsCanceledAsync(startTask), Is.True);
                Assert.That(runtime.Driver, Is.Null);
                Assert.That(world.DisposeCount, Is.Zero);
            }

            [UnityTest]
            public IEnumerator StartAsync_WhenReplaced_IgnoresStaleCompletion()
            {
                yield return AwaitTask(StartAsync_WhenReplaced_IgnoresStaleCompletionCore());
            }

            private static async Task StartAsync_WhenReplaced_IgnoresStaleCompletionCore()
            {
                var staleClient = new ControllableNetworkClient();
                var activeClient = new ControllableNetworkClient();
                var staleWorld = new TrackingWorld("spectator-stale");
                var activeWorld = new TrackingWorld("spectator-active");
                var runtime = new SpectatorSessionRuntime();

                var staleTask = runtime.StartAsync(staleClient, 19UL, () => staleWorld);
                var staleRequest = await staleClient.WaitForRequestAsync();
                var activeTask = runtime.StartAsync(activeClient, 20UL, () => activeWorld);
                var activeRequest = await activeClient.WaitForRequestAsync();

                activeRequest.Complete(CreateMetricsResponse(worldId: 73UL, currentFrame: 0));
                await AwaitWithTimeoutAsync(activeTask);
                staleRequest.Complete(CreateMetricsResponse(worldId: 74UL, currentFrame: 0));

                Assert.That(await IsCanceledAsync(staleTask), Is.True);
                Assert.That(runtime.World, Is.SameAs(activeWorld));
                Assert.That(staleWorld.DisposeCount, Is.Zero);
                Assert.That(staleClient.PushSubscriberCount, Is.Zero);
                Assert.That(activeClient.PushSubscriberCount, Is.EqualTo(1));

                runtime.Stop();
            }

            [UnityTest]
            public IEnumerator Stop_WhenWorldDisposeFails_RetainsOwnerForRetry()
            {
                yield return AwaitTask(Stop_WhenWorldDisposeFails_RetainsOwnerForRetryCore());
            }

            private static async Task Stop_WhenWorldDisposeFails_RetainsOwnerForRetryCore()
            {
                var client = new ControllableNetworkClient();
                var world = new TrackingWorld("spectator-retry")
                {
                    DisposeFailure = new InvalidOperationException("world dispose failed"),
                };
                var runtime = new SpectatorSessionRuntime();

                var startTask = runtime.StartAsync(client, 21UL, () => world);
                var request = await client.WaitForRequestAsync();
                request.Complete(CreateMetricsResponse(worldId: 75UL, currentFrame: 0));
                await AwaitWithTimeoutAsync(startTask);

                Assert.Throws<InvalidOperationException>(() => runtime.Stop());
                Assert.That(runtime.World, Is.SameAs(world));
                Assert.That(world.DisposeCount, Is.EqualTo(1));
                Assert.That(client.PushSubscriberCount, Is.Zero);

                world.DisposeFailure = null;
                runtime.Stop();

                Assert.That(world.DisposeCount, Is.EqualTo(2));
                Assert.That(runtime.World, Is.Null);
            }

            [UnityTest]
            public IEnumerator Stop_CancelsPendingCatchUpAndDisposesCandidateWorld()
            {
                yield return AwaitTask(Stop_CancelsPendingCatchUpAndDisposesCandidateWorldCore());
            }

            private static async Task Stop_CancelsPendingCatchUpAndDisposesCandidateWorldCore()
            {
                var client = new ControllableNetworkClient();
                var world = new TrackingWorld("spectator-catch-up");
                var runtime = new SpectatorSessionRuntime();

                var startTask = runtime.StartAsync(client, 22UL, () => world);
                var subscribeRequest = await client.WaitForRequestAsync();
                subscribeRequest.Complete(CreateMetricsResponse(worldId: 76UL, currentFrame: 12));
                var catchUpRequest = await client.WaitForRequestAsync();

                Assert.That(catchUpRequest.OpCode, Is.EqualTo(OpCodes.CatchUpRequest));
                Assert.That(runtime.Driver, Is.Null);
                Assert.That(world.DisposeCount, Is.Zero);

                runtime.Stop();
                catchUpRequest.Complete(Array.Empty<byte>());

                Assert.That(catchUpRequest.CancellationToken.IsCancellationRequested, Is.True);
                Assert.That(await IsCanceledAsync(startTask), Is.True);
                Assert.That(world.DisposeCount, Is.EqualTo(1));
                Assert.That(runtime.World, Is.Null);
                Assert.That(client.PushSubscriberCount, Is.Zero);
            }

            [UnityTest]
            public IEnumerator StartAsync_WhenWorldFactoryFails_RollsBackSubscription()
            {
                yield return AwaitTask(StartAsync_WhenWorldFactoryFails_RollsBackSubscriptionCore());
            }

            private static async Task StartAsync_WhenWorldFactoryFails_RollsBackSubscriptionCore()
            {
                var client = new ControllableNetworkClient();
                var runtime = new SpectatorSessionRuntime();
                var failure = new InvalidOperationException("world factory failed");

                var startTask = runtime.StartAsync(client, 23UL, () => throw failure);
                var request = await client.WaitForRequestAsync();
                request.Complete(CreateMetricsResponse(worldId: 77UL, currentFrame: 0));

                Exception thrown = null;
                try
                {
                    await AwaitWithTimeoutAsync(startTask);
                }
                catch (Exception ex)
                {
                    thrown = ex;
                }

                Assert.That(thrown, Is.SameAs(failure));
                Assert.That(runtime.IsStarting, Is.False);
                Assert.That(runtime.Driver, Is.Null);
                Assert.That(client.PushSubscriberCount, Is.Zero);
            }

            [UnityTest]
            public IEnumerator Stop_AfterSuccessfulStart_IsIdempotent()
            {
                yield return AwaitTask(Stop_AfterSuccessfulStart_IsIdempotentCore());
            }

            private static async Task Stop_AfterSuccessfulStart_IsIdempotentCore()
            {
                var client = new ControllableNetworkClient();
                var world = new TrackingWorld("spectator-idempotent");
                var runtime = new SpectatorSessionRuntime();

                var startTask = runtime.StartAsync(client, 24UL, () => world);
                var request = await client.WaitForRequestAsync();
                request.Complete(CreateMetricsResponse(worldId: 78UL, currentFrame: 0));
                await AwaitWithTimeoutAsync(startTask);

                runtime.Stop();
                runtime.Stop();
                runtime.Dispose();

                Assert.That(world.DisposeCount, Is.EqualTo(1));
                Assert.That(runtime.Driver, Is.Null);
                Assert.That(client.PushSubscriberCount, Is.Zero);
            }

            [UnityTest]
            public IEnumerator SeparateRuntimes_OwnIndependentClientsAndWorlds()
            {
                yield return AwaitTask(SeparateRuntimes_OwnIndependentClientsAndWorldsCore());
            }

            private static async Task SeparateRuntimes_OwnIndependentClientsAndWorldsCore()
            {
                var firstClient = new ControllableNetworkClient();
                var secondClient = new ControllableNetworkClient();
                var firstWorld = new TrackingWorld("spectator-one");
                var secondWorld = new TrackingWorld("spectator-two");
                var first = new SpectatorSessionRuntime();
                var second = new SpectatorSessionRuntime();

                var firstTask = first.StartAsync(firstClient, 25UL, () => firstWorld);
                var secondTask = second.StartAsync(secondClient, 26UL, () => secondWorld);
                var firstRequest = await firstClient.WaitForRequestAsync();
                var secondRequest = await secondClient.WaitForRequestAsync();
                firstRequest.Complete(CreateMetricsResponse(worldId: 79UL, currentFrame: 0));
                secondRequest.Complete(CreateMetricsResponse(worldId: 80UL, currentFrame: 0));
                await AwaitWithTimeoutAsync(Task.WhenAll(firstTask, secondTask));

                first.Stop();

                Assert.That(firstWorld.DisposeCount, Is.EqualTo(1));
                Assert.That(firstClient.PushSubscriberCount, Is.Zero);
                Assert.That(secondWorld.DisposeCount, Is.Zero);
                Assert.That(secondClient.PushSubscriberCount, Is.EqualTo(1));
                Assert.That(second.World, Is.SameAs(secondWorld));

                second.Stop();
            }

            private static byte[] CreateMetricsResponse(ulong worldId, int currentFrame)
            {
                var metrics = new WireFrameSyncMetrics(
                    roomId: 1UL,
                    worldId: worldId,
                    battleId: "spectator-test",
                    currentFrame: currentFrame,
                    tickRate: 30,
                    observerCount: 1,
                    avgTickDeltaMs: 0d,
                    lastTickDeltaMs: 0d,
                    effectiveHz: 30d,
                    totalFramesReceived: 0,
                    catchUpHistoryFrames: 0,
                    recordingFrameCount: 0,
                    uptimeSeconds: 0L);
                var payload = WireCustomBinary.Serialize(metrics);
                if (payload.Array == null) return Array.Empty<byte>();

                var result = new byte[payload.Count];
                Array.Copy(payload.Array, payload.Offset, result, 0, payload.Count);
                return result;
            }

            private static IEnumerator AwaitTask(Task task)
            {
                while (!task.IsCompleted)
                {
                    yield return null;
                }

                if (task.IsFaulted)
                {
                    ExceptionDispatchInfo.Capture(task.Exception.GetBaseException()).Throw();
                }

                if (task.IsCanceled)
                {
                    throw new OperationCanceledException();
                }
            }

            private static async Task AwaitWithTimeoutAsync(Task task)
            {
                var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5)));
                Assert.That(completed, Is.SameAs(task), "Timed out waiting for spectator operation.");
                await task;
            }

            private static async Task<bool> IsCanceledAsync(Task task)
            {
                try
                {
                    await AwaitWithTimeoutAsync(task);
                    return false;
                }
                catch (OperationCanceledException)
                {
                    return true;
                }
            }

            private sealed class ControllableNetworkClient : INetworkClient
            {
                private readonly Queue<PendingRequest> _requests = new Queue<PendingRequest>();
                private readonly Queue<TaskCompletionSource<PendingRequest>> _waiters =
                    new Queue<TaskCompletionSource<PendingRequest>>();
                private Action<uint, byte[]> _onServerPush;

                public bool IsConnected => true;
                public int PushSubscriberCount { get; private set; }

                public event Action OnConnected;
                public event Action<string> OnDisconnected;
                public event Action<Exception> OnError;

                public event Action<uint, byte[]> OnServerPush
                {
                    add
                    {
                        _onServerPush += value;
                        PushSubscriberCount++;
                    }
                    remove
                    {
                        _onServerPush -= value;
                        PushSubscriberCount--;
                    }
                }

                public void Connect(string host, int port)
                {
                    OnConnected?.Invoke();
                }

                public void Disconnect()
                {
                    OnDisconnected?.Invoke("test");
                }

                public Task<byte[]> SendRequestAsync(
                    uint opCode,
                    byte[] payload,
                    CancellationToken cancellationToken = default)
                {
                    var request = new PendingRequest(opCode, payload, cancellationToken);
                    if (_waiters.Count > 0)
                    {
                        _waiters.Dequeue().TrySetResult(request);
                    }
                    else
                    {
                        _requests.Enqueue(request);
                    }

                    return request.Response.Task;
                }

                public Task SendServerPushAsync(
                    uint opCode,
                    byte[] payload,
                    CancellationToken cancellationToken = default)
                {
                    _onServerPush?.Invoke(opCode, payload);
                    return Task.CompletedTask;
                }

                public async Task<PendingRequest> WaitForRequestAsync()
                {
                    if (_requests.Count > 0) return _requests.Dequeue();

                    var waiter = new TaskCompletionSource<PendingRequest>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    _waiters.Enqueue(waiter);
                    return await waiter.Task;
                }

                public void Dispose()
                {
                    while (_waiters.Count > 0)
                    {
                        _waiters.Dequeue().TrySetCanceled();
                    }

                    while (_requests.Count > 0)
                    {
                        _requests.Dequeue().Complete(Array.Empty<byte>());
                    }
                }
            }

            private sealed class PendingRequest
            {
                internal PendingRequest(uint opCode, byte[] payload, CancellationToken cancellationToken)
                {
                    OpCode = opCode;
                    Payload = payload;
                    CancellationToken = cancellationToken;
                    Response = new TaskCompletionSource<byte[]>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                }

                internal uint OpCode { get; }
                internal byte[] Payload { get; }
                internal CancellationToken CancellationToken { get; }
                internal TaskCompletionSource<byte[]> Response { get; }

                internal void Complete(byte[] response)
                {
                    Response.TrySetResult(response);
                }
            }

            private sealed class TrackingWorld : IWorld
            {
                internal TrackingWorld(string id)
                {
                    Id = new WorldId(id);
                }

                public WorldId Id { get; }
                public string WorldType => "spectator-test";
                public IWorldResolver Services { get; } = new EmptyWorldResolver();
                public int DisposeCount { get; private set; }
                public Exception DisposeFailure { get; set; }

                public void Initialize()
                {
                }

                public void Tick(float deltaTime)
                {
                }

                public void Dispose()
                {
                    DisposeCount++;
                    if (DisposeFailure != null) throw DisposeFailure;
                }
            }

            private sealed class EmptyWorldResolver : IWorldResolver
            {
                public object Resolve(Type serviceType)
                {
                    throw new KeyNotFoundException(serviceType.FullName);
                }

                public T Resolve<T>()
                {
                    return (T)Resolve(typeof(T));
                }

                public bool TryResolve(Type serviceType, out object instance)
                {
                    instance = null;
                    return false;
                }

                public bool TryResolve<T>(out T instance)
                {
                    instance = default;
                    return false;
                }
            }
        }
    }
}
