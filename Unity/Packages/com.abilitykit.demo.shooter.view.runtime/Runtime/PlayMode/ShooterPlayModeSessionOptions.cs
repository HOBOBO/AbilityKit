#nullable enable

using System;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Demo.Shooter.View;
using AbilityKit.Network.Runtime;

namespace AbilityKit.Demo.Shooter.View.PlayMode
{
    public readonly struct ShooterPlayModeSessionOptions
    {
        private const int PlayModeDefaultDurationFrames = ShooterAcceptanceLab.DefaultTickRate * 600;
        private const int PlayModeEnemiesPerWave = 64;
        private const int PlayModeCampaignReinforcementGroups = 5;
        private const int PlayModeReinforcementIntervalSeconds = 60;

        public const int PlayModeDefaultEnemyBudget = 512;
        public const int PlayModeMediumEnemyBudget = 2048;
        public const int PlayModeHighDensityEnemyBudget = 8192;

        public static ShooterPlayModeSessionOptions Default => FromTemplate(
            ShooterAcceptanceCatalog.GetSyncTemplate(ShooterRoomLaunchSpec.DefaultSyncTemplateId));

        public static ShooterPlayModeSessionOptions FromTemplate(in ShooterSyncTemplate template)
        {
            return FromTemplateForNetwork(
                in template,
                template.NetworkEnvironmentId,
                randomSeed: 3901,
                controlledPlayerId: 1,
                worldScale: 1f);
        }

        public static ShooterPlayModeSessionOptions FromTemplateForNetwork(
            in ShooterSyncTemplate template,
            string networkEnvironmentId,
            int randomSeed,
            int controlledPlayerId,
            float worldScale)
        {
            var network = ShooterAcceptanceCatalog.GetNetworkEnvironment(networkEnvironmentId);
            return new ShooterPlayModeSessionOptions(
                template.SyncModel,
                ShooterAcceptanceLab.DefaultTickRate,
                template.RecommendedPlayerCount,
                randomSeed,
                controlledPlayerId,
                enableAuthoritativeWorld: template.EnableAuthoritativeWorld,
                latencyMs: network.Profile.BaseLatencyMs,
                jitterMs: network.Profile.JitterMs,
                packetLossRate: (float)network.Profile.PacketLossRate,
                reorderRate: (float)network.Profile.ReorderRate,
                bandwidthKbps: network.Profile.BandwidthKbps,
                worldScale,
                networkName: network.DisplayName,
                syncTemplateId: template.Id,
                gameplayScenario: CreatePlayModeScenario(PlayModeDefaultEnemyBudget));
        }

        public static ShooterPlayModeSessionOptions FromTemplateId(string templateId)
        {
            return FromTemplate(ShooterAcceptanceCatalog.GetSyncTemplate(templateId));
        }

        public static ShooterPlayModeSessionOptions FromTemplate(
            in ShooterSyncTemplate template,
            int randomSeed,
            int controlledPlayerId,
            float worldScale)
        {
            var options = FromTemplate(in template);
            return new ShooterPlayModeSessionOptions(
                options.SyncModel,
                options.TickRate,
                options.PlayerCount,
                randomSeed,
                controlledPlayerId,
                options.EnableAuthoritativeWorld,
                options.LatencyMs,
                options.JitterMs,
                options.PacketLossRate,
                options.ReorderRate,
                options.BandwidthKbps,
                worldScale,
                options.NetworkName,
                options.SyncTemplateId,
                options.GameplayScenario);
        }

        public static ShooterPlayModeSessionOptions FromTemplate(
            in ShooterSyncTemplate template,
            in ShooterSveltoGameplayScenarioConfig gameplayScenario)
        {
            var options = FromTemplate(in template);
            return options.WithGameplayScenario(in gameplayScenario);
        }

        public static ShooterPlayModeSessionOptions FromTemplateAndScenarioJson(
            in ShooterSyncTemplate template,
            string scenarioJson)
        {
            return FromTemplate(in template, ShooterSveltoGameplayScenarioJsonParser.ParseScenario(scenarioJson));
        }

        public static ShooterPlayModeSessionOptions FromTemplateAndScenarioSource(
            in ShooterSyncTemplate template,
            IShooterSveltoGameplayScenarioSource scenarioSource,
            string scenarioId)
        {
            if (scenarioSource == null) throw new ArgumentNullException(nameof(scenarioSource));
            if (!scenarioSource.TryGetScenario(scenarioId, out var scenario))
            {
                throw new ArgumentException($"Gameplay scenario '{scenarioId}' was not found.", nameof(scenarioId));
            }

            return FromTemplate(in template, in scenario);
        }

        public static ShooterPlayModeSessionOptions FromTemplateIdAndScenarioJson(
            string templateId,
            string scenarioJson)
        {
            return FromTemplateAndScenarioJson(ShooterAcceptanceCatalog.GetSyncTemplate(templateId), scenarioJson);
        }

        public static ShooterPlayModeSessionOptions FromTemplateIdAndScenarioSource(
            string templateId,
            IShooterSveltoGameplayScenarioSource scenarioSource,
            string scenarioId)
        {
            return FromTemplateAndScenarioSource(ShooterAcceptanceCatalog.GetSyncTemplate(templateId), scenarioSource, scenarioId);
        }

        public static ShooterPlayModeSessionOptions LegacyDefault => new(
            NetworkSyncModel.PredictRollback,
            ShooterAcceptanceLab.DefaultTickRate,
            playerCount: 2,
            randomSeed: 3901,
            controlledPlayerId: 1,
            enableAuthoritativeWorld: true,
            latencyMs: 0,
            jitterMs: 0,
            packetLossRate: 0f,
            reorderRate: 0f,
            bandwidthKbps: 0,
            worldScale: 1f,
            networkName: null,
            syncTemplateId: null,
            gameplayScenario: ShooterSveltoGameplayScenarioCatalog.ProjectileStorm);

        public ShooterPlayModeSessionOptions(
            NetworkSyncModel syncModel,
            int tickRate,
            int playerCount,
            int randomSeed,
            int controlledPlayerId,
            bool enableAuthoritativeWorld,
            int latencyMs,
            int jitterMs,
            float packetLossRate,
            float reorderRate,
            int bandwidthKbps,
            float worldScale,
            string? networkName,
            string? syncTemplateId = null)
            : this(
                syncModel,
                tickRate,
                playerCount,
                randomSeed,
                controlledPlayerId,
                enableAuthoritativeWorld,
                latencyMs,
                jitterMs,
                packetLossRate,
                reorderRate,
                bandwidthKbps,
                worldScale,
                networkName,
                syncTemplateId,
                CreatePlayModeDefaultScenario(ShooterSveltoGameplayScenarioCatalog.WaveSurvival))
        {
        }

        public ShooterPlayModeSessionOptions(
            NetworkSyncModel syncModel,
            int tickRate,
            int playerCount,
            int randomSeed,
            int controlledPlayerId,
            bool enableAuthoritativeWorld,
            int latencyMs,
            int jitterMs,
            float packetLossRate,
            float reorderRate,
            int bandwidthKbps,
            float worldScale,
            string? networkName,
            string? syncTemplateId,
            ShooterSveltoGameplayScenarioConfig gameplayScenario)
        {
            SyncModel = syncModel;
            TickRate = tickRate;
            PlayerCount = playerCount;
            RandomSeed = randomSeed;
            ControlledPlayerId = controlledPlayerId;
            EnableAuthoritativeWorld = enableAuthoritativeWorld;
            LatencyMs = latencyMs;
            JitterMs = jitterMs;
            PacketLossRate = packetLossRate;
            ReorderRate = reorderRate;
            BandwidthKbps = bandwidthKbps;
            WorldScale = worldScale;
            NetworkName = networkName;
            SyncTemplateId = syncTemplateId;
            GameplayScenario = string.IsNullOrWhiteSpace(gameplayScenario.Id)
                ? CreatePlayModeDefaultScenario(ShooterSveltoGameplayScenarioCatalog.WaveSurvival)
                : gameplayScenario;
        }

        public NetworkSyncModel SyncModel { get; }
        public int TickRate { get; }
        public int PlayerCount { get; }
        public int RandomSeed { get; }
        public int ControlledPlayerId { get; }
        public bool EnableAuthoritativeWorld { get; }
        public int LatencyMs { get; }
        public int JitterMs { get; }
        public float PacketLossRate { get; }
        public float ReorderRate { get; }
        public int BandwidthKbps { get; }
        public float WorldScale { get; }
        public string? NetworkName { get; }
        public string? SyncTemplateId { get; }
        public ShooterSveltoGameplayScenarioConfig GameplayScenario { get; }

        public ShooterPlayModeSessionOptions WithGameplayScenarioJson(string scenarioJson)
        {
            return WithGameplayScenario(ShooterSveltoGameplayScenarioJsonParser.ParseScenario(scenarioJson));
        }

        public ShooterPlayModeSessionOptions WithGameplayScenarioSource(
            IShooterSveltoGameplayScenarioSource scenarioSource,
            string scenarioId)
        {
            if (scenarioSource == null) throw new ArgumentNullException(nameof(scenarioSource));
            if (!scenarioSource.TryGetScenario(scenarioId, out var scenario))
            {
                throw new ArgumentException($"Gameplay scenario '{scenarioId}' was not found.", nameof(scenarioId));
            }

            return WithGameplayScenario(in scenario);
        }

        public ShooterPlayModeSessionOptions WithGameplayScenario(in ShooterSveltoGameplayScenarioConfig gameplayScenario)
        {
            return new ShooterPlayModeSessionOptions(
                SyncModel,
                TickRate,
                PlayerCount,
                RandomSeed,
                ControlledPlayerId,
                EnableAuthoritativeWorld,
                LatencyMs,
                JitterMs,
                PacketLossRate,
                ReorderRate,
                BandwidthKbps,
                WorldScale,
                NetworkName,
                SyncTemplateId,
                gameplayScenario);
        }

        public ShooterPlayModeSessionOptions Normalized()
        {
            var tickRate = TickRate <= 0 ? ShooterAcceptanceLab.DefaultTickRate : TickRate;
            var playerCount = Math.Max(1, PlayerCount);
            var controlledPlayerId = Math.Min(Math.Max(ControlledPlayerId, 1), playerCount);
            var worldScale = WorldScale <= 0f ? 1f : WorldScale;
            var gameplayScenario = string.IsNullOrWhiteSpace(GameplayScenario.Id)
                ? CreatePlayModeDefaultScenario(ShooterSveltoGameplayScenarioCatalog.WaveSurvival)
                : GameplayScenario;

            return new ShooterPlayModeSessionOptions(
                SyncModel,
                tickRate,
                playerCount,
                RandomSeed,
                controlledPlayerId,
                EnableAuthoritativeWorld,
                Math.Max(0, LatencyMs),
                Math.Max(0, JitterMs),
                Clamp01(PacketLossRate),
                Clamp01(ReorderRate),
                Math.Max(0, BandwidthKbps),
                worldScale,
                NetworkName,
                SyncTemplateId,
                gameplayScenario);
        }

        public static ShooterSveltoGameplayScenarioConfig CreatePlayModeScenario(int enemyBudget)
        {
            return CreatePlayModeScenario(ShooterSveltoGameplayScenarioCatalog.WaveSurvival, enemyBudget);
        }

        private static ShooterSveltoGameplayScenarioConfig CreatePlayModeDefaultScenario(in ShooterSveltoGameplayScenarioConfig scenario)
        {
            return CreatePlayModeScenario(in scenario, PlayModeDefaultEnemyBudget);
        }

        private static ShooterSveltoGameplayScenarioConfig CreatePlayModeScenario(
            in ShooterSveltoGameplayScenarioConfig scenario,
            int enemyBudget)
        {
            var battleFlow = scenario.BattleFlow;
            var tickRate = Math.Max(1, (int)Math.Round(1f / scenario.TickDeltaTime));
            var durationFrames = Math.Max(battleFlow.DurationFrames, PlayModeDefaultDurationFrames);
            var normalizedEnemyBudget = Math.Min(
                Math.Max(1, enemyBudget),
                PlayModeHighDensityEnemyBudget);
            var campaignEnemyBudget = SaturatingMultiply(normalizedEnemyBudget, PlayModeCampaignReinforcementGroups);
            var playModeWaves = CreatePlayModeDefaultWaves(normalizedEnemyBudget, campaignEnemyBudget, tickRate);
            var playModeFlow = new ShooterSveltoGameplayBattleFlowConfig(
                durationFrames,
                victoryTargetDefeats: campaignEnemyBudget,
                maxActiveEnemies: normalizedEnemyBudget,
                playModeWaves,
                battleFlow.EnemyLoadoutId,
                enemyAttackIntervalFrames: tickRate * 2,
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
                Math.Max(scenario.TickCount, durationFrames),
                scenario.TickDeltaTime,
                scenario.ArenaRadius,
                scenario.Loadout,
                playModeFlow);
        }

        private static ShooterSveltoGameplayWaveConfig[] CreatePlayModeDefaultWaves(
            int activeEnemyBudget,
            int campaignEnemyBudget,
            int tickRate)
        {
            var waveCount = DivideRoundUp(campaignEnemyBudget, PlayModeEnemiesPerWave);
            var initialWaveCount = DivideRoundUp(activeEnemyBudget, PlayModeEnemiesPerWave);
            var waves = new ShooterSveltoGameplayWaveConfig[waveCount];
            var remainingEnemies = campaignEnemyBudget;
            for (var i = 0; i < waves.Length; i++)
            {
                var enemiesInWave = Math.Min(PlayModeEnemiesPerWave, remainingEnemies);
                var reinforcementGroup = i / initialWaveCount;
                waves[i] = new ShooterSveltoGameplayWaveConfig(
                    waveId: i + 1,
                    startFrame: reinforcementGroup * tickRate * PlayModeReinforcementIntervalSeconds,
                    spawnFrameInterval: 1,
                    enemyCount: enemiesInWave,
                    enemyHp: 2 + reinforcementGroup * 2,
                    spawnRadius: (18f + i % 16) * 2f);
                remainingEnemies -= enemiesInWave;
            }

            return waves;
        }

        private static int SaturatingMultiply(int value, int multiplier)
        {
            return value > int.MaxValue / multiplier ? int.MaxValue : value * multiplier;
        }

        private static int DivideRoundUp(int value, int divisor)
        {
            return Math.Max(1, (value - 1) / divisor + 1);
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            return value > 1f ? 1f : value;
        }
    }
}
