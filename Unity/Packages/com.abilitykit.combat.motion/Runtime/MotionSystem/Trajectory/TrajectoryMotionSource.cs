using AbilityKit.Core.Common.MotionSystem.Core;
using AbilityKit.Core.Math;

namespace AbilityKit.Core.Common.MotionSystem.Trajectory
{
    public sealed class TrajectoryMotionSource : IMotionSource, IMotionFinishEventSource
    {
        private readonly ITrajectory3D _trajectory;
        private readonly int _priority;
        private readonly int _groupId;
        private readonly MotionStacking _stacking;
        private float _time;
        private bool _active;

        public TrajectoryMotionSource(ITrajectory3D trajectory, int priority = 10, int groupId = MotionGroups.Ability, MotionStacking stacking = MotionStacking.ExclusiveHighestPriority)
        {
            _trajectory = trajectory;
            _priority = priority;
            _groupId = groupId;
            _stacking = stacking;
            _time = 0f;
            _active = trajectory != null;
        }

        public int GroupId => _groupId;

        public MotionStacking Stacking => _stacking;

        public MotionFinishEvent FinishEvent => MotionFinishEvent.Arrive;

        public int Priority => _priority;
        public bool IsActive => _active;

        public bool IsFinished
        {
            get
            {
                if (!_active || _trajectory == null) return true;
                return _time >= _trajectory.Duration;
            }
        }

        public void Tick(int id, ref MotionState state, float dt, ref Vec3 outDesiredDelta)
        {
            if (!_active || _trajectory == null) return;

            var prev = _trajectory.SamplePosition(_time);
            _time += dt;
            if (_time > _trajectory.Duration) _time = _trajectory.Duration;
            var next = _trajectory.SamplePosition(_time);

            outDesiredDelta = outDesiredDelta + (next - prev);

            if (_trajectory.TrySampleForward(_time, out var f))
            {
                state.Forward = f;
            }

            if (_time >= _trajectory.Duration)
            {
                _active = false;
            }
        }

        public void Cancel()
        {
            _active = false;
        }
    }
}
