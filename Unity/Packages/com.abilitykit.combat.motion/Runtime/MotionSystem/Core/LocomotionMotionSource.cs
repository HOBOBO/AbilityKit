using AbilityKit.Core.Mathematics;
using AbilityKit.Core.Pooling;

namespace AbilityKit.Combat.MotionSystem.Core
{
    public enum MotionInputSpace
    {
        World = 0,
        Local = 1,
    }

    public sealed class LocomotionMotionSource : IMotionSource, IMotionSnapshotSource
    {
        private static readonly ObjectPool<LocomotionMotionSource> Pool = Pools.GetPool(
            createFunc: () => new LocomotionMotionSource(),
            onRelease: s => s.Reset(),
            defaultCapacity: 64,
            maxSize: 2048,
            collectionCheck: false);

        private Vec3 _input;
        private float _speed;
        private MotionInputSpace _space;
        private int _priority;

        private LocomotionMotionSource()
        {
            Reset();
        }

        public LocomotionMotionSource(float speed, MotionInputSpace space = MotionInputSpace.Local, int priority = 0)
        {
            Configure(speed, space, priority);
        }

        public static LocomotionMotionSource Rent(float speed, MotionInputSpace space = MotionInputSpace.Local, int priority = 0)
        {
            var source = Pool.Get();
            source.Configure(speed, space, priority);
            return source;
        }

        public static void Release(LocomotionMotionSource source)
        {
            if (source == null) return;
            Pool.Release(source);
        }

        public void Configure(float speed, MotionInputSpace space = MotionInputSpace.Local, int priority = 0)
        {
            _speed = speed;
            _space = space;
            _priority = priority;
            _input = Vec3.Zero;
        }

        public void Reset()
        {
            _input = Vec3.Zero;
            _speed = 0f;
            _space = MotionInputSpace.Local;
            _priority = 0;
        }

        public int GroupId => MotionGroups.Locomotion;

        public MotionStacking Stacking => MotionStacking.Additive;

        public int Priority => _priority;

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
                var f = state.Forward;
                f = new Vec3(f.X, 0f, f.Z);
                var fMag = DeterministicMathBridge.Magnitude(in f);
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

            var mag = DeterministicMathBridge.Magnitude(in dir);
            if (mag <= 0.00001f) return;
            dir = dir / mag;

            outDesiredDelta = outDesiredDelta + dir * (_speed * dt);
        }

        public void Cancel()
        {
            _input = Vec3.Zero;
        }

        public bool ExportSnapshot(out MotionSourceSnapshot snapshot)
        {
            snapshot = new MotionSourceSnapshot
            {
                GroupId = GroupId,
                Priority = _priority,
                Stacking = Stacking,
                IsActive = true,
                Vector0 = _input,
                Float0 = _speed,
                Index = (int)_space,
            };
            return true;
        }

        public bool ImportSnapshot(in MotionSourceSnapshot snapshot)
        {
            _priority = snapshot.Priority;
            _input = snapshot.Vector0;
            _speed = snapshot.Float0;
            _space = (MotionInputSpace)snapshot.Index;
            return true;
        }
    }
}
