using System;
using System.Collections.Generic;
using AbilityKit.Core.Mathematics;

namespace AbilityKit.Combat.Projectile
{
    public sealed class FanPattern : IProjectileSpawnPattern
    {
        private readonly int _count;
        private readonly float _totalAngleDeg;

        internal int Count => _count;
        internal float TotalAngleDeg => _totalAngleDeg;

        public FanPattern(int count, float totalAngleDeg)
        {
            _count = MathUtil.Clamp(count, 1, 1024);
            _totalAngleDeg = totalAngleDeg;
        }

        public void Build(in ProjectileSpawnParams baseSpawn, List<ProjectileSpawnParams> results)
        {
            if (results == null) return;

            if (_count <= 1 || MathUtil.IsZero(_totalAngleDeg))
            {
                results.Add(baseSpawn.WithPatternSlot(0, 1));
                return;
            }

            var step = _totalAngleDeg / (_count - 1);
            var start = -_totalAngleDeg * 0.5f;

            for (int i = 0; i < _count; i++)
            {
                var angleDeg = start + step * i;
                var angleRad = angleDeg * (System.MathF.PI / 180f);
                var rot = Quat.FromAxisAngle(Vec3.Up, angleRad);
                var dir = rot.Rotate(baseSpawn.Direction).Normalized;

                results.Add(baseSpawn.WithDirection(in dir).WithPatternSlot(i, _count));
            }
        }
    }
}
