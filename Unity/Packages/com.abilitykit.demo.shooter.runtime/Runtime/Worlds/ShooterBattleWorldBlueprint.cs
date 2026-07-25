using System;
using AbilityKit.Ability.Host.WorldBlueprints;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Demo.Shooter.Runtime
{
    public readonly struct ShooterBattleFlowOverrides
    {
        public ShooterBattleFlowOverrides(int durationFrames, int victoryTargetDefeats)
            : this(durationFrames, victoryTargetDefeats, continueAfterAllPlayersDefeated: false)
        {
        }

        public ShooterBattleFlowOverrides(
            int durationFrames,
            int victoryTargetDefeats,
            bool continueAfterAllPlayersDefeated)
        {
            DurationFrames = durationFrames;
            VictoryTargetDefeats = victoryTargetDefeats;
            ContinueAfterAllPlayersDefeated = continueAfterAllPlayersDefeated;
        }

        public int DurationFrames { get; }

        public int VictoryTargetDefeats { get; }

        public bool ContinueAfterAllPlayersDefeated { get; }
    }

    public sealed class ShooterBattleWorldBlueprint : IWorldBlueprint
    {
        public string WorldType => ShooterGameplay.WorldType;

        public void Configure(WorldCreateOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            options.WorldType = ShooterGameplay.WorldType;
            options.ServiceBuilder ??= WorldServiceContainerFactory.CreateDefaultOnly();
            var scenario = ResolveScenario(options);
            var battleFlow = CreateBattleFlow(scenario.BattleFlow, options);
            var enemyWaveOptions = new ShooterEnemyWaveOptions(true, battleFlow);
            var arenaOptions = ShooterArenaGameplayOptions.CreateCircular(scenario.ArenaRadius);
            var matchStateOptions = CreateMatchStateOptions(options);
            options.ServiceBuilder.Register<ShooterEnemyWaveOptions>(WorldLifetime.Singleton, _ => enemyWaveOptions);
            options.ServiceBuilder.Register<ShooterArenaGameplayOptions>(WorldLifetime.Singleton, _ => arenaOptions);
            options.ServiceBuilder.Register<ShooterMatchStateOptions>(WorldLifetime.Singleton, _ => matchStateOptions);
            options.Modules.Add(new ShooterWorldModule());
        }

        private static ShooterMatchStateOptions CreateMatchStateOptions(WorldCreateOptions options)
        {
            return options.Extensions.TryGetValue(typeof(ShooterBattleFlowOverrides), out var overridesValue) &&
                   overridesValue is ShooterBattleFlowOverrides overrides &&
                   overrides.ContinueAfterAllPlayersDefeated
                ? ShooterMatchStateOptions.NonTerminatingDefeat
                : ShooterMatchStateOptions.Default;
        }

        private static ShooterSveltoGameplayScenarioConfig ResolveScenario(WorldCreateOptions options)
        {
            return options.Extensions.TryGetValue(typeof(ShooterSveltoGameplayScenarioConfig), out var value) &&
                   value is ShooterSveltoGameplayScenarioConfig scenario
                ? scenario
                : ShooterSveltoGameplayScenarioCatalog.WaveSurvival;
        }

        private static ShooterSveltoGameplayBattleFlowConfig CreateBattleFlow(
            ShooterSveltoGameplayBattleFlowConfig battleFlow,
            WorldCreateOptions options)
        {
            var durationFrames = battleFlow.DurationFrames;
            var victoryTargetDefeats = battleFlow.VictoryTargetDefeats;
            if (options.Extensions.TryGetValue(typeof(ShooterBattleFlowOverrides), out var overridesValue) &&
                overridesValue is ShooterBattleFlowOverrides overrides)
            {
                durationFrames = overrides.DurationFrames > 0 ? overrides.DurationFrames : durationFrames;
                victoryTargetDefeats = overrides.VictoryTargetDefeats > 0
                    ? overrides.VictoryTargetDefeats
                    : victoryTargetDefeats;
            }
            else if (options.Extensions.TryGetValue(typeof(ShooterGameplay), out var durationValue) &&
                     durationValue is int legacyDurationFrames &&
                     legacyDurationFrames > 0)
            {
                durationFrames = legacyDurationFrames;
            }

            if (durationFrames == battleFlow.DurationFrames &&
                victoryTargetDefeats == battleFlow.VictoryTargetDefeats)
            {
                return battleFlow;
            }

            return new ShooterSveltoGameplayBattleFlowConfig(
                durationFrames,
                victoryTargetDefeats,
                battleFlow.MaxActiveEnemies,
                battleFlow.Waves,
                battleFlow.EnemyLoadoutId,
                battleFlow.EnemyAttackIntervalFrames,
                battleFlow.EnemyAttackDamage,
                battleFlow.EnemyProjectileSpeedScale,
                battleFlow.EnemyProjectilesPerShot,
                battleFlow.EnemySpreadDegrees);
        }
    }
}
