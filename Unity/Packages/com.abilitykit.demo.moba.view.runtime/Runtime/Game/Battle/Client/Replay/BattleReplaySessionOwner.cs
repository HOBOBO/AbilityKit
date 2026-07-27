using System;
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
        private readonly IBattleReplaySessionFactory _factory;
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

        public float PlaybackSpeed
        {
            get => _runtime?.PlaybackSpeed ?? 1f;
            set
            {
                if (_runtime != null) _runtime.PlaybackSpeed = value;
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
                return true;
            }
            catch (Exception ex)
            {
                var cleanupError = StopCore();
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

            var fixedDelta = GetFixedDeltaSeconds();
            _tickAccumulator += Math.Max(0f, deltaSeconds) * PlaybackSpeed;
            var frames = (int)Math.Floor(_tickAccumulator / fixedDelta);
            if (frames <= 0) return false;

            _tickAccumulator -= frames * fixedDelta;
            return AdvanceTo(Math.Min(_currentFrame + frames, _runtime.LastFrame), fixedDelta);
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
                if (targetFrame < _currentFrame)
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
            catch
            {
                StopCore();
                return false;
            }
        }

        public void Stop()
        {
            StopCore();
        }

        public void Dispose()
        {
            Stop();
        }

        private void RestartSession()
        {
            var previous = _runtime;
            _runtime = null;
            previous?.Dispose();
            StartSession();
        }

        private void StartSession()
        {
            _runtime = _factory.Start(_plan, _file)
                ?? throw new InvalidOperationException("Replay session factory returned no runtime.");
            _currentFrame = 0;
            _tickAccumulator = 0f;
        }

        private bool AdvanceTo(int targetFrame, float fixedDelta)
        {
            var runtime = _runtime;
            if (runtime == null || !runtime.IsActive) return false;

            for (var frame = _currentFrame + 1; frame <= targetFrame; frame++)
            {
                runtime.PumpAndTick(frame, fixedDelta);
            }

            _currentFrame = targetFrame;
            return true;
        }

        private Exception StopCore()
        {
            var runtime = _runtime;
            _runtime = null;
            _file = null;
            _plan = default;
            _currentFrame = 0;
            _tickAccumulator = 0f;
            ReplayPath = string.Empty;

            try
            {
                runtime?.Dispose();
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        private static string FormatFailure(string operation, Exception failure, Exception cleanupFailure)
        {
            if (cleanupFailure == null) return $"{operation}：{failure.Message}";
            return $"{operation}：{failure.Message}；清理 Replay Session 失败：{cleanupFailure.Message}";
        }

        private float GetFixedDeltaSeconds()
        {
            var tickRate = _plan.World.TickRate;
            return 1f / (tickRate > 0 ? tickRate : 30);
        }
    }
}
