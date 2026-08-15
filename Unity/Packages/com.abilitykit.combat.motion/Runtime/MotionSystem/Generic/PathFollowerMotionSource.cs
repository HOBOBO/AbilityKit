using System;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Core.Mathematics;
using AbilityKit.Core.Pooling;

namespace AbilityKit.Combat.MotionSystem.Generic
{
    public sealed class PathFollowerMotionSource : IMotionSource, IMotionFinishEventSource, IMotionSnapshotSource
    {
        private static readonly ObjectPool<PathFollowerMotionSource> Pool = Pools.GetPool(
            createFunc: () => new PathFollowerMotionSource(),
            onRelease: s => s.Reset(),
            defaultCapacity: 64,
            maxSize: 2048,
            collectionCheck: false);

        private Vec3[] _points;
        private float _speed;
        private float _arriveEpsilon;

        private int _groupId;
        private MotionStacking _stacking;
        private int _priority;

        private int _index;
        private bool _active;

        private PathFollowerMotionSource()
        {
            Reset();
        }

        public PathFollowerMotionSource(Vec3[] points, float speed, float arriveEpsilon = 0.05f, int priority = 10, int groupId = MotionGroups.Path, MotionStacking stacking = MotionStacking.ExclusiveHighestPriority)
        {
            Configure(points, speed, arriveEpsilon, priority, groupId, stacking);
        }

        public static PathFollowerMotionSource Rent(Vec3[] points, float speed, float arriveEpsilon = 0.05f, int priority = 10, int groupId = MotionGroups.Path, MotionStacking stacking = MotionStacking.ExclusiveHighestPriority)
        {
            var source = Pool.Get();
            source.Configure(points, speed, arriveEpsilon, priority, groupId, stacking);
            return source;
        }

        public static void Release(PathFollowerMotionSource source)
        {
            if (source == null) return;
            Pool.Release(source);
        }

        public void Configure(Vec3[] points, float speed, float arriveEpsilon = 0.05f, int priority = 10, int groupId = MotionGroups.Path, MotionStacking stacking = MotionStacking.ExclusiveHighestPriority)
        {
            _points = CopyPoints(points);
            _speed = speed <= 0f ? 0.0001f : speed;
            _arriveEpsilon = arriveEpsilon <= 0f ? 0.01f : arriveEpsilon;
            _priority = priority;
            _groupId = groupId;
            _stacking = stacking;
            _index = 0;
            _active = _points.Length > 0;
        }

        public void Reset()
        {
            _points = Array.Empty<Vec3>();
            _speed = 0.0001f;
            _arriveEpsilon = 0.05f;
            _groupId = MotionGroups.Path;
            _stacking = MotionStacking.ExclusiveHighestPriority;
            _priority = 10;
            _index = 0;
            _active = false;
        }

        public int GroupId => _groupId;

        public MotionStacking Stacking => _stacking;

        public MotionFinishEvent FinishEvent => MotionFinishEvent.Arrive;

        public int Priority => _priority;

        public bool IsActive => _active;

        public bool IsFinished => !_active;

        public int CurrentIndex => _index;

        public void Tick(int id, ref MotionState state, float dt, ref Vec3 outDesiredDelta)
        {
            if (!_active) return;
            if (dt <= 0f) return;

            if (_points.Length == 0)
            {
                _active = false;
                return;
            }

            while (_index < _points.Length)
            {
                var to = _points[_index] - state.Position;
                if (to.SqrMagnitude > _arriveEpsilon * _arriveEpsilon) break;
                _index++;
            }

            if (_index >= _points.Length)
            {
                _active = false;
                return;
            }

            var target = _points[_index];
            var deltaTo = target - state.Position;
            var dist = DeterministicMathBridge.Magnitude(in deltaTo);
            if (dist <= 0.00001f)
            {
                _index++;
                return;
            }

            var maxStep = _speed * dt;
            if (maxStep <= 0f) return;

            var step = maxStep;
            if (step > dist) step = dist;

            var dir = deltaTo / dist;
            outDesiredDelta = outDesiredDelta + dir * step;
            state.Forward = dir;

            if (step >= dist - 0.00001f)
            {
                _index++;
                if (_index >= _points.Length) _active = false;
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
                Index = _index,
                Float0 = _speed,
                Float1 = _arriveEpsilon,
            };
            return true;
        }

        public bool ImportSnapshot(in MotionSourceSnapshot snapshot)
        {
            _groupId = snapshot.GroupId;
            _priority = snapshot.Priority;
            _stacking = snapshot.Stacking;
            _active = snapshot.IsActive && _points.Length > 0;
            _index = snapshot.Index;
            if (_index < 0) _index = 0;
            if (_index > _points.Length) _index = _points.Length;
            _speed = snapshot.Float0 <= 0f ? _speed : snapshot.Float0;
            _arriveEpsilon = snapshot.Float1 <= 0f ? _arriveEpsilon : snapshot.Float1;
            return true;
        }

        private static Vec3[] CopyPoints(Vec3[] points)
        {
            if (points == null || points.Length == 0) return Array.Empty<Vec3>();
            var copy = new Vec3[points.Length];
            Array.Copy(points, copy, points.Length);
            return copy;
        }
    }
}
