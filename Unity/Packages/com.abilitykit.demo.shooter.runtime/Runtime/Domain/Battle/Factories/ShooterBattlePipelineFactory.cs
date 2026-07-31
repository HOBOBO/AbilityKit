using System;
using System.Collections.Generic;
using AbilityKit.Game.Battle;

namespace AbilityKit.Demo.Shooter.Runtime
{
    internal interface IShooterBattlePipelineFactory
    {
        ShooterBattleSveltoStepEngine Create(ShooterBattleServiceContext services);
    }

    internal sealed class ShooterBattlePipelineFactory : IShooterBattlePipelineFactory
    {
        public ShooterBattleSveltoStepEngine Create(ShooterBattleServiceContext services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            var plan = BattleSystemContributionPlanner.Create<ShooterBattleServiceContext, IShooterBattleSystem>(
                CreateDefaultContributions());
            return new ShooterBattleSveltoStepEngine(plan.CreateSystems(services));
        }

        internal static IReadOnlyList<IBattleSystemContribution<ShooterBattleServiceContext, IShooterBattleSystem>> CreateDefaultContributions()
        {
            return new IBattleSystemContribution<ShooterBattleServiceContext, IShooterBattleSystem>[]
            {
                Contribute("shooter.frame.begin", ShooterBattleSystemOrder.BeginFrame, services => new ShooterFrameBeginBattleSystem(services)),
                Contribute("shooter.bot_ai", ShooterBattleSystemOrder.PlayerBotAi, services => new ShooterBotAiServiceBattleSystem(services)),
                Contribute("shooter.enemy_wave.spawn", ShooterBattleSystemOrder.EnemyWaveSpawn, services => new ShooterEnemyWaveBattleSystem(services, ShooterEnemyWavePhase.Spawn)),
                Contribute("shooter.enemy.movement_intent", ShooterBattleSystemOrder.EnemyMovementIntent, services => new ShooterEnemyMovementIntentBattleSystem(services)),
                Contribute("shooter.enemy.rvo_solve", ShooterBattleSystemOrder.EnemyRvoSolve, services => new ShooterEnemyRvoSolveBattleSystem(services)),
                Contribute("shooter.enemy.movement_integration", ShooterBattleSystemOrder.EnemyMovementIntegration, services => new ShooterEnemyMovementIntegrationBattleSystem(services)),
                Contribute("shooter.simulation", ShooterBattleSystemOrder.Simulation, services => new ShooterSimulationBattleSystem(services)),
                Contribute("shooter.enemy.cleanup", ShooterBattleSystemOrder.EnemyLifecycleCleanup, services => new ShooterEnemyLifecycleCleanupBattleSystem(services)),
                Contribute("shooter.enemy_wave.attack", ShooterBattleSystemOrder.EnemyWaveAttack, services => new ShooterEnemyWaveBattleSystem(services, ShooterEnemyWavePhase.Attack)),
                Contribute("shooter.match_state", ShooterBattleSystemOrder.MatchState, services => new ShooterMatchStateBattleSystem(services)),
            };
        }

        private static BattleSystemContribution<ShooterBattleServiceContext, IShooterBattleSystem> Contribute(
            string id,
            int order,
            Func<ShooterBattleServiceContext, IShooterBattleSystem> factory)
        {
            return new BattleSystemContribution<ShooterBattleServiceContext, IShooterBattleSystem>(id, order, factory);
        }
    }
}
