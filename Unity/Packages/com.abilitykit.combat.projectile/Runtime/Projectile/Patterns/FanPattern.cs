using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.Math;

namespace AbilityKit.Ability.Share.Common.Projectile
{
    public sealed class FanPattern : IProjectileSpawnPattern
    {
        private readonly int _count;
        private readonly float _totalAngleDeg;

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
                results.Add(baseSpawn);
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

                results.Add(baseSpawn.WithDirection(in dir));
            }
        }
    }
}
