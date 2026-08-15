using System;
using AbilityKit.Core.Mathematics;

namespace AbilityKit.Ability.Triggering.Runtime
{
    public sealed class DelayRunningAction : IRunningAction
    {
        // Q32.32 raw 倒计时（整数减法无漂移）。
        private long _remainingRaw;
        private bool _done;
        private bool _disposed;

        public DelayRunningAction(float delaySeconds)
        {
            _remainingRaw = DeterministicMathBridge.ToFixed(delaySeconds).RawValue;
        }

        public bool IsDone => _done;

        public void Tick(float deltaTime)
        {
            if (_done) return;
            _remainingRaw -= DeterministicMathBridge.ToFixed(deltaTime).RawValue;
            if (_remainingRaw <= 0L) _done = true;
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
