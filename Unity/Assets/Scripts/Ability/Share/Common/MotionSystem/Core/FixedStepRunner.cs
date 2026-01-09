namespace AbilityKit.Ability.Share.Common.MotionSystem.Core
{
    public struct FixedStepRunner
    {
        private float _accumulator;
        private readonly float _step;

        public FixedStepRunner(float step)
        {
            _step = step <= 0f ? 0.02f : step;
            _accumulator = 0f;
        }

        public float Step => _step;

        public int Accumulate(float dt)
        {
            if (dt <= 0f) return 0;
            _accumulator += dt;
            var count = (int)(_accumulator / _step);
            if (count > 0) _accumulator -= count * _step;
            return count;
        }

        public float ConsumeOneStep()
        {
            return _step;
        }

        public void Reset()
        {
            _accumulator = 0f;
        }
    }
}
