using System;

namespace AbilityKit.Ability.Triggering.Runtime
{
    public sealed class DelayRunningAction : IRunningAction
    {
        private float _remaining;
        private bool _done;
        private bool _disposed;

        public DelayRunningAction(float delaySeconds)
        {
            _remaining = delaySeconds;
        }

        public bool IsDone => _done;

        public void Tick(float deltaTime)
        {
            if (_done) return;
            _remaining -= deltaTime;
            if (_remaining <= 0f) _done = true;
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
