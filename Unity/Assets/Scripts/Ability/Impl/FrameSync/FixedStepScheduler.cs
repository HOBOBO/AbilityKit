using System;
using AbilityKit.Ability.FrameSync;

namespace AbilityKit.Ability.Impl.FrameSync
{
    public sealed class FixedStepScheduler
    {
        private readonly IFrameDriver _driver;
        private readonly float _fixedDelta;
        private float _accumulator;

        public FixedStepScheduler(IFrameDriver driver, float fixedDelta)
        {
            if (fixedDelta <= 0f) throw new ArgumentOutOfRangeException(nameof(fixedDelta));
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _fixedDelta = fixedDelta;
            _accumulator = 0f;
        }

        public FrameIndex Frame => _driver.Frame;

        public void AddTime(float deltaTime)
        {
            _accumulator += deltaTime;
            while (_accumulator >= _fixedDelta)
            {
                _driver.Step(_fixedDelta);
                _accumulator -= _fixedDelta;
            }
        }

        public void StepOnce()
        {
            _driver.Step(_fixedDelta);
        }
    }
}
