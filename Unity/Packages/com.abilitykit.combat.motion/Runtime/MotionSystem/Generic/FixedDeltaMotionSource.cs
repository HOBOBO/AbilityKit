using AbilityKit.Combat.MotionSystem.Constraints;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Core.Mathematics;
using AbilityKit.Core.Pooling;

namespace AbilityKit.Combat.MotionSystem.Generic
{
    public sealed class FixedDeltaMotionSource : IMotionSource, IMotionFinishEventSource, IMotionSnapshotSource, IMotionCollisionPolicySource, IMotionCompletionCollisionPolicySource
    {
        private static readonly ObjectPool<FixedDeltaMotionSource> Pool = Pools.GetPool(
            createFunc: () => new FixedDeltaMotionSource(),
            onRelease: s => s.Reset(),
            defaultCapacity: 64,
            maxSize: 2048,
            collectionCheck: false);

        private int _groupId;
        private MotionStacking _stacking;
        private int _priority;

        private Vec3 _deltaPerSecond;
        private float _timeLeft;
        private bool _active;

        private MotionCollisionConstraints _collisionPolicy;
        private bool _hasCollisionPolicy;
        private MotionCollisionConstraints _completionCollisionPolicy;
        private bool _hasCompletionCollisionPolicy;

        private FixedDeltaMotionSource()
        {
            Reset();
        }

        public FixedDeltaMotionSource(
            in Vec3 deltaPerSecond,
            float duration,
            int priority,
            int groupId,
            MotionStacking stacking,
            MotionCollisionConstraints collisionPolicy = default,
            bool hasCollisionPolicy = false,
            MotionCollisionConstraints completionCollisionPolicy = default,
            bool hasCompletionCollisionPolicy = false)
        {
            Configure(
                in deltaPerSecond,
                duration,
                priority,
                groupId,
                stacking,
                collisionPolicy,
                hasCollisionPolicy,
                completionCollisionPolicy,
                hasCompletionCollisionPolicy);
        }

        public static FixedDeltaMotionSource Rent(
            in Vec3 deltaPerSecond,
            float duration,
            int priority,
            int groupId,
            MotionStacking stacking,
            MotionCollisionConstraints collisionPolicy = default,
            bool hasCollisionPolicy = false,
            MotionCollisionConstraints completionCollisionPolicy = default,
            bool hasCompletionCollisionPolicy = false)
        {
            var source = Pool.Get();
            source.Configure(
                in deltaPerSecond,
                duration,
                priority,
                groupId,
                stacking,
                collisionPolicy,
                hasCollisionPolicy,
                completionCollisionPolicy,
                hasCompletionCollisionPolicy);
            return source;
        }

        public static void Release(FixedDeltaMotionSource source)
        {
            if (source == null) return;
            Pool.Release(source);
        }

        public void Configure(
            in Vec3 deltaPerSecond,
            float duration,
            int priority,
            int groupId,
            MotionStacking stacking,
            MotionCollisionConstraints collisionPolicy = default,
            bool hasCollisionPolicy = false,
            MotionCollisionConstraints completionCollisionPolicy = default,
            bool hasCompletionCollisionPolicy = false)
        {
            _deltaPerSecond = deltaPerSecond;
            _timeLeft = duration;
            _priority = priority;
            _groupId = groupId;
            _stacking = stacking;
            _active = duration > 0f;
            _collisionPolicy = collisionPolicy;
            _hasCollisionPolicy = hasCollisionPolicy;
            _completionCollisionPolicy = completionCollisionPolicy;
            _hasCompletionCollisionPolicy = hasCompletionCollisionPolicy;
        }

        public void Reset()
        {
            _groupId = MotionGroups.Ability;
            _stacking = MotionStacking.ExclusiveHighestPriority;
            _priority = 0;
            _deltaPerSecond = Vec3.Zero;
            _timeLeft = 0f;
            _active = false;
            _collisionPolicy = default;
            _hasCollisionPolicy = false;
            _completionCollisionPolicy = default;
            _hasCompletionCollisionPolicy = false;
        }

        public int GroupId => _groupId;
        public MotionStacking Stacking => _stacking;
        public MotionFinishEvent FinishEvent => MotionFinishEvent.Expired;
        public int Priority => _priority;
        public bool IsActive => _active;

        public float TimeLeft => _timeLeft;

        public bool HasCollisionPolicy => _hasCollisionPolicy;
        public MotionCollisionConstraints CollisionPolicy => _collisionPolicy;
        public bool HasCompletionCollisionPolicy => _hasCompletionCollisionPolicy;
        public MotionCollisionConstraints CompletionCollisionPolicy => _completionCollisionPolicy;

        public void Tick(int id, ref MotionState state, float dt, ref Vec3 outDesiredDelta)
        {
            if (!_active) return;
            if (dt <= 0f) return;

            if (_timeLeft <= MathUtil.Epsilon)
            {
                _timeLeft = 0f;
                _active = false;
                return;
            }

            var step = dt;
            if (step > _timeLeft) step = _timeLeft;
            _timeLeft -= step;

            outDesiredDelta = outDesiredDelta + _deltaPerSecond * step;

            if (_timeLeft <= MathUtil.Epsilon)
            {
                _timeLeft = 0f;
                _active = false;
            }
        }

        public void Cancel()
        {
            _timeLeft = 0f;
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
                TimeLeft = _timeLeft,
                Vector0 = _deltaPerSecond,
            };
            return true;
        }

        public bool ImportSnapshot(in MotionSourceSnapshot snapshot)
        {
            _groupId = snapshot.GroupId;
            _priority = snapshot.Priority;
            _stacking = snapshot.Stacking;
            _active = snapshot.IsActive;
            _timeLeft = snapshot.TimeLeft;
            _deltaPerSecond = snapshot.Vector0;
            return true;
        }
    }
}
