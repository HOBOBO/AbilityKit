using System;

namespace AbilityKit.Demo.Shooter.Runtime
{
    public sealed class ShooterEnemyWaveOptions
    {
        public static ShooterEnemyWaveOptions Disabled { get; } = new ShooterEnemyWaveOptions(false);

        public static ShooterEnemyWaveOptions EnabledOption { get; } = new ShooterEnemyWaveOptions(true);

        public static ShooterEnemyWaveOptions DefaultEnabled { get; } = new ShooterEnemyWaveOptions(true, ShooterSveltoGameplayBattleFlowConfig.Default);

        public const float DefaultEnemyAttackRange = 1.1f;

        public const int DefaultMaxEnemyAttackersPerPlayer = 3;

        public ShooterEnemyWaveOptions(bool enabled)
            : this(enabled, ShooterSveltoGameplayBattleFlowConfig.Default)
        {
        }

        public ShooterEnemyWaveOptions(bool enabled, ShooterSveltoGameplayBattleFlowConfig battleFlow)
            : this(enabled, battleFlow, battleFlow.EnemyAttackIntervalFrames, battleFlow.EnemyAttackDamage)
        {
        }

        public ShooterEnemyWaveOptions(
            bool enabled,
            ShooterSveltoGameplayBattleFlowConfig battleFlow,
            int enemyAttackIntervalFrames,
            int enemyAttackDamage)
            : this(
                enabled,
                battleFlow,
                enemyAttackIntervalFrames,
                enemyAttackDamage,
                DefaultEnemyAttackRange,
                DefaultMaxEnemyAttackersPerPlayer)
        {
        }

        public ShooterEnemyWaveOptions(
            bool enabled,
            ShooterSveltoGameplayBattleFlowConfig battleFlow,
            int enemyAttackIntervalFrames,
            int enemyAttackDamage,
            float enemyAttackRange,
            int maxEnemyAttackersPerPlayer)
        {
            Enabled = enabled;
            BattleFlow = battleFlow.DurationFrames <= 0
                ? ShooterSveltoGameplayBattleFlowConfig.Default
                : battleFlow;
            Waves = BattleFlow.Waves is { Length: > 0 }
                ? (ShooterSveltoGameplayWaveConfig[])BattleFlow.Waves.Clone()
                : Array.Empty<ShooterSveltoGameplayWaveConfig>();
            EnemyAttackIntervalFrames = enemyAttackIntervalFrames < 1 ? 1 : enemyAttackIntervalFrames;
            EnemyAttackDamage = enemyAttackDamage < 1 ? 1 : enemyAttackDamage;
            EnemyAttackRange = enemyAttackRange <= 0f ? DefaultEnemyAttackRange : enemyAttackRange;
            MaxEnemyAttackersPerPlayer = maxEnemyAttackersPerPlayer < 1
                ? DefaultMaxEnemyAttackersPerPlayer
                : maxEnemyAttackersPerPlayer;
        }

        public bool Enabled { get; }

        public ShooterSveltoGameplayBattleFlowConfig BattleFlow { get; }

        public int DurationFrames => Enabled ? BattleFlow.DurationFrames : 0;

        public int VictoryTargetDefeats => BattleFlow.VictoryTargetDefeats;

        public int MaxActiveEnemies => BattleFlow.MaxActiveEnemies;

        public ShooterSveltoGameplayWaveConfig[] Waves { get; }

        public int EnemyAttackIntervalFrames { get; }

        public int EnemyAttackDamage { get; }

        public float EnemyAttackRange { get; }

        public int MaxEnemyAttackersPerPlayer { get; }
    }
}
