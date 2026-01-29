using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.Math;

namespace AbilityKit.Ability.Share.Common.Projectile
{
    public sealed class ScatterPattern : IProjectileSpawnPattern
    {
        private readonly int _count;
        private readonly float _maxAngleDeg;
        private readonly uint _seed;

        public ScatterPattern(int count, float maxAngleDeg, int seed)
        {
            _count = MathUtil.Clamp(count, 1, 1024);
            _maxAngleDeg = MathUtil.Abs(maxAngleDeg);
            _seed = (uint)seed;
        }

        public void Build(in ProjectileSpawnParams baseSpawn, List<ProjectileSpawnParams> results)
        {
            if (results == null) return;

            if (_count <= 1 || MathUtil.IsZero(_maxAngleDeg))
            {
                results.Add(baseSpawn);
                return;
            }

            // Deterministic scatter around Vec3.Up axis (yaw). For pitch scatter, extend here later.
            var rng = new XorShift32(_seed == 0 ? 1u : _seed);

            for (int i = 0; i < _count; i++)
            {
                var t = rng.NextFloat01();
                var angleDeg = (t * 2f - 1f) * _maxAngleDeg;
                var angleRad = angleDeg * (System.MathF.PI / 180f);

                var rot = Quat.FromAxisAngle(Vec3.Up, angleRad);
                var dir = rot.Rotate(baseSpawn.Direction).Normalized;
                results.Add(baseSpawn.WithDirection(in dir));
            }
        }

        private struct XorShift32
        {
            private uint _state;

            public XorShift32(uint seed)
            {
                _state = seed;
            }

            public uint NextUInt()
            {
                var x = _state;
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                _state = x;
                return x;
            }

            public float NextFloat01()
            {
                // 24-bit mantissa precision
                return (NextUInt() & 0x00FFFFFFu) / 16777215f;
            }
        }
    }
}
