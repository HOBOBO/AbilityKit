using System;
using AbilityKit.Ability.Share.Common.MotionSystem.Core;
using AbilityKit.Ability.Share.Math;

namespace AbilityKit.Ability.Share.Common.MotionSystem.Generic
{
    public sealed class PathFollowerMotionSource : IMotionSource, IMotionFinishEventSource
    {
        private readonly Vec3[] _points;
        private readonly float _speed;
        private readonly float _arriveEpsilon;

        private readonly int _groupId;
        private readonly MotionStacking _stacking;

        private int _index;
        private bool _active;

        public PathFollowerMotionSource(Vec3[] points, float speed, float arriveEpsilon = 0.05f, int priority = 10, int groupId = MotionGroups.Path, MotionStacking stacking = MotionStacking.ExclusiveHighestPriority)
        {
            _points = points ?? Array.Empty<Vec3>();
            _speed = speed <= 0f ? 0.0001f : speed;
            _arriveEpsilon = arriveEpsilon <= 0f ? 0.01f : arriveEpsilon;
            Priority = priority;

            _groupId = groupId;
            _stacking = stacking;

            _index = 0;
            _active = _points.Length > 0;
        }

        public int GroupId => _groupId;

        public MotionStacking Stacking => _stacking;

        public MotionFinishEvent FinishEvent => MotionFinishEvent.Arrive;

        public int Priority { get; }

        public bool IsActive => _active;

        public bool IsFinished => !_active;

        public void Tick(int id, ref MotionState state, float dt, ref Vec3 outDesiredDelta)
        {
            if (!_active) return;
            if (dt <= 0f) return;

            if (_points.Length == 0)
            {
                _active = false;
                return;
            }

            // Skip reached waypoints.
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
            var dist = deltaTo.Magnitude;
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
    }
}
