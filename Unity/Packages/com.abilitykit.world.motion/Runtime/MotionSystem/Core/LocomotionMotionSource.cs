using AbilityKit.Core.Math;

namespace AbilityKit.Core.Common.MotionSystem.Core
{
    public enum MotionInputSpace
    {
        World = 0,
        Local = 1,
    }

    public sealed class LocomotionMotionSource : IMotionSource
    {
        private Vec3 _input;
        private float _speed;
        private MotionInputSpace _space;

        public LocomotionMotionSource(float speed, MotionInputSpace space = MotionInputSpace.Local, int priority = 0)
        {
            _speed = speed;
            _space = space;
            Priority = priority;
            _input = Vec3.Zero;
        }

        public int GroupId => MotionGroups.Locomotion;

        public MotionStacking Stacking => MotionStacking.Additive;

        public int Priority { get; }

        public bool IsActive => true;

        public float Speed
        {
            get => _speed;
            set => _speed = value;
        }

        public MotionInputSpace Space
        {
            get => _space;
            set => _space = value;
        }

        public Vec3 Input => _input;

        public void SetInput(float x, float z)
        {
            _input = new Vec3(x, 0f, z);
        }

        public void Tick(int id, ref MotionState state, float dt, ref Vec3 outDesiredDelta)
        {
            if (dt <= 0f) return;
            if (_speed <= 0f) return;
            if (_input.SqrMagnitude <= 0.0000001f) return;

            Vec3 dir;
            if (_space == MotionInputSpace.World)
            {
                dir = _input;
            }
            else
            {
                // Local-space: input.x is right, input.z is forward.
                var f = state.Forward;
                f = new Vec3(f.X, 0f, f.Z);
                var fMag = f.Magnitude;
                if (fMag <= 0.00001f)
                {
                    f = new Vec3(0f, 0f, 1f);
                }
                else
                {
                    f = f / fMag;
                }

                var r = new Vec3(f.Z, 0f, -f.X);
                dir = r * _input.X + f * _input.Z;
            }

            var mag = dir.Magnitude;
            if (mag <= 0.00001f) return;
            dir = dir / mag;

            outDesiredDelta = outDesiredDelta + dir * (_speed * dt);
        }

        public void Cancel()
        {
            _input = Vec3.Zero;
        }
    }
}
