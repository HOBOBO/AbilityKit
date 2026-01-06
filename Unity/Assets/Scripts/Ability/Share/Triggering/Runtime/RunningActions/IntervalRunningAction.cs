using System;

namespace AbilityKit.Ability.Triggering.Runtime
{
    public sealed class IntervalRunningAction : IRunningAction
    {
        private readonly Action _tick;
        private readonly float _interval;
        private float _elapsed;
        private float _duration;
        private bool _done;
        private bool _disposed;

        public IntervalRunningAction(float intervalSeconds, float durationSeconds, Action tick)
        {
            if (intervalSeconds <= 0f) throw new ArgumentException("intervalSeconds must be > 0", nameof(intervalSeconds));
            _interval = intervalSeconds;
            _duration = durationSeconds;
            _tick = tick;
        }

        public bool IsDone => _done;

        public void Tick(float deltaTime)
        {
            if (_done) return;

            _duration -= deltaTime;
            if (_duration <= 0f)
            {
                _done = true;
                return;
            }

            _elapsed += deltaTime;
            while (_elapsed >= _interval)
            {
                _elapsed -= _interval;
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
