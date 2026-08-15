using System;
using AbilityKit.Core.Mathematics;

namespace AbilityKit.Ability.Triggering.Runtime
{
    public sealed class IntervalRunningAction : IRunningAction
    {
        private readonly Action _tick;
        // Q32.32 raw 计时（整数加减无漂移）。
        private readonly long _intervalRaw;
        private long _elapsedRaw;
        private long _durationRaw;
        private bool _done;
        private bool _disposed;

        public IntervalRunningAction(float intervalSeconds, float durationSeconds, Action tick)
        {
            if (intervalSeconds <= 0f) throw new ArgumentException("intervalSeconds must be > 0", nameof(intervalSeconds));
            _intervalRaw = DeterministicMathBridge.ToFixed(intervalSeconds).RawValue;
            _durationRaw = DeterministicMathBridge.ToFixed(durationSeconds).RawValue;
            _tick = tick;
        }

        public bool IsDone => _done;

        public void Tick(float deltaTime)
        {
            if (_done) return;

            var dtRaw = DeterministicMathBridge.ToFixed(deltaTime).RawValue;
            _durationRaw -= dtRaw;
            if (_durationRaw <= 0L)
            {
                _done = true;
                return;
            }

            _elapsedRaw += dtRaw;
            while (_elapsedRaw >= _intervalRaw)
            {
                _elapsedRaw -= _intervalRaw;
                _tick?.Invoke();
                if (_done) return;
            }
        }

        public void Cancel()
        {
            _done = true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
        }
    }
}
