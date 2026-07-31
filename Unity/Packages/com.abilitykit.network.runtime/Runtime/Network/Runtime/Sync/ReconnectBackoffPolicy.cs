using System;

namespace AbilityKit.Network.Runtime.Sync
{
    /// <summary>
    /// 断线重连的退避策略（纯函数，无会话依赖）。
    /// 指数退避：1s、2s、4s、8s……封顶 15s。
    ///
    /// 框架级共享策略：MOBA（BattleSessionFeature.Reconnect）与
    /// Shooter（FastReconnect 路径）等 demo 统一使用，
    /// 避免每个示例各自实现一套重连节奏。
    /// </summary>
    public static class ReconnectBackoffPolicy
    {
        public const int MaxAttempts = 10;
        public const float BaseDelaySeconds = 1f;
        public const float MaxDelaySeconds = 15f;

        public static float ResolveDelay(int attempts)
        {
            if (attempts < 0) attempts = 0;
            var delay = BaseDelaySeconds * (1 << attempts);
            return delay > MaxDelaySeconds ? MaxDelaySeconds : delay;
        }
    }

    /// <summary>
    /// Stateful retry cadence for reconnect and session-recovery workflows.
    /// The scheduler owns no transport or callback and is advanced by its host tick.
    /// </summary>
    public sealed class ReconnectAttemptScheduler
    {
        private readonly int _maxAttempts;
        private readonly Func<int, float> _resolveDelay;
        private float _elapsedSeconds;

        public ReconnectAttemptScheduler(
            int maxAttempts = ReconnectBackoffPolicy.MaxAttempts,
            Func<int, float> resolveDelay = null)
        {
            if (maxAttempts <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxAttempts));
            }

            _maxAttempts = maxAttempts;
            _resolveDelay = resolveDelay ?? ReconnectBackoffPolicy.ResolveDelay;
        }

        public bool IsPending { get; private set; }

        public bool IsExhausted { get; private set; }

        public int AttemptsStarted { get; private set; }

        public int MaxAttempts => _maxAttempts;

        public int NextAttemptNumber => IsExhausted ? 0 : AttemptsStarted + 1;

        public float NextDelaySeconds => IsPending
            ? Math.Max(0f, _resolveDelay(AttemptsStarted))
            : 0f;

        public float RemainingDelaySeconds => IsPending
            ? Math.Max(0f, NextDelaySeconds - _elapsedSeconds)
            : 0f;

        /// <summary>
        /// Starts the retry loop. Repeated requests while pending do not postpone the next attempt.
        /// </summary>
        public bool Request()
        {
            if (IsExhausted)
            {
                return false;
            }

            if (!IsPending)
            {
                IsPending = true;
                _elapsedSeconds = 0f;
            }

            return true;
        }

        /// <summary>
        /// Advances the cadence and consumes at most one due attempt.
        /// </summary>
        public bool TryTakeAttempt(float deltaTime, out int attemptNumber)
        {
            attemptNumber = 0;
            if (!IsPending)
            {
                return false;
            }

            _elapsedSeconds += Math.Max(0f, deltaTime);
            if (_elapsedSeconds < NextDelaySeconds)
            {
                return false;
            }

            _elapsedSeconds = 0f;
            AttemptsStarted++;
            attemptNumber = AttemptsStarted;

            if (AttemptsStarted >= _maxAttempts)
            {
                IsPending = false;
                IsExhausted = true;
            }

            return true;
        }

        public void Reset()
        {
            IsPending = false;
            IsExhausted = false;
            AttemptsStarted = 0;
            _elapsedSeconds = 0f;
        }
    }
}
