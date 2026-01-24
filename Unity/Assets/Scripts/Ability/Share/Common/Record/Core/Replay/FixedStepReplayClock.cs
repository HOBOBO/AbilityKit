using AbilityKit.Ability.FrameSync;

namespace AbilityKit.Ability.Share.Common.Record.Core
{
    public sealed class FixedStepReplayClock : IReplayClock
    {
        private readonly float _fixedDelta;
        private float _acc;
        private FrameIndex _frame;

        public FixedStepReplayClock(float fixedDelta)
        {
            _fixedDelta = fixedDelta;
            _acc = 0f;
            _frame = new FrameIndex(0);
            Speed = 1f;
        }

        public FrameIndex CurrentFrame => _frame;

        public float Speed { get; set; }

        public void Reset(FrameIndex startFrame)
        {
            _acc = 0f;
            _frame = startFrame;
        }

        public bool TryConsume(float deltaTime, out FrameIndex nextFrame)
        {
            _acc += deltaTime * Speed;
            if (_acc < _fixedDelta)
            {
                nextFrame = default;
                return false;
            }

            _acc -= _fixedDelta;
            _frame = new FrameIndex(_frame.Value + 1);
            nextFrame = _frame;
            return true;
        }
    }
}
