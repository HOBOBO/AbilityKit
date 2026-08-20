using System;
using AbilityKit.Ability.Host.WorldBlueprints;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Demo.Shooter.Runtime
{
    public readonly struct ShooterEnemyBudgetOverride
    {
        public ShooterEnemyBudgetOverride(int enemyBudget)
        {
            EnemyBudget = Math.Max(1, enemyBudget);
        }

        public int EnemyBudget { get; }
    }

    public readonly struct ShooterEnemySimulationOverride
    {
        public ShooterEnemySimulationOverride(bool enabled)
        {
            Enabled = enabled;
        }

        public bool Enabled { get; }
    }

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
            var enemySimulationEnabled = !options.Extensions.TryGetValue(
                    typeof(ShooterEnemySimulationOverride),
                    out var enemySimulationValue)
                || enemySimulationValue is not ShooterEnemySimulationOverride enemySimulationOverride
                || enemySimulationOverride.Enabled;
            var enemyWaveOptions = new ShooterEnemyWaveOptions(enemySimulationEnabled, battleFlow);
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
            var scenario = options.Extensions.TryGetValue(typeof(ShooterSveltoGameplayScenarioConfig), out var value) &&
                           value is ShooterSveltoGameplayScenarioConfig configuredScenario
                ? configuredScenario
                : ShooterSveltoGameplayScenarioCatalog.WaveSurvival;
            if (!options.Extensions.TryGetValue(typeof(ShooterEnemyBudgetOverride), out var budgetValue) ||
                budgetValue is not ShooterEnemyBudgetOverride budgetOverride)
            {
                return scenario;
            }

            return WithEnemyBudget(in scenario, budgetOverride.EnemyBudget);
        }

        private static ShooterSveltoGameplayScenarioConfig WithEnemyBudget(
            in ShooterSveltoGameplayScenarioConfig scenario,
            int enemyBudget)
        {
            const int enemiesPerWave = 64;
            var battleFlow = scenario.BattleFlow;
            var normalizedBudget = Math.Max(1, enemyBudget);
            var waveCount = (normalizedBudget - 1) / enemiesPerWave + 1;
            var waves = new ShooterSveltoGameplayWaveConfig[waveCount];
            var remainingEnemies = normalizedBudget;
            for (var i = 0; i < waves.Length; i++)
            {
                var enemiesInWave = Math.Min(enemiesPerWave, remainingEnemies);
                waves[i] = new ShooterSveltoGameplayWaveConfig(
                    waveId: i + 1,
                    startFrame: 0,
                    spawnFrameInterval: 1,
                    enemyCount: enemiesInWave,
                    enemyHp: 2,
                    spawnRadius: (18f + i % 16) * 2f);
                remainingEnemies -= enemiesInWave;
            }

            var scaledFlow = new ShooterSveltoGameplayBattleFlowConfig(
                battleFlow.DurationFrames,
                victoryTargetDefeats: normalizedBudget,
                maxActiveEnemies: normalizedBudget,
                waves,
                battleFlow.EnemyLoadoutId,
                battleFlow.EnemyAttackIntervalFrames,
                battleFlow.EnemyAttackDamage,
                battleFlow.EnemyProjectileSpeedScale,
                battleFlow.EnemyProjectilesPerShot,
                battleFlow.EnemySpreadDegrees);
            return new ShooterSveltoGameplayScenarioConfig(
                scenario.Id,
                scenario.DisplayName,
                scenario.Description,
                scenario.ShooterCount,
                scenario.TargetCount,
                scenario.TickCount,
                scenario.TickDeltaTime,
                scenario.ArenaRadius,
                scenario.Loadout,
                scaledFlow);
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
