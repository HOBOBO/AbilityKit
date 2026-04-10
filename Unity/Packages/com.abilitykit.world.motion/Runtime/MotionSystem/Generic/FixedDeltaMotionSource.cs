using AbilityKit.Core.Common.MotionSystem.Core;
using AbilityKit.Core.Math;

namespace AbilityKit.Core.Common.MotionSystem.Generic
{
    public sealed class FixedDeltaMotionSource : IMotionSource, IMotionFinishEventSource
    {
        private readonly int _groupId;
        private readonly MotionStacking _stacking;
        private readonly int _priority;

        private Vec3 _deltaPerSecond;
        private float _timeLeft;
        private bool _active;

        public FixedDeltaMotionSource(in Vec3 deltaPerSecond, float duration, int priority, int groupId, MotionStacking stacking)
        {
            _deltaPerSecond = deltaPerSecond;
            _timeLeft = duration;
            _priority = priority;
            _groupId = groupId;
            _stacking = stacking;
            _active = duration > 0f;
        }

        public int GroupId => _groupId;
        public MotionStacking Stacking => _stacking;
        public MotionFinishEvent FinishEvent => MotionFinishEvent.Expired;
        public int Priority => _priority;
        public bool IsActive => _active;

        public void Tick(int id, ref MotionState state, float dt, ref Vec3 outDesiredDelta)
        {
            if (!_active) return;
            if (dt <= 0f) return;

            if (_timeLeft <= 0f)
            {
                _active = false;
                return;
            }

            var step = dt;
            if (step > _timeLeft) step = _timeLeft;
            _timeLeft -= dt;

            outDesiredDelta = outDesiredDelta + _deltaPerSecond * step;

            if (_timeLeft <= 0f) _active = false;
        }

        public void Cancel()
        {
            _timeLeft = 0f;
            _active = false;
        }
    }
}
