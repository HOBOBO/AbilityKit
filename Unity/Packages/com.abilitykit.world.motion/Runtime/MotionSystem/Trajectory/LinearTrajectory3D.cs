using AbilityKit.Core.Math;

namespace AbilityKit.Core.Common.MotionSystem.Trajectory
{
    public sealed class LinearTrajectory3D : ITrajectory3D
    {
        private readonly Vec3 _start;
        private readonly Vec3 _end;
        private readonly float _duration;
        private readonly Vec3 _dir;

        public LinearTrajectory3D(in Vec3 start, in Vec3 end, float duration)
        {
            _start = start;
            _end = end;
            _duration = duration <= 0f ? 0.0001f : duration;

            var d = end - start;
            var mag = d.Magnitude;
            _dir = mag > 0.00001f ? d / mag : new Vec3(0f, 0f, 1f);
        }

        public float Duration => _duration;

        public Vec3 SamplePosition(float time)
        {
            var t = time / _duration;
            if (t <= 0f) return _start;
            if (t >= 1f) return _end;
            return _start + (_end - _start) * t;
        }

        public bool TrySampleForward(float time, out Vec3 forward)
        {
            forward = _dir;
            return true;
        }
    }
}
