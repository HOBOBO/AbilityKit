using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Ability.Impl.Triggering.SummonSpawning
{
    public sealed class SummonSpawnerRunningAction : IRunningAction
    {
        private readonly System.Action _tickSpawnOnce;
        private readonly int _intervalMs;
        private readonly int _durationMs;
        private readonly int _totalCount;

        private float _elapsed;
        private float _acc;
        private int _spawned;
        private bool _done;

        public SummonSpawnerRunningAction(System.Action tickSpawnOnce, int intervalMs, int durationMs, int totalCount)
        {
            _tickSpawnOnce = tickSpawnOnce;
            _intervalMs = intervalMs;
            _durationMs = durationMs;
            _totalCount = totalCount;
            _elapsed = 0f;
            _acc = 0f;
            _spawned = 0;
        }

        public bool IsDone => _done;

        public void Tick(float deltaTime)
        {
            if (_done) return;
            if (_tickSpawnOnce == null) { _done = true; return; }

            _elapsed += deltaTime;
            _acc += deltaTime;

            if (_durationMs > 0 && _elapsed * 1000f >= _durationMs)
            {
                _done = true;
                return;
            }

            var intervalSec = _intervalMs > 0 ? _intervalMs / 1000f : 0.1f;
            if (_acc < intervalSec) return;

            var maxSteps = 4;
            while (_acc >= intervalSec && maxSteps-- > 0)
            {
                _acc -= intervalSec;
                if (_totalCount > 0 && _spawned >= _totalCount)
                {
                    _done = true;
                    return;
                }

                _tickSpawnOnce();
                _spawned++;
            }
        }

        public void Cancel()
        {
            _done = true;
        }

        public void Dispose()
        {
        }
    }
}
