using System;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Protocol.Shooter;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests.Runtime;

public sealed class ShooterRvoNeighborAccelerationTests
{
    [Theory]
    [InlineData(AccelerationBehavior.Success)]
    [InlineData(AccelerationBehavior.Reject)]
    [InlineData(AccelerationBehavior.Throw)]
    [InlineData(AccelerationBehavior.Unavailable)]
    [InlineData(AccelerationBehavior.InvalidOutput)]
    [InlineData(AccelerationBehavior.DuplicateNeighbor)]
    [InlineData(AccelerationBehavior.OutOfRangeDistance)]
    [InlineData(AccelerationBehavior.ForgedDistance)]
    public void OptionalNeighborAccelerationUsesServiceOrFallsBackDeterministically(AccelerationBehavior behavior)
    {
        var service = new TestNeighborAccelerationService(behavior);
        var accelerated = CreateWorld(service, ShooterRvoExecutionMode.AcceleratedPreferred);
        var managed = CreateWorld(null, ShooterRvoExecutionMode.Managed);
        var start = new ShooterStartGamePayload(
            "optional-rvo-neighbor-acceleration",
            30,
            12345,
            new[] { new ShooterStartPlayer(1, "P1", 0f, 0f) });

        Assert.True(accelerated.Runtime.StartGame(in start));
        Assert.True(managed.Runtime.StartGame(in start));
        Assert.True(accelerated.Runtime.Tick(0f));
        Assert.True(managed.Runtime.Tick(0f));

        var health = new ShooterSveltoHealthComponent { Current = 3, Max = 3, Alive = 1 };
        for (var index = 0; index < 4; index++)
        {
            var transform = new ShooterSveltoTransformComponent
            {
                X = 4f + index * 0.05f,
                Y = index % 2 == 0 ? 0.1f : -0.1f,
                DirectionX = -1f
            };
            var enemyId = 9101 + index;
            accelerated.Entities.AddEnemy(enemyId, in transform, in health);
            managed.Entities.AddEnemy(enemyId, in transform, in health);
        }

        for (var frame = 0; frame < 30; frame++)
        {
            Assert.True(accelerated.Runtime.Tick(1f / 30f));
            Assert.True(managed.Runtime.Tick(1f / 30f));
        }

        if (behavior == AccelerationBehavior.Unavailable)
        {
            Assert.Equal(0, service.CallCount);
        }
        else
        {
            Assert.True(service.CallCount > 0);
        }

        Assert.Equal(managed.Runtime.ComputeStateHash(), accelerated.Runtime.ComputeStateHash());
    }

    [Fact]
    public void ManagedExecutionModeNeverCallsAccelerationService()
    {
        var service = new TestNeighborAccelerationService(AccelerationBehavior.Throw);
        var world = CreateWorld(service, ShooterRvoExecutionMode.Managed);
        var start = new ShooterStartGamePayload(
            "managed-rvo-neighbor-collection",
            30,
            12345,
            new[] { new ShooterStartPlayer(1, "P1", 0f, 0f) });

        Assert.True(world.Runtime.StartGame(in start));
        Assert.True(world.Runtime.Tick(1f / 30f));
        Assert.Equal(0, service.CallCount);
    }

    private static TestWorld CreateWorld(
        IShooterRvoNeighborAccelerationService? acceleration,
        ShooterRvoExecutionMode executionMode)
    {
        var flow = new ShooterSveltoGameplayBattleFlowConfig(
            durationFrames: 120,
            victoryTargetDefeats: 99,
            maxActiveEnemies: 4,
            new[] { new ShooterSveltoGameplayWaveConfig(1, 100, 1, 1, 3, 4f) },
            enemyLoadoutId: ShooterSveltoGameplayBattleFlowConfig.DefaultEnemyLoadoutId,
            enemyAttackIntervalFrames: 120,
            enemyAttackDamage: 1,
            enemyProjectileSpeedScale: ShooterSveltoGameplayBattleFlowConfig.DefaultEnemyProjectileSpeedScale,
            enemyProjectilesPerShot: ShooterSveltoGameplayBattleFlowConfig.DefaultEnemyProjectilesPerShot,
            enemySpreadDegrees: ShooterSveltoGameplayBattleFlowConfig.DefaultEnemySpreadDegrees);
        var builder = new WorldContainerBuilder()
            .RegisterInstance(new ShooterEnemyWaveOptions(enabled: true, flow))
            .RegisterInstance(new ShooterRvoOptions(executionMode, maxAcceleration: 100f));
        if (acceleration != null)
        {
            builder.RegisterInstance<IShooterRvoNeighborAccelerationService>(acceleration);
        }

        var container = builder
            .AddModule(new ShooterWorldModule())
            .Build();
        return new TestWorld(
            container.Resolve<IShooterBattleRuntimePort>(),
            container.Resolve<IShooterEntityManager>());
    }

    public enum AccelerationBehavior
    {
        Success,
        Reject,
        Throw,
        Unavailable,
        InvalidOutput,
        DuplicateNeighbor,
        OutOfRangeDistance,
        ForgedDistance
    }

    private sealed class TestNeighborAccelerationService : IShooterRvoNeighborAccelerationService
    {
        private readonly AccelerationBehavior _behavior;

        public TestNeighborAccelerationService(AccelerationBehavior behavior)
        {
            _behavior = behavior;
        }

        public bool IsAvailable => _behavior != AccelerationBehavior.Unavailable;
        public int CallCount { get; private set; }

        public bool TryCollectNeighbors(in ShooterRvoNeighborBatch batch)
        {
            CallCount++;
            if (_behavior == AccelerationBehavior.Throw)
            {
                throw new InvalidOperationException("Simulated acceleration failure.");
            }

            if (_behavior == AccelerationBehavior.Reject)
            {
                return false;
            }

            if (_behavior == AccelerationBehavior.InvalidOutput)
            {
                batch.NeighborCounts[0] = 1;
                batch.NeighborIndices[0] = 0;
                batch.NeighborDistanceSquared[0] = 0f;
                return true;
            }

            if (_behavior == AccelerationBehavior.DuplicateNeighbor)
            {
                batch.NeighborCounts[0] = 2;
                batch.NeighborIndices[0] = 1;
                batch.NeighborIndices[1] = 1;
                var dx = batch.PositionX[1] - batch.PositionX[0];
                var dy = batch.PositionY[1] - batch.PositionY[0];
                var distanceSquared = dx * dx + dy * dy;
                batch.NeighborDistanceSquared[0] = distanceSquared;
                batch.NeighborDistanceSquared[1] = distanceSquared;
                return true;
            }

            if (_behavior == AccelerationBehavior.OutOfRangeDistance)
            {
                batch.NeighborCounts[0] = 1;
                batch.NeighborIndices[0] = 1;
                batch.NeighborDistanceSquared[0] =
                    batch.NeighborDistance * batch.NeighborDistance + 1f;
                return true;
            }

            if (_behavior == AccelerationBehavior.ForgedDistance)
            {
                batch.NeighborCounts[0] = 1;
                batch.NeighborIndices[0] = 1;
                batch.NeighborDistanceSquared[0] = 0f;
                return true;
            }

            var rangeSquared = batch.NeighborDistance * batch.NeighborDistance;
            Array.Clear(batch.NeighborCounts, 0, batch.Count);
            for (var agentIndex = 0; agentIndex < batch.Count; agentIndex++)
            {
                for (var candidateIndex = 0; candidateIndex < batch.Count; candidateIndex++)
                {
                    if (candidateIndex == agentIndex)
                    {
                        continue;
                    }

                    var dx = batch.PositionX[candidateIndex] - batch.PositionX[agentIndex];
                    var dy = batch.PositionY[candidateIndex] - batch.PositionY[agentIndex];
                    var distanceSquared = dx * dx + dy * dy;
                    if (distanceSquared <= rangeSquared)
                    {
                        InsertNeighbor(in batch, agentIndex, candidateIndex, distanceSquared);
                    }
                }
            }

            return true;
        }

        private static void InsertNeighbor(
            in ShooterRvoNeighborBatch batch,
            int agentIndex,
            int candidateIndex,
            float distanceSquared)
        {
            var count = batch.NeighborCounts[agentIndex];
            var offset = agentIndex * batch.MaxNeighbors;
            var insertIndex = Math.Min(count, batch.MaxNeighbors - 1);
            while (insertIndex > 0)
            {
                var previousSlot = insertIndex - 1;
                var previousDistance = batch.NeighborDistanceSquared[offset + previousSlot];
                var previousIndex = batch.NeighborIndices[offset + previousSlot];
                if (previousDistance < distanceSquared ||
                    (previousDistance == distanceSquared && batch.EntityIds[previousIndex] < batch.EntityIds[candidateIndex]))
                {
                    break;
                }

                if (insertIndex < batch.MaxNeighbors)
                {
                    batch.NeighborIndices[offset + insertIndex] = previousIndex;
                    batch.NeighborDistanceSquared[offset + insertIndex] = previousDistance;
                }
                insertIndex--;
            }

            batch.NeighborIndices[offset + insertIndex] = candidateIndex;
            batch.NeighborDistanceSquared[offset + insertIndex] = distanceSquared;
            batch.NeighborCounts[agentIndex] = Math.Min(count + 1, batch.MaxNeighbors);
        }
    }

    private readonly struct TestWorld
    {
        public TestWorld(IShooterBattleRuntimePort runtime, IShooterEntityManager entities)
        {
            Runtime = runtime;
            Entities = entities;
        }

        public IShooterBattleRuntimePort Runtime { get; }
        public IShooterEntityManager Entities { get; }
    }
}
