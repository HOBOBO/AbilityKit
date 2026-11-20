using System;
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
        // Q32.32 raw 剩余时间（整数减法无漂移）；float TimeLeft 是边界视图。
        private long _timeLeftRaw;
        private long _durationRaw;
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
            _timeLeftRaw = DeterministicMathBridge.ToFixed(duration).RawValue;
            _durationRaw = _timeLeftRaw;
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
            _timeLeftRaw = 0L;
            _durationRaw = 0L;
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

        public float TimeLeft => Deterministic.Fixed64.FromRaw(_timeLeftRaw).ToSingle();

        public bool HasCollisionPolicy => _hasCollisionPolicy;
        public MotionCollisionConstraints CollisionPolicy => _collisionPolicy;
        public bool HasCompletionCollisionPolicy => _hasCompletionCollisionPolicy;
        public MotionCollisionConstraints CompletionCollisionPolicy => _completionCollisionPolicy;

        public FixedDeltaMotionExactState CaptureExactState() => new FixedDeltaMotionExactState(
            _durationRaw, _timeLeftRaw, _active, _deltaPerSecond, _groupId, _priority, _stacking,
            _hasCollisionPolicy, _collisionPolicy, _hasCompletionCollisionPolicy, _completionCollisionPolicy);

        public void ValidateExactState(in FixedDeltaMotionExactState state)
        {
            if (state.DurationRaw < 0 || state.TimeLeftRaw < 0 || state.TimeLeftRaw > state.DurationRaw ||
                state.Active != (state.TimeLeftRaw > 0) ||
                !Finite(state.Velocity.X) || !Finite(state.Velocity.Y) || !Finite(state.Velocity.Z) ||
                !Enum.IsDefined(typeof(MotionStacking), state.Stacking) ||
                state.DurationRaw != _durationRaw || state.GroupId != _groupId || state.Priority != _priority ||
                state.Stacking != _stacking || !state.Velocity.Equals(_deltaPerSecond) ||
                state.HasCollisionPolicy != _hasCollisionPolicy || !state.CollisionPolicy.Equals(_collisionPolicy) ||
                state.HasCompletionCollisionPolicy != _hasCompletionCollisionPolicy ||
                !state.CompletionCollisionPolicy.Equals(_completionCollisionPolicy))
                throw new InvalidOperationException("Invalid FixedDelta exact state or changed frozen source configuration.");
        }

        /// <summary>Restores exact time state on the same frozen source without lifecycle callbacks.</summary>
        public void RestoreExactState(in FixedDeltaMotionExactState state)
        {
            ValidateExactState(in state);
            _timeLeftRaw = state.TimeLeftRaw;
            _active = state.Active;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public void Tick(int id, ref MotionState state, float dt, ref Vec3 outDesiredDelta)
        {
            if (!_active) return;
            if (dt <= 0f) return;

            var epsilonRaw = DeterministicMathBridge.Epsilon.RawValue;
            if (_timeLeftRaw <= epsilonRaw)
            {
                _timeLeftRaw = 0L;
                _active = false;
                return;
            }

            var dtRaw = DeterministicMathBridge.ToFixed(dt).RawValue;
            var stepRaw = dtRaw < _timeLeftRaw ? dtRaw : _timeLeftRaw;
            _timeLeftRaw -= stepRaw;

            outDesiredDelta = outDesiredDelta + _deltaPerSecond * Deterministic.Fixed64.FromRaw(stepRaw).ToSingle();

            if (_timeLeftRaw <= epsilonRaw)
            {
                _timeLeftRaw = 0L;
                _active = false;
            }
        }

        public void Cancel()
        {
            _timeLeftRaw = 0L;
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
                TimeLeft = TimeLeft,
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
            _timeLeftRaw = DeterministicMathBridge.ToFixed(snapshot.TimeLeft).RawValue;
            _deltaPerSecond = snapshot.Vector0;
            return true;
        }
    }

    /// <summary>Exact in-process value state. Unlike MotionSourceSnapshot, time never crosses float.</summary>
    public readonly struct FixedDeltaMotionExactState
    {
        public FixedDeltaMotionExactState(long durationRaw, long timeLeftRaw, bool active, Vec3 velocity, int groupId,
            int priority, MotionStacking stacking, bool hasCollisionPolicy, MotionCollisionConstraints collisionPolicy,
            bool hasCompletionCollisionPolicy, MotionCollisionConstraints completionCollisionPolicy)
        {
            DurationRaw = durationRaw; TimeLeftRaw = timeLeftRaw; Active = active; Velocity = velocity; GroupId = groupId;
            Priority = priority; Stacking = stacking; HasCollisionPolicy = hasCollisionPolicy;
            CollisionPolicy = collisionPolicy; HasCompletionCollisionPolicy = hasCompletionCollisionPolicy;
            CompletionCollisionPolicy = completionCollisionPolicy;
        }
        public long DurationRaw { get; }
        public long TimeLeftRaw { get; }
        public bool Active { get; }
        public Vec3 Velocity { get; }
        public int GroupId { get; }
        public int Priority { get; }
        public MotionStacking Stacking { get; }
        public bool HasCollisionPolicy { get; }
        public MotionCollisionConstraints CollisionPolicy { get; }
        public bool HasCompletionCollisionPolicy { get; }
        public MotionCollisionConstraints CompletionCollisionPolicy { get; }
    }
}
