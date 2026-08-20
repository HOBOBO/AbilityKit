#nullable enable

using System.Collections.Generic;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Protocol.Shooter;
using AbilityKit.World.Svelto;
using NUnit.Framework;

namespace AbilityKit.Demo.Shooter.View.Tests
{
    public sealed class ShooterEnemyWaveAdvanceTests
    {
        [Test]
        public void ClearedGroupStartsNextGroupAfterTwoSeconds()
        {
            var waves = new[]
            {
                CreateWave(waveId: 1, startFrame: 0),
                CreateWave(waveId: 2, startFrame: 0),
                CreateWave(waveId: 3, startFrame: 100)
            };
            var runtime = CreateRuntime(waves, out var entities);

            Assert.That(runtime.Tick(0.5f), Is.True);
            Assert.That(entities.EnemyCount, Is.EqualTo(2),
                "Waves sharing a start frame must remain one reinforcement group.");

            RemoveAllEnemies(entities);
            for (var i = 0; i < 3; i++)
            {
                Assert.That(runtime.Tick(0.5f), Is.True);
                Assert.That(entities.EnemyCount, Is.Zero,
                    "The next group must not start before two cleared seconds elapse.");
            }

            Assert.That(runtime.Tick(0.5f), Is.True);
            Assert.That(entities.EnemyCount, Is.EqualTo(1),
                "A cleared group should advance the next group before its fixed start frame.");
            Assert.That(runtime.CurrentFrame, Is.LessThan(100));
        }

        [Test]
        public void UnclearedGroupKeepsFixedStartFrameSchedule()
        {
            var waves = new[]
            {
                CreateWave(waveId: 1, startFrame: 0),
                CreateWave(waveId: 2, startFrame: 4)
            };
            var runtime = CreateRuntime(waves, out var entities);

            for (var frame = 1; frame < 4; frame++)
            {
                Assert.That(runtime.Tick(1f), Is.True);
                Assert.That(entities.EnemyCount, Is.EqualTo(1));
            }

            Assert.That(runtime.Tick(1f), Is.True);
            Assert.That(runtime.CurrentFrame, Is.EqualTo(4));
            Assert.That(entities.EnemyCount, Is.EqualTo(2),
                "The original fixed start frame must still start the next group when enemies remain.");
        }

        [Test]
        public void EmptyFieldDoesNotAdvanceWhileCurrentGroupIsStillSpawning()
        {
            var waves = new[]
            {
                CreateWave(waveId: 1, startFrame: 0, spawnFrameInterval: 2, enemyCount: 2),
                CreateWave(waveId: 2, startFrame: 100)
            };
            var runtime = CreateRuntime(waves, out var entities);

            Assert.That(runtime.Tick(1.1f), Is.True);
            Assert.That(entities.EnemyCount, Is.Zero);
            Assert.That(runtime.Tick(1.1f), Is.True);
            Assert.That(entities.EnemyCount, Is.EqualTo(1),
                "An empty interval inside an unfinished group must not unlock the next group.");

            RemoveAllEnemies(entities);
            Assert.That(runtime.Tick(1f), Is.True);
            Assert.That(entities.EnemyCount, Is.Zero);
            Assert.That(runtime.Tick(1f), Is.True);
            Assert.That(entities.EnemyCount, Is.EqualTo(1),
                "The unfinished current group must continue instead of being skipped.");
        }

        private static ShooterBattleRuntimePort CreateRuntime(
            ShooterSveltoGameplayWaveConfig[] waves,
            out ShooterEntityManager entities)
        {
            var context = new SveltoWorldContext();
            entities = new ShooterEntityManager(context);
            var state = new ShooterBattleState(entities);
            var rules = ShooterBattleRules.Default;
            var simulation = new ShooterBattleSimulation(state, rules);
            var flow = new ShooterSveltoGameplayBattleFlowConfig(
                durationFrames: 1000,
                victoryTargetDefeats: 100,
                maxActiveEnemies: 16,
                waves,
                ShooterSveltoGameplayBattleFlowConfig.DefaultEnemyLoadoutId,
                enemyAttackIntervalFrames: 1000,
                enemyAttackDamage: 1,
                ShooterSveltoGameplayBattleFlowConfig.DefaultEnemyProjectileSpeedScale,
                ShooterSveltoGameplayBattleFlowConfig.DefaultEnemyProjectilesPerShot,
                ShooterSveltoGameplayBattleFlowConfig.DefaultEnemySpreadDegrees);
            var runtime = new ShooterBattleRuntimePort(
                state,
                simulation,
                entities,
                rules,
                new ShooterEnemyWaveOptions(true, flow));
            var start = new ShooterStartGamePayload(
                "enemy-wave-advance-test",
                tickRate: 30,
                randomSeed: 701,
                new[] { new ShooterStartPlayer(1, "P1", 0f, 0f) });

            Assert.That(runtime.StartGame(in start), Is.True);
            return runtime;
        }

        private static ShooterSveltoGameplayWaveConfig CreateWave(
            int waveId,
            int startFrame,
            int spawnFrameInterval = 1,
            int enemyCount = 1)
        {
            return new ShooterSveltoGameplayWaveConfig(
                waveId,
                startFrame,
                spawnFrameInterval,
                enemyCount,
                enemyHp: 100,
                spawnRadius: 12f);
        }

        private static void RemoveAllEnemies(ShooterEntityManager entities)
        {
            var enemyIds = new List<int>(entities.EnemyIds);
            entities.BeginStructuralChanges();
            try
            {
                for (var i = 0; i < enemyIds.Count; i++)
                {
                    entities.RemoveEnemy(enemyIds[i]);
                }
            }
            finally
            {
                entities.EndStructuralChanges();
            }
        }
    }
}
