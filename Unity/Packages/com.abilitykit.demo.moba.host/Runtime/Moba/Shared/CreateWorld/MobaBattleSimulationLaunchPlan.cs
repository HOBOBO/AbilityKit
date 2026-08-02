using AbilityKit.Ability.Host;
using AbilityKit.Protocol.Moba;

namespace AbilityKit.Ability.Host.Extensions.Moba.CreateWorld
{
    public readonly struct MobaBattleSimulationLaunchPlan
    {
        public readonly MobaBattleLaunchSpec BaseSpec;
        public readonly MobaBattleLaunchInstanceSpec[] Instances;
        public readonly bool ShareRandomSeed;
        public readonly int SeedStep;

        public MobaBattleSimulationLaunchPlan(
            in MobaBattleLaunchSpec baseSpec,
            MobaBattleLaunchInstanceSpec[] instances,
            bool shareRandomSeed = true,
            int seedStep = 1)
        {
            BaseSpec = baseSpec;
            Instances = instances;
            ShareRandomSeed = shareRandomSeed;
            SeedStep = seedStep == 0 ? 1 : seedStep;
        }

        public int Count => Instances == null ? 0 : Instances.Length;

        public MobaBattleLaunchSpec BuildSpec(int index)
        {
            if (Instances == null || index < 0 || index >= Instances.Length)
            {
                return BaseSpec;
            }

            var instance = Instances[index];
            var seed = ShareRandomSeed ? BaseSpec.RandomSeed : BaseSpec.RandomSeed + index * SeedStep;

            return new MobaBattleLaunchSpec(
                battleId: instance.OverrideBattleId(BaseSpec.BattleId),
                matchId: instance.OverrideMatchId(BaseSpec.MatchId),
                worldId: instance.OverrideWorldId(BaseSpec.WorldId),
                worldType: instance.OverrideWorldType(BaseSpec.WorldType),
                clientId: instance.OverrideClientId(BaseSpec.ClientId),
                localPlayerId: instance.LocalPlayerId.Value == null ? BaseSpec.LocalPlayerId : instance.LocalPlayerId,
                mapId: BaseSpec.MapId,
                gameplayId: BaseSpec.GameplayId,
                ruleSetId: BaseSpec.RuleSetId,
                configVersion: BaseSpec.ConfigVersion,
                protocolVersion: BaseSpec.ProtocolVersion,
                randomSeed: seed,
                tickRate: BaseSpec.TickRate,
                inputDelayFrames: BaseSpec.InputDelayFrames,
                launchMode: instance.LaunchMode == MobaBattleLaunchMode.Unspecified ? BaseSpec.LaunchMode : instance.LaunchMode,
                syncMode: instance.SyncMode == MobaBattleLaunchSyncMode.Unspecified ? BaseSpec.SyncMode : instance.SyncMode,
                authorityMode: instance.AuthorityMode == MobaBattleLaunchAuthorityMode.Unspecified ? BaseSpec.AuthorityMode : instance.AuthorityMode,
                players: BaseSpec.Players,
                enterGameOpCode: BaseSpec.EnterGameOpCode,
                enterGamePayload: BaseSpec.EnterGamePayload);
        }

        public static MobaBattleSimulationLaunchPlan LocalMultiClient(
            in MobaBattleLaunchSpec baseSpec,
            int count,
            string clientIdPrefix = "local_client",
            string worldIdPrefix = "local_world",
            bool shareWorld = true,
            bool shareRandomSeed = true)
        {
            if (count <= 0) count = 1;

            var instances = new MobaBattleLaunchInstanceSpec[count];
            for (int i = 0; i < count; i++)
            {
                var player = ResolvePlayer(baseSpec.Players, i, baseSpec.LocalPlayerId);
                instances[i] = new MobaBattleLaunchInstanceSpec(
                    instanceId: i,
                    battleId: baseSpec.BattleId,
                    matchId: baseSpec.MatchId,
                    worldId: shareWorld ? baseSpec.WorldId : $"{worldIdPrefix}_{i + 1}",
                    worldType: baseSpec.WorldType,
                    clientId: $"{clientIdPrefix}_{i + 1}",
                    localPlayerId: player,
                    launchMode: MobaBattleLaunchMode.ConsoleSimulation,
                    syncMode: baseSpec.SyncMode,
                    authorityMode: baseSpec.AuthorityMode);
            }

            return new MobaBattleSimulationLaunchPlan(in baseSpec, instances, shareRandomSeed);
        }

        private static PlayerId ResolvePlayer(MobaPlayerLoadout[] players, int index, PlayerId fallback)
        {
            if (players == null || players.Length == 0) return fallback;
            if (index >= 0 && index < players.Length) return players[index].PlayerId;
            return players[players.Length - 1].PlayerId;
        }
    }

    public readonly struct MobaBattleLaunchInstanceSpec
    {
        public readonly int InstanceId;
        public readonly string BattleId;
        public readonly string MatchId;
        public readonly string WorldId;
        public readonly string WorldType;
        public readonly string ClientId;
        public readonly PlayerId LocalPlayerId;
        public readonly MobaBattleLaunchMode LaunchMode;
        public readonly MobaBattleLaunchSyncMode SyncMode;
        public readonly MobaBattleLaunchAuthorityMode AuthorityMode;

        public MobaBattleLaunchInstanceSpec(
            int instanceId,
            string battleId,
            string matchId,
            string worldId,
            string worldType,
            string clientId,
            PlayerId localPlayerId,
            MobaBattleLaunchMode launchMode = MobaBattleLaunchMode.Unspecified,
            MobaBattleLaunchSyncMode syncMode = MobaBattleLaunchSyncMode.Unspecified,
            MobaBattleLaunchAuthorityMode authorityMode = MobaBattleLaunchAuthorityMode.Unspecified)
        {
            InstanceId = instanceId;
            BattleId = battleId;
            MatchId = matchId;
            WorldId = worldId;
            WorldType = worldType;
            ClientId = clientId;
            LocalPlayerId = localPlayerId;
            LaunchMode = launchMode;
            SyncMode = syncMode;
            AuthorityMode = authorityMode;
        }

        public string OverrideBattleId(string fallback) => string.IsNullOrEmpty(BattleId) ? fallback : BattleId;
        public string OverrideMatchId(string fallback) => string.IsNullOrEmpty(MatchId) ? fallback : MatchId;
        public string OverrideWorldId(string fallback) => string.IsNullOrEmpty(WorldId) ? fallback : WorldId;
        public string OverrideWorldType(string fallback) => string.IsNullOrEmpty(WorldType) ? fallback : WorldType;
        public string OverrideClientId(string fallback) => string.IsNullOrEmpty(ClientId) ? fallback : ClientId;
    }
}
