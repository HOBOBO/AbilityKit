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
        /// 胶囊包围盒边界的零距离近似命中，只阻挡朝内移动，允许反向和切向离开。
        /// </summary>
        [Theory]
        [InlineData(-1f, 0f, false)]
        [InlineData(0f, 1f, false)]
        [InlineData(1f, 0f, true)]
        public void SweepVsCapsule_ConservativeBoundaryAllowsSeparation(float x, float z, bool blocked)
        {
            var capsule = ColliderShape.CreateCapsule(new Vec3(0f, 1f, 0f), new Vec3(0f, 2f, 0f), 1f);
            var start = new Vec3(-1.5f, 0f, 0f);
            var direction = new Vec3(x, 0f, z);

            Assert.Equal(blocked, SphereSweepQueries.SweepVsShape(start, direction, 1f, .5f,
                capsule, out var distance, out var normal));
            if (blocked)
            {
                Assert.InRange(distance, 0f, 1f);
                Assert.True(normal.X < 0f);
            }
        }

        /// <summary>
        /// 接触点的切向尾差不应再次阻挡；有实际朝内分量时仍必须报告碰撞。
        /// </summary>
        [Theory]
        [InlineData(0, .0000001f, 0f, false)]
        [InlineData(0, .001f, 0f, true)]
        [InlineData(0, .0000001f, .0000005f, false)]
        [InlineData(0, .001f, .0000005f, true)]
        [InlineData(1, .0000001f, 0f, false)]
        [InlineData(1, .001f, 0f, true)]
        [InlineData(1, .0000001f, .0000005f, false)]
        [InlineData(1, .001f, .0000005f, true)]
        [InlineData(2, .0000001f, 0f, false)]
        [InlineData(2, .001f, 0f, true)]
        public void Contact_sweep_distinguishes_tangent_roundoff_from_inward_motion(int shapeKind, float inward, float outwardOffset, bool blocked)
        {
            var rotation = shapeKind == 2 ? Quat.FromAxisAngle(Vec3.Up, (float)Math.PI / 6f) : Quat.Identity;
            var shape = shapeKind == 0 ? ColliderShape.CreateSphere(Vec3.Zero, 1f)
                : shapeKind == 1 ? ColliderShape.CreateCapsule(new Vec3(0, -.5f, 0), new Vec3(0, .5f, 0), 1f)
                : ColliderShape.CreateObb(Vec3.Zero, rotation, new Vec3(1, 2, 2));
            var start = rotation.Rotate(Vec3.Right * (1.5f + outwardOffset));
            var direction = rotation.Rotate(new Vec3(-inward, 0, 1).Normalized);

            Assert.Equal(blocked, SphereSweepQueries.SweepVsShape(start, direction, .01f, .5f,
                shape, out _, out _));
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
