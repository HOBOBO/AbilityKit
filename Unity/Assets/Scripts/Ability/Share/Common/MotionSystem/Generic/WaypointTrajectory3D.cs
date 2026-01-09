using System;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.Share.Common.MotionSystem.Trajectory;

namespace AbilityKit.Ability.Share.Common.MotionSystem.Generic
{
    public sealed class WaypointTrajectory3D : ITrajectory3D
    {
        private readonly Vec3[] _points;
        private readonly float[] _cumulative;
        private readonly float _speed;
        private readonly float _duration;

        public WaypointTrajectory3D(Vec3[] points, float speed)
        {
            _points = points ?? Array.Empty<Vec3>();
            _speed = speed <= 0f ? 0.0001f : speed;

            if (_points.Length < 2)
            {
                _cumulative = Array.Empty<float>();
                _duration = 0f;
                return;
            }

            _cumulative = new float[_points.Length];
            _cumulative[0] = 0f;

            var total = 0f;
            for (int i = 1; i < _points.Length; i++)
            {
                var seg = (_points[i] - _points[i - 1]).Magnitude;
                total += seg;
                _cumulative[i] = total;
            }

            _duration = total / _speed;
        }

        public float Speed => _speed;

        public float Duration => _duration;

        public Vec3 SamplePosition(float time)
        {
            if (_points.Length == 0) return Vec3.Zero;
            if (_points.Length == 1) return _points[0];
            if (_duration <= 0f) return _points[_points.Length - 1];

            if (time <= 0f) return _points[0];
            if (time >= _duration) return _points[_points.Length - 1];

            var dist = time * _speed;

            // Find segment containing dist.
            var idx = FindSegmentIndex(dist);
            if (idx <= 0) return _points[0];

            var prevDist = _cumulative[idx - 1];
            var segDist = _cumulative[idx] - prevDist;
            if (segDist <= 0.00001f) return _points[idx];

            var u = (dist - prevDist) / segDist;
            return _points[idx - 1] + (_points[idx] - _points[idx - 1]) * u;
        }

        public bool TrySampleForward(float time, out Vec3 forward)
        {
            forward = new Vec3(0f, 0f, 1f);
            if (_points.Length < 2) return false;

            if (_duration <= 0f)
            {
                forward = (_points[_points.Length - 1] - _points[0]).Normalized;
                return true;
            }

            var t = time;
            if (t < 0f) t = 0f;
            if (t > _duration) t = _duration;

            var dist = t * _speed;
            var idx = FindSegmentIndex(dist);
            if (idx <= 0) idx = 1;
            if (idx >= _points.Length) idx = _points.Length - 1;

            var d = _points[idx] - _points[idx - 1];
            var mag = d.Magnitude;
            forward = mag > 0.00001f ? d / mag : new Vec3(0f, 0f, 1f);
            return true;
        }

        private int FindSegmentIndex(float dist)
        {
            // _cumulative is monotonically increasing.
            // Return smallest i where cumulative[i] >= dist.
            int lo = 1;
            int hi = _cumulative.Length - 1;

            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                var v = _cumulative[mid];
                if (v < dist)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            if (lo < 1) return 1;
            if (lo >= _cumulative.Length) return _cumulative.Length - 1;
            return lo;
        }
    }
}
