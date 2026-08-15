using AbilityKit.Combat.MotionSystem.Constraints;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Core.Mathematics;
using AbilityKit.Core.Pooling;

namespace AbilityKit.Combat.MotionSystem.Trajectory
{
    public sealed class TrajectoryMotionSource : IMotionSource, IMotionFinishEventSource, IMotionSnapshotSource, IMotionCollisionPolicySource
    {
        private static readonly ObjectPool<TrajectoryMotionSource> Pool = Pools.GetPool(
            createFunc: () => new TrajectoryMotionSource(),
            onRelease: s => s.Reset(),
            defaultCapacity: 64,
            maxSize: 2048,
            collectionCheck: false);

        private ITrajectory3D _trajectory;
        private int _priority;
        private int _groupId;
        private MotionStacking _stacking;
        // Q32.32 raw 时间累计（整数加法无漂移）；轨迹采样接口是 float，采样点单次换算。
        private long _timeRaw;
        private long _durationRaw;
        private bool _active;
        private MotionCollisionConstraints _collisionPolicy;
        private bool _hasCollisionPolicy;

        private TrajectoryMotionSource()
        {
            Reset();
        }

        public TrajectoryMotionSource(
            ITrajectory3D trajectory,
            int priority = 10,
            int groupId = MotionGroups.Ability,
            MotionStacking stacking = MotionStacking.ExclusiveHighestPriority,
            MotionCollisionConstraints collisionPolicy = default,
            bool hasCollisionPolicy = false)
        {
            Configure(trajectory, priority, groupId, stacking, collisionPolicy, hasCollisionPolicy);
        }

        public static TrajectoryMotionSource Rent(
            ITrajectory3D trajectory,
            int priority = 10,
            int groupId = MotionGroups.Ability,
            MotionStacking stacking = MotionStacking.ExclusiveHighestPriority,
            MotionCollisionConstraints collisionPolicy = default,
            bool hasCollisionPolicy = false)
        {
            var source = Pool.Get();
            source.Configure(trajectory, priority, groupId, stacking, collisionPolicy, hasCollisionPolicy);
            return source;
        }

        public static void Release(TrajectoryMotionSource source)
        {
            if (source == null) return;
            Pool.Release(source);
        }

        public void Configure(
            ITrajectory3D trajectory,
            int priority = 10,
            int groupId = MotionGroups.Ability,
            MotionStacking stacking = MotionStacking.ExclusiveHighestPriority,
            MotionCollisionConstraints collisionPolicy = default,
            bool hasCollisionPolicy = false)
        {
            _trajectory = trajectory;
            _priority = priority;
            _groupId = groupId;
            _stacking = stacking;
            _timeRaw = 0L;
            _durationRaw = trajectory != null
                ? DeterministicMathBridge.ToFixed(trajectory.Duration).RawValue
                : 0L;
            _active = trajectory != null && trajectory.Duration > 0f;
            _collisionPolicy = collisionPolicy;
            _hasCollisionPolicy = hasCollisionPolicy;
        }

        public void Reset()
        {
            _trajectory = null;
            _priority = 10;
            _groupId = MotionGroups.Ability;
            _stacking = MotionStacking.ExclusiveHighestPriority;
            _timeRaw = 0L;
            _durationRaw = 0L;
            _active = false;
            _collisionPolicy = default;
            _hasCollisionPolicy = false;
        }

        public int GroupId => _groupId;

        public MotionStacking Stacking => _stacking;

        public MotionFinishEvent FinishEvent => MotionFinishEvent.Arrive;

        public int Priority => _priority;
        public bool IsActive => _active;
        public bool HasCollisionPolicy => _hasCollisionPolicy;
        public MotionCollisionConstraints CollisionPolicy => _collisionPolicy;

        public float Time => Deterministic.Fixed64.FromRaw(_timeRaw).ToSingle();

        public bool IsFinished
        {
            get
            {
                if (!_active || _trajectory == null) return true;
                return _timeRaw >= _durationRaw;
            }
        }

        public void Tick(int id, ref MotionState state, float dt, ref Vec3 outDesiredDelta)
        {
            if (!_active || _trajectory == null) return;
            if (dt <= 0f) return;

            var prev = _trajectory.SamplePosition(Time);
            _timeRaw += DeterministicMathBridge.ToFixed(dt).RawValue;
            if (_timeRaw > _durationRaw) _timeRaw = _durationRaw;
            var next = _trajectory.SamplePosition(Time);

            outDesiredDelta = outDesiredDelta + (next - prev);

            if (_trajectory.TrySampleForward(Time, out var f))
            {
                state.Forward = f;
            }

            if (_timeRaw >= _durationRaw)
            {
                _active = false;
            }
        }

        public void Cancel()
        {
            _active = false;
        }

        public bool ExportSnapshot(out MotionSourceSnapshot snapshot)
        {
            snapshot = new MotionSourceSnapshot
            {
                GroupId = _groupId,
                Priority = _priority,
                Stacking = _stacking,
                IsActive = _active,
                Time = Time,
            };
            return true;
        }

        public bool ImportSnapshot(in MotionSourceSnapshot snapshot)
        {
            _groupId = snapshot.GroupId;
            _priority = snapshot.Priority;
            _stacking = snapshot.Stacking;
            _active = snapshot.IsActive && _trajectory != null;
            _timeRaw = DeterministicMathBridge.ToFixed(snapshot.Time).RawValue;
            if (_trajectory != null)
            {
                _durationRaw = DeterministicMathBridge.ToFixed(_trajectory.Duration).RawValue;
                if (_timeRaw > _durationRaw) _timeRaw = _durationRaw;
            }
            if (_timeRaw < 0L) _timeRaw = 0L;
            return _trajectory != null;
        }
    }
}
