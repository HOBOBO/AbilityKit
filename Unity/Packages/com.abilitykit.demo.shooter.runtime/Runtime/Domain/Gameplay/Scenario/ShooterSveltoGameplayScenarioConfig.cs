#nullable enable

using System;
using Newtonsoft.Json;

namespace AbilityKit.Demo.Shooter.Runtime
{
    public readonly struct ShooterSveltoGameplayLoadout
    {
        public ShooterSveltoGameplayLoadout(
            int loadoutId,
            string name,
            float projectileSpeed,
            int projectileLifeFrames,
            int damage,
            int cooldownFrames,
            int projectilesPerShot,
            float spreadDegrees)
        {
            if (loadoutId <= 0) throw new ArgumentOutOfRangeException(nameof(loadoutId));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Loadout name is required.", nameof(name));

            LoadoutId = loadoutId;
            Name = name;
            ProjectileSpeed = projectileSpeed <= 0f ? 1f : projectileSpeed;
            ProjectileLifeFrames = projectileLifeFrames < 1 ? 1 : projectileLifeFrames;
            Damage = damage < 1 ? 1 : damage;
            CooldownFrames = cooldownFrames < 1 ? 1 : cooldownFrames;
            ProjectilesPerShot = projectilesPerShot < 1 ? 1 : projectilesPerShot;
            SpreadDegrees = spreadDegrees < 0f ? 0f : spreadDegrees;
        }

        public int LoadoutId { get; }

        public string Name { get; }

        public float ProjectileSpeed { get; }

        public int ProjectileLifeFrames { get; }

        public int Damage { get; }

        public int CooldownFrames { get; }

        public int ProjectilesPerShot { get; }

        public float SpreadDegrees { get; }
    }

    public readonly struct ShooterSveltoGameplayWaveConfig
    {
        public ShooterSveltoGameplayWaveConfig(
            int waveId,
            int startFrame,
            int spawnFrameInterval,
            int enemyCount,
            int enemyHp,
            float spawnRadius)
        {
            WaveId = waveId < 1 ? 1 : waveId;
            StartFrame = Math.Max(0, startFrame);
            SpawnFrameInterval = spawnFrameInterval < 1 ? 1 : spawnFrameInterval;
            EnemyCount = enemyCount < 1 ? 1 : enemyCount;
            EnemyHp = enemyHp < 1 ? 1 : enemyHp;
            SpawnRadius = spawnRadius <= 0f ? 8f : spawnRadius;
        }

        public int WaveId { get; }

        public int StartFrame { get; }

        public int SpawnFrameInterval { get; }

        public int EnemyCount { get; }

        public int EnemyHp { get; }

        public float SpawnRadius { get; }
    }

    public readonly struct ShooterSveltoGameplayBattleFlowConfig
    {
        public const int DefaultEnemyLoadoutId = 100;
        public const int DefaultEnemyAttackIntervalFrames = 12;
        public const int DefaultEnemyAttackDamage = 1;
        public const float DefaultEnemyProjectileSpeedScale = 0.55f;
        public const int DefaultEnemyProjectilesPerShot = 1;
        public const float DefaultEnemySpreadDegrees = 0f;

        public ShooterSveltoGameplayBattleFlowConfig(
            int durationFrames,
            int victoryTargetDefeats,
            int maxActiveEnemies,
            ShooterSveltoGameplayWaveConfig[] waves)
            : this(
                durationFrames,
                victoryTargetDefeats,
                maxActiveEnemies,
                waves,
                DefaultEnemyLoadoutId,
                DefaultEnemyAttackIntervalFrames,
                DefaultEnemyAttackDamage,
                DefaultEnemyProjectileSpeedScale,
                DefaultEnemyProjectilesPerShot,
                DefaultEnemySpreadDegrees)
        {
        }

        public ShooterSveltoGameplayBattleFlowConfig(
            int durationFrames,
            int victoryTargetDefeats,
            int maxActiveEnemies,
            ShooterSveltoGameplayWaveConfig[] waves,
            int enemyLoadoutId,
            int enemyAttackIntervalFrames,
            int enemyAttackDamage,
            float enemyProjectileSpeedScale,
            int enemyProjectilesPerShot,
            float enemySpreadDegrees)
        {
            DurationFrames = durationFrames < 1 ? 1 : durationFrames;
            VictoryTargetDefeats = victoryTargetDefeats < 1 ? 1 : victoryTargetDefeats;
            MaxActiveEnemies = maxActiveEnemies < 1 ? 1 : maxActiveEnemies;
            Waves = waves is { Length: > 0 } ? (ShooterSveltoGameplayWaveConfig[])waves.Clone() : DefaultWaves();
            EnemyLoadoutId = enemyLoadoutId < 1 ? DefaultEnemyLoadoutId : enemyLoadoutId;
            EnemyAttackIntervalFrames = enemyAttackIntervalFrames < 1 ? DefaultEnemyAttackIntervalFrames : enemyAttackIntervalFrames;
            EnemyAttackDamage = enemyAttackDamage < 1 ? DefaultEnemyAttackDamage : enemyAttackDamage;
            EnemyProjectileSpeedScale = enemyProjectileSpeedScale <= 0f ? DefaultEnemyProjectileSpeedScale : enemyProjectileSpeedScale;
            EnemyProjectilesPerShot = enemyProjectilesPerShot < 1 ? DefaultEnemyProjectilesPerShot : enemyProjectilesPerShot;
            EnemySpreadDegrees = enemySpreadDegrees < 0f ? DefaultEnemySpreadDegrees : enemySpreadDegrees;
        }

        public int DurationFrames { get; }

        public int VictoryTargetDefeats { get; }

        public int MaxActiveEnemies { get; }

        public ShooterSveltoGameplayWaveConfig[] Waves { get; }

        public int EnemyLoadoutId { get; }

        public int EnemyAttackIntervalFrames { get; }

        public int EnemyAttackDamage { get; }

        public float EnemyProjectileSpeedScale { get; }

        public int EnemyProjectilesPerShot { get; }

        public float EnemySpreadDegrees { get; }

        public static ShooterSveltoGameplayBattleFlowConfig Default => ShooterSveltoGameplayScenarioJsonParser.ParseBattleFlow(ShooterSveltoGameplayScenarioJsonCatalog.DefaultBattleFlowJson);

        private static ShooterSveltoGameplayWaveConfig[] DefaultWaves()
        {
            return ShooterSveltoGameplayScenarioJsonParser.ParseBattleFlow(ShooterSveltoGameplayScenarioJsonCatalog.DefaultBattleFlowJson).Waves;
        }
    }

    public readonly struct ShooterSveltoGameplayScenarioConfig
    {
        public ShooterSveltoGameplayScenarioConfig(
            string id,
            string displayName,
            string description,
            int shooterCount,
            int targetCount,
            int tickCount,
            float tickDeltaTime,
            float arenaRadius,
            ShooterSveltoGameplayLoadout loadout)
            : this(
                id,
                displayName,
                description,
                shooterCount,
                targetCount,
                tickCount,
                tickDeltaTime,
                arenaRadius,
                loadout,
                ShooterSveltoGameplayBattleFlowConfig.Default)
        {
        }

        public ShooterSveltoGameplayScenarioConfig(
            string id,
            string displayName,
            string description,
            int shooterCount,
            int targetCount,
            int tickCount,
            float tickDeltaTime,
            float arenaRadius,
            ShooterSveltoGameplayLoadout loadout,
            ShooterSveltoGameplayBattleFlowConfig battleFlow)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Scenario id is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Scenario display name is required.", nameof(displayName));
            if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Scenario description is required.", nameof(description));

            Id = id;
            DisplayName = displayName;
            Description = description;
            ShooterCount = shooterCount < 1 ? 1 : shooterCount;
            TargetCount = targetCount < 1 ? 1 : targetCount;
            TickCount = tickCount < 1 ? 1 : tickCount;
            TickDeltaTime = tickDeltaTime <= 0f ? 1f / 30f : tickDeltaTime;
            ArenaRadius = arenaRadius <= 0f ? 8f : arenaRadius;
            Loadout = loadout.LoadoutId <= 0
                ? ShooterSveltoGameplayScenarioCatalog.DefaultLoadout
                : loadout;
            BattleFlow = battleFlow.DurationFrames <= 0
                ? ShooterSveltoGameplayBattleFlowConfig.Default
                : battleFlow;
        }

        public string Id { get; }

        public string DisplayName { get; }

        public string Description { get; }

        public int ShooterCount { get; }

        public int TargetCount { get; }

        public int TickCount { get; }

        public float TickDeltaTime { get; }

        public float ArenaRadius { get; }

        public ShooterSveltoGameplayLoadout Loadout { get; }

        public ShooterSveltoGameplayBattleFlowConfig BattleFlow { get; }
    }

    public static class ShooterSveltoGameplayScenarioCatalog
    {
        public static ShooterSveltoGameplayLoadout DefaultLoadout { get; } = ShooterSveltoGameplayScenarioJsonParser.ParseLoadout(ShooterSveltoGameplayScenarioJsonCatalog.DefaultLoadoutJson);

        public static ShooterSveltoGameplayScenarioConfig ProjectileStorm { get; } = ShooterSveltoGameplayScenarioJsonParser.ParseScenario(ShooterSveltoGameplayScenarioJsonCatalog.ProjectileStormJson);

        public static ShooterSveltoGameplayScenarioConfig WaveSurvival { get; } = ShooterSveltoGameplayScenarioJsonParser.ParseScenario(ShooterSveltoGameplayScenarioJsonCatalog.WaveSurvivalJson);
    }

    public static class ShooterSveltoGameplayScenarioJsonParser
    {
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore
        };

        public static ShooterSveltoGameplayLoadout ParseLoadout(string json)
        {
            var dto = Deserialize<ShooterSveltoGameplayLoadoutDto>(json, nameof(ShooterSveltoGameplayLoadout));
            return dto.ToConfig();
        }

        public static ShooterSveltoGameplayBattleFlowConfig ParseBattleFlow(string json)
        {
            var dto = ParseBattleFlowDto(json);
            return dto.ToConfig();
        }

        internal static ShooterSveltoGameplayLoadoutDto ParseLoadoutDto(string json)
        {
            return Deserialize<ShooterSveltoGameplayLoadoutDto>(json, nameof(ShooterSveltoGameplayLoadout));
        }

        internal static ShooterSveltoGameplayBattleFlowDto ParseBattleFlowDto(string json)
        {
            return Deserialize<ShooterSveltoGameplayBattleFlowDto>(json, nameof(ShooterSveltoGameplayBattleFlowConfig));
        }

        public static ShooterSveltoGameplayScenarioConfig ParseScenario(string json)
        {
            var dto = Deserialize<ShooterSveltoGameplayScenarioDto>(json, nameof(ShooterSveltoGameplayScenarioConfig));
            return dto.ToConfig();
        }

        private static T Deserialize<T>(string json, string configName)
            where T : class
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException($"{configName} json is required.", nameof(json));
            }

            var dto = JsonConvert.DeserializeObject<T>(json, JsonSettings);
            return dto ?? throw new InvalidOperationException($"{configName} json cannot be parsed.");
        }
    }

    public static class ShooterSveltoGameplayScenarioJsonCatalog
    {
        public const string DefaultLoadoutJson = @"
{
  ""loadoutId"": 1,
  ""name"": ""burst-rifle"",
  ""projectileSpeed"": 18.0,
  ""projectileLifeFrames"": 48,
  ""damage"": 1,
  ""cooldownFrames"": 3,
  ""projectilesPerShot"": 3,
  ""spreadDegrees"": 8.0
}";

        public const string DefaultBattleFlowJson = @"
{
  ""durationFrames"": 120,
  ""victoryTargetDefeats"": 96,
  ""maxActiveEnemies"": 48,
  ""enemyLoadoutId"": 100,
  ""enemyAttackIntervalFrames"": 12,
  ""enemyAttackDamage"": 1,
  ""enemyProjectileSpeedScale"": 0.55,
  ""enemyProjectilesPerShot"": 1,
  ""enemySpreadDegrees"": 0.0,
  ""waves"": [
    { ""waveId"": 1, ""startFrame"": 0, ""spawnFrameInterval"": 1, ""enemyCount"": 36, ""enemyHp"": 2, ""spawnRadius"": 18.0 },
    { ""waveId"": 2, ""startFrame"": 20, ""spawnFrameInterval"": 1, ""enemyCount"": 36, ""enemyHp"": 2, ""spawnRadius"": 22.0 },
    { ""waveId"": 3, ""startFrame"": 40, ""spawnFrameInterval"": 2, ""enemyCount"": 36, ""enemyHp"": 3, ""spawnRadius"": 26.0 }
  ]
}";

        public const string ProjectileStormJson = @"
{
  ""id"": ""svelto-projectile-storm"",
  ""displayName"": ""Svelto Projectile Storm"",
  ""description"": ""大量射手、靶子和子弹全部使用 Svelto struct 组件，按数组批处理推进移动、冷却、命中与生命期。"",
  ""shooterCount"": 64,
  ""targetCount"": 96,
  ""tickCount"": 120,
  ""tickDeltaTime"": 0.033333334,
  ""arenaRadius"": 32.0,
  ""loadout"": {
    ""loadoutId"": 1,
    ""name"": ""burst-rifle"",
    ""projectileSpeed"": 18.0,
    ""projectileLifeFrames"": 48,
    ""damage"": 1,
    ""cooldownFrames"": 3,
    ""projectilesPerShot"": 3,
    ""spreadDegrees"": 8.0
  },
  ""battleFlow"": {
    ""durationFrames"": 120,
    ""victoryTargetDefeats"": 96,
    ""maxActiveEnemies"": 48,
    ""enemyLoadoutId"": 100,
    ""enemyAttackIntervalFrames"": 12,
    ""enemyAttackDamage"": 1,
    ""enemyProjectileSpeedScale"": 0.55,
    ""enemyProjectilesPerShot"": 1,
    ""enemySpreadDegrees"": 0.0,
    ""waves"": [
      { ""waveId"": 1, ""startFrame"": 0, ""spawnFrameInterval"": 1, ""enemyCount"": 36, ""enemyHp"": 2, ""spawnRadius"": 18.0 },
      { ""waveId"": 2, ""startFrame"": 20, ""spawnFrameInterval"": 1, ""enemyCount"": 36, ""enemyHp"": 2, ""spawnRadius"": 22.0 },
      { ""waveId"": 3, ""startFrame"": 40, ""spawnFrameInterval"": 2, ""enemyCount"": 36, ""enemyHp"": 3, ""spawnRadius"": 26.0 }
    ]
  }
}";

        public const string WaveSurvivalJson = @"
{
  ""id"": ""svelto-wave-survival"",
  ""displayName"": ""Svelto Wave Survival"",
  ""description"": ""使用游戏时间、敌人生成与波次刷新配置驱动的 Shooter 玩法样板，Unity 侧只需要选择该配置即可运行。"",
  ""shooterCount"": 4,
  ""targetCount"": 96,
  ""tickCount"": 180,
  ""tickDeltaTime"": 0.033333334,
  ""arenaRadius"": 36.0,
  ""loadout"": {
    ""loadoutId"": 1,
    ""name"": ""burst-rifle"",
    ""projectileSpeed"": 18.0,
    ""projectileLifeFrames"": 48,
    ""damage"": 1,
    ""cooldownFrames"": 3,
    ""projectilesPerShot"": 3,
    ""spreadDegrees"": 8.0
  },
  ""battleFlow"": {
    ""durationFrames"": 180,
    ""victoryTargetDefeats"": 96,
    ""maxActiveEnemies"": 36,
    ""enemyLoadoutId"": 100,
    ""enemyAttackIntervalFrames"": 12,
    ""enemyAttackDamage"": 1,
    ""enemyProjectileSpeedScale"": 0.55,
    ""enemyProjectilesPerShot"": 1,
    ""enemySpreadDegrees"": 0.0,
    ""waves"": [
      { ""waveId"": 1, ""startFrame"": 0, ""spawnFrameInterval"": 1, ""enemyCount"": 32, ""enemyHp"": 2, ""spawnRadius"": 18.0 },
      { ""waveId"": 2, ""startFrame"": 30, ""spawnFrameInterval"": 1, ""enemyCount"": 32, ""enemyHp"": 2, ""spawnRadius"": 22.0 },
      { ""waveId"": 3, ""startFrame"": 60, ""spawnFrameInterval"": 2, ""enemyCount"": 32, ""enemyHp"": 3, ""spawnRadius"": 26.0 }
    ]
  }
}";
    }

    internal sealed class ShooterSveltoGameplayLoadoutDto
    {
        public int LoadoutId { get; set; }
        public string? Name { get; set; }
        public float ProjectileSpeed { get; set; }
        public int ProjectileLifeFrames { get; set; }
        public int Damage { get; set; }
        public int CooldownFrames { get; set; }
        public int ProjectilesPerShot { get; set; }
        public float SpreadDegrees { get; set; }

        public ShooterSveltoGameplayLoadout ToConfig()
        {
            return new ShooterSveltoGameplayLoadout(
                LoadoutId,
                Name ?? string.Empty,
                ProjectileSpeed,
                ProjectileLifeFrames,
                Damage,
                CooldownFrames,
                ProjectilesPerShot,
                SpreadDegrees);
        }
    }

    internal sealed class ShooterSveltoGameplayWaveDto
    {
        public int WaveId { get; set; }
        public int StartFrame { get; set; }
        public int SpawnFrameInterval { get; set; }
        public int EnemyCount { get; set; }
        public int EnemyHp { get; set; }
        public float SpawnRadius { get; set; }

        public ShooterSveltoGameplayWaveConfig ToConfig()
        {
            return new ShooterSveltoGameplayWaveConfig(
                WaveId,
                StartFrame,
                SpawnFrameInterval,
                EnemyCount,
                EnemyHp,
                SpawnRadius);
        }
    }

    internal sealed class ShooterSveltoGameplayBattleFlowDto
    {
        public int DurationFrames { get; set; }
        public int VictoryTargetDefeats { get; set; }
        public int MaxActiveEnemies { get; set; }
        public int EnemyLoadoutId { get; set; }
        public int EnemyAttackIntervalFrames { get; set; }
        public int EnemyAttackDamage { get; set; }
        public float EnemyProjectileSpeedScale { get; set; }
        public int EnemyProjectilesPerShot { get; set; }
        public float EnemySpreadDegrees { get; set; }
        public ShooterSveltoGameplayWaveDto[]? Waves { get; set; }

        public ShooterSveltoGameplayBattleFlowConfig ToConfig()
        {
            var waves = Waves is { Length: > 0 }
                ? Array.ConvertAll(Waves, wave => wave.ToConfig())
                : Array.Empty<ShooterSveltoGameplayWaveConfig>();

            return new ShooterSveltoGameplayBattleFlowConfig(
                DurationFrames,
                VictoryTargetDefeats,
                MaxActiveEnemies,
                waves,
                EnemyLoadoutId,
                EnemyAttackIntervalFrames,
                EnemyAttackDamage,
                EnemyProjectileSpeedScale,
                EnemyProjectilesPerShot,
                EnemySpreadDegrees);
        }
    }

    internal sealed class ShooterSveltoGameplayScenarioDto
    {
        public string? Id { get; set; }
        public string? DisplayName { get; set; }
        public string? Description { get; set; }
        public int ShooterCount { get; set; }
        public int TargetCount { get; set; }
        public int TickCount { get; set; }
        public float TickDeltaTime { get; set; }
        public float ArenaRadius { get; set; }
        public ShooterSveltoGameplayLoadoutDto? Loadout { get; set; }
        public ShooterSveltoGameplayBattleFlowDto? BattleFlow { get; set; }

        public ShooterSveltoGameplayScenarioConfig ToConfig()
        {
            return new ShooterSveltoGameplayScenarioConfig(
                Id ?? string.Empty,
                DisplayName ?? string.Empty,
                Description ?? string.Empty,
                ShooterCount,
                TargetCount,
                TickCount,
                TickDeltaTime,
                ArenaRadius,
                (Loadout ?? ShooterSveltoGameplayScenarioJsonParser.ParseLoadoutDto(ShooterSveltoGameplayScenarioJsonCatalog.DefaultLoadoutJson)).ToConfig(),
                (BattleFlow ?? ShooterSveltoGameplayScenarioJsonParser.ParseBattleFlowDto(ShooterSveltoGameplayScenarioJsonCatalog.DefaultBattleFlowJson)).ToConfig());
        }
    }

}
