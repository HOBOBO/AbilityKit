using System;
using System.Collections.Generic;
using AbilityKit.Core.Recording.FrameRecord;
using AbilityKit.Game.Battle;

namespace AbilityKit.Game.Flow.Battle.Replay
{
    /// <summary>
    /// Owns a logic-only replay session. Its registry intentionally does not publish the
    /// global debug facade so a replay cannot replace the active battle session's facade.
    /// </summary>
    internal sealed class BattleReplaySessionOwner : IDisposable
    {
        private const int CheckpointIntervalFrames = 30;
        private const int MaxRetainedCheckpoints = 64;
        private const int MaxPlaybackFramesPerTick = 8;
        private const int MaxBufferedPlaybackFrames = 30;

        private readonly IBattleReplaySessionFactory _factory;
        private readonly SortedDictionary<int, IBattleReplayCheckpoint> _checkpoints =
            new SortedDictionary<int, IBattleReplayCheckpoint>();
        private BattleStartPlan _plan;
        private FrameRecordFile _file;
        private IBattleReplaySessionRuntime _runtime;
        private int _currentFrame;
        private float _tickAccumulator;

        public BattleReplaySessionOwner(IBattleLogicSessionRegistry registry = null)
            : this(new DefaultBattleReplaySessionFactory(registry))
        {
        }

        internal BattleReplaySessionOwner(IBattleReplaySessionFactory factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public bool IsActive => _runtime != null && _runtime.IsActive;
        public bool IsPlaying => _runtime != null && _runtime.IsPlaying;
        public int CurrentFrame => _currentFrame;
        public int LastFrame => _runtime?.LastFrame ?? 0;
        public string ReplayPath { get; private set; }
        internal Exception LastFailure { get; private set; }

        public float PlaybackSpeed
        {
            get => _runtime?.PlaybackSpeed ?? 1f;
            set
            {
                if (_runtime != null) _runtime.PlaybackSpeed = NormalizePlaybackSpeed(value);
            }
        }

        public bool TryStart(BattleStartPlan plan, string path, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(path))
            {
                error = "录像路径为空。";
                return false;
            }

            try
            {
                var file = _factory.Load(path);
                if (!BattleReplayManifest.TryCreate(file, out var manifest, out error)) return false;

                var world = plan.World;
                if (!manifest.IsCompatibleWith(world.WorldId, world.WorldType, world.TickRate, out error)) return false;

                var previousCleanupError = StopCore();
                if (previousCleanupError != null)
                {
                    throw new InvalidOperationException(
                        $"Failed to dispose the previous replay session: {previousCleanupError.Message}",
                        previousCleanupError);
                }

                _plan = plan;
                _file = file;
                ReplayPath = path;
                StartSession();
                _runtime.Pause();
                LastFailure = null;
                return true;
            }
            catch (Exception ex)
            {
                var cleanupError = StopCore();
                LastFailure = CombineFailures(
                    "Replay session startup and cleanup both failed.",
                    ex,
                    cleanupError);
                error = FormatFailure("启动独立 Replay Session 失败", ex, cleanupError);
                return false;
            }
        }

        public void Play()
        {
            _runtime?.Play();
        }

        public void Pause()
        {
            _runtime?.Pause();
        }

        public bool Tick(float deltaSeconds)
        {
            if (!IsActive || !_runtime.IsPlaying || _currentFrame >= _runtime.LastFrame)
            {
                if (_runtime != null && _currentFrame >= _runtime.LastFrame) _runtime.Pause();
                return false;
            }

            try
            {
                var fixedDelta = GetFixedDeltaSeconds();
                var safeDelta = float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds)
                    ? 0f
                    : Math.Max(0f, deltaSeconds);
                var maxBufferedSeconds = fixedDelta * MaxBufferedPlaybackFrames;
                _tickAccumulator = Math.Min(
                    maxBufferedSeconds,
                    _tickAccumulator + safeDelta * NormalizePlaybackSpeed(PlaybackSpeed));
                var pendingFrames = (int)Math.Floor(_tickAccumulator / fixedDelta);
                var framesToAdvance = Math.Min(pendingFrames, MaxPlaybackFramesPerTick);
                if (framesToAdvance <= 0) return false;

                _tickAccumulator -= framesToAdvance * fixedDelta;
                var remainingFrames = _runtime.LastFrame - _currentFrame;
                var targetFrame = _currentFrame + Math.Min(framesToAdvance, remainingFrames);
                return AdvanceTo(targetFrame, fixedDelta);
            }
            catch (Exception ex)
            {
                LastFailure = CombineFailures(
                    "Replay tick and cleanup both failed.",
                    ex,
                    StopCore());
                return false;
            }
        }

        public bool SeekToFrame(int targetFrame)
        {
            if (!IsActive) return false;

            targetFrame = Math.Max(0, Math.Min(targetFrame, _runtime.LastFrame));
            var wasPlaying = _runtime.IsPlaying;
            var playbackSpeed = _runtime.PlaybackSpeed;
            var fixedDelta = GetFixedDeltaSeconds();

            try
            {
                if (targetFrame < _currentFrame && !TryRestoreCheckpoint(targetFrame))
                {
                    RestartSession();
                }

                _tickAccumulator = 0f;
                var advanced = AdvanceTo(targetFrame, fixedDelta);
                _runtime.PlaybackSpeed = playbackSpeed;
                if (wasPlaying) _runtime.Play();
                else _runtime.Pause();
                return advanced;
            }
            catch (Exception ex)
            {
                LastFailure = CombineFailures(
                    "Replay seek and cleanup both failed.",
                    ex,
                    StopCore());
                return false;
            }
        }

        public void Stop()
        {
            var cleanupFailure = StopCore();
            if (cleanupFailure != null) LastFailure = cleanupFailure;
        }

        public void Dispose()
        {
            Stop();
        }

        private void RestartSession()
        {
            var previous = _runtime;
            _runtime = null;
            var cleanupFailure = ReleaseCheckpointsAndRuntime(previous);
            if (cleanupFailure != null) throw cleanupFailure;
            StartSession();
        }

        private void StartSession()
        {
            if (_checkpoints.Count != 0)
            {
                throw new InvalidOperationException("Replay checkpoints were not released before session startup.");
            }

            _runtime = _factory.Start(_plan, _file)
                ?? throw new InvalidOperationException("Replay session factory returned no runtime.");
            _currentFrame = 0;
            _tickAccumulator = 0f;
            CaptureCheckpoint(0);
        }

        private bool TryRestoreCheckpoint(int targetFrame)
        {
            if (!(_runtime is IBattleReplayCheckpointRuntime checkpointRuntime)) return false;

            var checkpointFrame = -1;
            IBattleReplayCheckpoint checkpoint = null;
            foreach (var pair in _checkpoints)
            {
                if (pair.Key > targetFrame) break;
                checkpointFrame = pair.Key;
                checkpoint = pair.Value;
            }

            if (checkpoint == null) return false;

            checkpointRuntime.RestoreCheckpoint(checkpoint);
            _currentFrame = checkpointFrame;
            ReleaseCheckpointsAfter(checkpointRuntime, checkpointFrame);
            return true;
        }

        private void CaptureCheckpoint(int frame)
        {
            if (!(_runtime is IBattleReplayCheckpointRuntime checkpointRuntime)) return;
            if (frame != 0 && frame % CheckpointIntervalFrames != 0) return;

            var checkpoint = checkpointRuntime.CaptureCheckpoint()
                ?? throw new InvalidOperationException("Replay checkpoint runtime returned no checkpoint.");
            if (_checkpoints.TryGetValue(frame, out var replaced))
            {
                try
                {
                    checkpointRuntime.ReleaseCheckpoint(replaced);
                    _checkpoints[frame] = checkpoint;
                }
                catch (Exception replaceFailure)
                {
                    Exception rollbackFailure = null;
                    try
                    {
                        checkpointRuntime.ReleaseCheckpoint(checkpoint);
                    }
                    catch (Exception ex)
                    {
                        rollbackFailure = ex;
                    }

                    throw CombineFailures(
                        "Replay checkpoint replacement and rollback both failed.",
                        replaceFailure,
                        rollbackFailure);
                }
            }
            else
            {
                _checkpoints.Add(frame, checkpoint);
            }

            TrimCheckpointCache(checkpointRuntime);
        }

        private bool AdvanceTo(int targetFrame, float fixedDelta)
        {
            var runtime = _runtime;
            if (runtime == null || !runtime.IsActive || targetFrame < _currentFrame) return false;

            while (_currentFrame < targetFrame)
            {
                var frame = _currentFrame + 1;
                runtime.PumpAndTick(frame, fixedDelta);
                _currentFrame = frame;
                CaptureCheckpoint(frame);
            }

            return true;
        }

        private Exception StopCore()
        {
            var runtime = _runtime;
            _runtime = null;
            var cleanupFailure = ReleaseCheckpointsAndRuntime(runtime);
            _file = null;
            _plan = default;
            _currentFrame = 0;
            _tickAccumulator = 0f;
            ReplayPath = string.Empty;
            return cleanupFailure;
        }

        private Exception ReleaseCheckpointsAndRuntime(IBattleReplaySessionRuntime runtime)
        {
            List<Exception> failures = null;
            if (runtime is IBattleReplayCheckpointRuntime checkpointRuntime)
            {
                foreach (var checkpoint in _checkpoints.Values)
                {
                    TryCleanup(() => checkpointRuntime.ReleaseCheckpoint(checkpoint), ref failures);
                }
            }

            _checkpoints.Clear();
            TryCleanup(() => runtime?.Dispose(), ref failures);
            if (failures == null) return null;
            return failures.Count == 1
                ? failures[0]
                : new AggregateException("Failed to release replay session resources.", failures);
        }

        private void ReleaseCheckpointsAfter(IBattleReplayCheckpointRuntime runtime, int frame)
        {
            var framesToRemove = new List<int>();
            foreach (var pair in _checkpoints)
            {
                if (pair.Key > frame) framesToRemove.Add(pair.Key);
            }

            ReleaseCheckpoints(runtime, framesToRemove);
        }

        private void TrimCheckpointCache(IBattleReplayCheckpointRuntime runtime)
        {
            if (_checkpoints.Count <= MaxRetainedCheckpoints) return;

            var framesToRemove = new List<int>();
            foreach (var pair in _checkpoints)
            {
                if (pair.Key == 0) continue;
                framesToRemove.Add(pair.Key);
                if (_checkpoints.Count - framesToRemove.Count <= MaxRetainedCheckpoints) break;
            }

            ReleaseCheckpoints(runtime, framesToRemove);
        }

        private void ReleaseCheckpoints(
            IBattleReplayCheckpointRuntime runtime,
            IList<int> frames)
        {
            List<Exception> failures = null;
            for (var i = 0; i < frames.Count; i++)
            {
                var frame = frames[i];
                if (!_checkpoints.TryGetValue(frame, out var checkpoint)) continue;

                try
                {
                    runtime.ReleaseCheckpoint(checkpoint);
                    _checkpoints.Remove(frame);
                }
                catch (Exception ex)
                {
                    if (failures == null) failures = new List<Exception>();
                    failures.Add(ex);
                }
            }

            if (failures == null) return;
            if (failures.Count == 1) throw failures[0];
            throw new AggregateException("Failed to release replay checkpoints.", failures);
        }

        private static void TryCleanup(Action cleanup, ref List<Exception> failures)
        {
            try
            {
                cleanup?.Invoke();
            }
            catch (Exception ex)
            {
                if (failures == null) failures = new List<Exception>();
                failures.Add(ex);
            }
        }

        private static Exception CombineFailures(
            string message,
            Exception operationFailure,
            Exception cleanupFailure)
        {
            if (operationFailure == null) return cleanupFailure;
            if (cleanupFailure == null) return operationFailure;
            return new AggregateException(message, operationFailure, cleanupFailure);
        }

        private static string FormatFailure(string operation, Exception failure, Exception cleanupFailure)
        {
            if (cleanupFailure == null) return $"{operation}：{failure.Message}";
            return $"{operation}：{failure.Message}；清理 Replay Session 失败：{cleanupFailure.Message}";
        }

        private static float NormalizePlaybackSpeed(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 1f;
            return Math.Max(0.1f, Math.Min(8f, value));
        }

        private float GetFixedDeltaSeconds()
        {
            var tickRate = _plan.World.TickRate;
            return 1f / (tickRate > 0 ? tickRate : 30);
        }
    }
}
