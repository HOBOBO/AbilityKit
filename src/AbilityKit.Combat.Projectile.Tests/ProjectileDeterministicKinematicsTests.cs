using System.Collections.Generic;
using AbilityKit.Core.Mathematics;
using AbilityKit.Deterministic;
using AbilityKit.Ability.World.Services;
using Xunit;

namespace AbilityKit.Combat.Projectile.Tests
{
    /// <summary>
    /// 弹丸运动学定点化（P1）的行为与位一致性测试。
    /// raw 值断言锁死跨平台位一致：位置推进、方向归一化、回滚快照往返。
    /// </summary>
    public sealed class ProjectileDeterministicKinematicsTests
    {
        private const int TestLayer = 1;

        private static ProjectileWorld CreateWorld() => new(new NaiveCollisionWorld());

        private static ProjectileSpawnParams StraightFlightParams(
            in Vec3 position,
            in Vec3 direction,
            float speed,
            float maxDistance = 1000f,
            int lifetimeFrames = 600,
            int trackingTargetActorId = 0)
        {
            return new ProjectileSpawnParams(
                ownerId: 1,
                templateId: 100,
                launcherActorId: 1,
                rootActorId: 1,
                spawnFrame: 0,
                position: position,
                direction: direction,
                speed: speed,
                returnAfterFrames: 0,
                returnSpeed: 0f,
                returnStopDistance: 0f,
                lifetimeFrames: lifetimeFrames,
                maxDistance: maxDistance,
                collisionLayerMask: 1 << TestLayer,
                ignoreCollider: ColliderId.Invalid,
                trackingTargetActorId: trackingTargetActorId);
        }

        private static void Tick(ProjectileWorld world, int frame, Fixed64 dt, int frames)
        {
            for (var i = 0; i < frames; i++)
            {
                world.Tick(frame + i, dt, null, null, null);
            }
        }

        [Fact]
        public void Flight_PositionAccumulatesInExactFixedArithmetic()
        {
            var world = CreateWorld();
            var id = world.Spawn(StraightFlightParams(Vec3.Zero, new Vec3(1f, 0f, 0f), 8f));

            // dt = 1/16（二进制精确），speed = 8：每帧 move = 0.5，4 帧后 X = 2.0 精确。
            Tick(world, 1, Fixed64.FromRatio(1, 16), 4);

            Assert.True(world.TryGetFixedKinematics(id, out var position, out _));
            Assert.Equal(Fixed64.FromInt64(2).RawValue, position.X.RawValue);
            Assert.Equal(0L, position.Y.RawValue);
            Assert.Equal(0L, position.Z.RawValue);
        }

        [Fact]
        public void Flight_DecimalDt_PositionRawValueIsGolden()
        {
            var world = CreateWorld();
            var id = world.Spawn(StraightFlightParams(Vec3.Zero, new Vec3(1f, 0f, 0f), 5f));

            // dt = 1/15（截断），speed = 5：每帧 move raw = 5 * floor(2^32/15) = 1431655765，
            // 3 帧后 X raw = 4294967295。该值在任何平台/运行时上必须逐位一致。
            Tick(world, 1, Fixed64.FromRatio(1, 15), 3);

            Assert.True(world.TryGetFixedKinematics(id, out var position, out _));
            Assert.Equal(4294967295L, position.X.RawValue);
        }

        [Fact]
        public void DiagonalFlight_TrackingDirectionUsesFixedNormalization()
        {
            var world = CreateWorld();
            world.SetTrackingTargetProvider(new StubTrackingProvider(new Vec3(3f, 0f, 4f)));

            var id = world.Spawn(StraightFlightParams(Vec3.Zero, new Vec3(1f, 0f, 0f), 10f, trackingTargetActorId: 7));

            Tick(world, 1, Fixed64.FromRatio(1, 16), 1);

            // 目标 (3,0,4)：归一化方向 = (3/5, 0, 4/5)，定点 Sqrt(25) = 5 精确。
            Assert.True(world.TryGetFixedKinematics(id, out _, out var direction));
            Assert.Equal(Fixed64.FromRatio(3, 5).RawValue, direction.X.RawValue);
            Assert.Equal(0L, direction.Y.RawValue);
            Assert.Equal(Fixed64.FromRatio(4, 5).RawValue, direction.Z.RawValue);
        }

        [Fact]
        public void MaxDistance_ExitFiresAtExactFixedBudget()
        {
            var world = CreateWorld();
            var exits = new List<ProjectileExitEvent>();

            // speed = 4，dt = 1/16 → 每帧 0.25；MaxDistance = 1：第 4 帧恰好走完并退出。
            var id = world.Spawn(StraightFlightParams(Vec3.Zero, new Vec3(1f, 0f, 0f), 4f, maxDistance: 1f));

            for (var frame = 1; frame <= 4; frame++)
            {
                world.Tick(frame, Fixed64.FromRatio(1, 16), null, exits, null);
            }

            Assert.Single(exits);
            Assert.Equal(ProjectileExitReason.MaxDistance, exits[0].Reason);
            Assert.Equal(id, exits[0].Projectile);
        }

        [Fact]
        public void SphereHit_ProjectileExitsOnHit()
        {
            var collision = new NaiveCollisionWorld();
            collision.Add(
                new Transform3(new Vec3(5f, 0f, 0f), Quat.Identity, new Vec3(1f, 1f, 1f)),
                ColliderShape.CreateSphere(new Vec3(5f, 0f, 0f), 0.5f),
                TestLayer);

            var world = new ProjectileWorld(collision);
            var hits = new List<ProjectileHitEvent>();
            var exits = new List<ProjectileExitEvent>();

            world.Spawn(StraightFlightParams(Vec3.Zero, new Vec3(1f, 0f, 0f), 16f));

            for (var frame = 1; frame <= 16; frame++)
            {
                world.Tick(frame, Fixed64.FromRatio(1, 16), hits, exits, null);
            }

            Assert.Single(hits);
            Assert.Single(exits);
            Assert.Equal(ProjectileExitReason.Hit, exits[0].Reason);
        }

        [Fact]
        public void RollbackSnapshot_RoundTripsExactRawBits()
        {
            var world = CreateWorld();
            var id = world.Spawn(StraightFlightParams(new Vec3(1f, 0f, 0f), new Vec3(1f, 0f, 1f).Normalized, 7f));

            Tick(world, 1, Fixed64.FromRatio(1, 15), 5);

            var payload = world.ExportRollback(default);
            Assert.NotNull(payload);
            Assert.True(payload.Length > 0);

            // 继续推进制造分歧，然后回滚，再走相同帧序列。
            Tick(world, 6, Fixed64.FromRatio(1, 15), 5);
            world.ImportRollback(default, payload);
            Tick(world, 6, Fixed64.FromRatio(1, 15), 5);

            // 与“从未回滚”的基准逐位一致（45° 方向含非精确量，往返必须无损）。
            var reference = CreateWorld();
            var referenceId = reference.Spawn(StraightFlightParams(new Vec3(1f, 0f, 0f), new Vec3(1f, 0f, 1f).Normalized, 7f));
            Tick(reference, 1, Fixed64.FromRatio(1, 15), 10);

            Assert.True(world.TryGetFixedKinematics(id, out var restored, out _));
            Assert.True(reference.TryGetFixedKinematics(referenceId, out var expected, out _));

            Assert.Equal(expected.X.RawValue, restored.X.RawValue);
            Assert.Equal(expected.Z.RawValue, restored.Z.RawValue);
        }

        private sealed class StubTrackingProvider : IProjectileTrackingTargetProvider
        {
            private readonly Vec3 _position;

            public StubTrackingProvider(in Vec3 position)
            {
                _position = position;
            }

            public bool TryGetTrackingTargetPosition(int targetActorId, out Vec3 position)
            {
                position = _position;
                return true;
            }

            public void Dispose()
            {
            }
        }
    }
}
