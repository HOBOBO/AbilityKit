using System;
using AbilityKit.Core.Mathematics;
using Xunit;

namespace AbilityKit.Combat.Collision.Abstractions.Tests
{
    /// <summary>
    /// 碰撞查询确定性（P2）测试：漂移敏感运算（球体射线判别式开方、法线/方向归一化、
    /// sweep 分离法向）已切换定点内核。断言用精确 float 值——输出由整数算法加单次
    /// IEEE 换算得出，任何平台/运行时逐位一致；若断言失败说明内核被改动。
    /// </summary>
    public sealed class DeterministicCollisionQueryTests
    {
        [Fact]
        public void RaycastSphere_HitsWithExactDeterministicDistanceAndNormal()
        {
            var ray = new Ray3(new Vec3(0f, 0f, 0f), new Vec3(1f, 0f, 0f));
            var sphere = new Sphere(new Vec3(5f, 0f, 0f), 1f);

            Assert.True(CollisionQueries.Raycast(ray, sphere, out var distance, out var normal));

            Assert.Equal(4f, distance);
            Assert.Equal(-1f, normal.X);
            Assert.Equal(0f, normal.Y);
            Assert.Equal(0f, normal.Z);
        }

        [Fact]
        public void RaycastSphere_MissesWhenDiscriminantNegative()
        {
            var ray = new Ray3(new Vec3(0f, 3f, 0f), new Vec3(1f, 0f, 0f));
            var sphere = new Sphere(new Vec3(5f, 0f, 0f), 1f);

            Assert.False(CollisionQueries.Raycast(ray, sphere, out var distance, out _));
            Assert.Equal(0f, distance);
        }

        [Fact]
        public void RaycastSphere_RejectsWhenSphereIsBehind()
        {
            var ray = new Ray3(new Vec3(0f, 0f, 0f), new Vec3(1f, 0f, 0f));
            var sphere = new Sphere(new Vec3(-5f, 0f, 0f), 1f);

            Assert.False(CollisionQueries.Raycast(ray, sphere, out _, out _));
        }

        [Fact]
        public void RaycastSphere_GlancingHitNormalIsUnitLength()
        {
            var ray = new Ray3(new Vec3(0f, 1f, 0f), new Vec3(1f, 0f, 0f));
            var sphere = new Sphere(new Vec3(5f, 0f, 0f), 1f);

            Assert.True(CollisionQueries.Raycast(ray, sphere, out _, out var normal));
            Assert.InRange(normal.Magnitude, 0.999999f, 1.000001f);
        }

        [Fact]
        public void RaycastSphere_IrrationalDistanceMatchesFixedMath()
        {
            // 斜射产生无理数命中距离：定点内核的输出必须与 DeterministicMath 逐位一致。
            var ray = new Ray3(Vec3.Zero, new Vec3(1f, 0f, 0f));
            var sphere = new Sphere(new Vec3(4f, 1f, 0f), 1f);

            Assert.True(CollisionQueries.Raycast(ray, sphere, out var distance, out _));

            var expected = HitDistanceReference(ray, sphere);
            Assert.Equal(expected, distance);
        }

        [Fact]
        public void SweepVsSphere_WorldNormalIsDeterministicallyNormalized()
        {
            var shape = ColliderShape.CreateSphere(new Vec3(5f, 0f, 0f), 0.5f);

            Assert.True(AbilityKit.Core.Mathematics.SphereSweepQueries.SweepVsShape(
                new Vec3(0f, 0f, 0f),
                new Vec3(1f, 0f, 0f),
                10f,
                0.25f,
                shape,
                out var distance,
                out var normal));

            Assert.Equal(4.25f, distance);
            Assert.Equal(-1f, normal.X);
        }

        /// <summary>
        /// 用与查询内核相同的定点公式独立复算命中距离（t = -b - sqrt(disc) / 2a），
        /// 锁定“内核与参考公式逐位一致”的契约。
        /// </summary>
        private static float HitDistanceReference(in Ray3 ray, in Sphere sphere)
        {
            var o = new AbilityKit.Deterministic.FixedVec3(
                AbilityKit.Deterministic.Fixed64.FromSingle(ray.Origin.X),
                AbilityKit.Deterministic.Fixed64.FromSingle(ray.Origin.Y),
                AbilityKit.Deterministic.Fixed64.FromSingle(ray.Origin.Z));
            var d = new AbilityKit.Deterministic.FixedVec3(
                AbilityKit.Deterministic.Fixed64.FromSingle(ray.Direction.X),
                AbilityKit.Deterministic.Fixed64.FromSingle(ray.Direction.Y),
                AbilityKit.Deterministic.Fixed64.FromSingle(ray.Direction.Z));
            var c = new AbilityKit.Deterministic.FixedVec3(
                AbilityKit.Deterministic.Fixed64.FromSingle(sphere.Center.X),
                AbilityKit.Deterministic.Fixed64.FromSingle(sphere.Center.Y),
                AbilityKit.Deterministic.Fixed64.FromSingle(sphere.Center.Z));
            var r = AbilityKit.Deterministic.Fixed64.FromSingle(sphere.Radius);

            var oc = o - c;
            var two = AbilityKit.Deterministic.Fixed64.FromInt64(2);
            var a = AbilityKit.Deterministic.FixedVec3.Dot(d, d);
            var b = two * AbilityKit.Deterministic.FixedVec3.Dot(oc, d);
            var cc = AbilityKit.Deterministic.FixedVec3.Dot(oc, oc) - r * r;
            var disc = (b * b) - (AbilityKit.Deterministic.Fixed64.FromInt64(4) * a * cc);
            var sqrt = AbilityKit.Deterministic.DeterministicMath.Sqrt(disc);
            return ((-b - sqrt) / (two * a)).ToSingle();
        }
    }
}
