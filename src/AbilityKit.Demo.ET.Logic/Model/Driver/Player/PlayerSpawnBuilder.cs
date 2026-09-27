using System.Collections.Generic;
using AbilityKit.Ability.Config;
using AbilityKit.Ability.Host.Extensions.Moba.Room;
using AbilityKit.Demo.Moba.Config.BattleDemo;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using PlayerId = AbilityKit.Ability.Host.PlayerId;

namespace ET.Logic
{
    /// <summary>
    /// 玩家生成列表构建器
    /// 负责从正式配置或房间玩家快照构建 ETPlayerSpawnData 列表
    /// </summary>
    public static class PlayerSpawnBuilder
    {
        /// <summary>
        /// Builds battle spawn data from formal room player snapshots.
        /// </summary>
        public static List<ETPlayerSpawnData> BuildSpawnListFromRoomPlayers(MobaRoomPlayerSnapshot[] players, PlayerId localPlayerId, ETTeamSpawnLayout spawnLayout = null, int localTeamId = 1)
        {
            var spawnList = new List<ETPlayerSpawnData>();
            if (players == null || players.Length == 0)
            {
                return spawnList;
            }

            spawnLayout ??= ETTeamSpawnLayout.CreateDefaultLaneLayout();

            int localTeamCount = 0;
            int remoteTeamCount = 0;

            foreach (var player in players)
            {
                int teamSlotIndex;
                if (player.TeamId == localTeamId)
                {
                    teamSlotIndex = localTeamCount;
                    localTeamCount++;
                }
                else
                {
                    teamSlotIndex = remoteTeamCount;
                    remoteTeamCount++;
                }

                spawnLayout.ResolvePosition(player.TeamId, teamSlotIndex, localTeamId, out var x, out var z);

                var spawnData = new ETPlayerSpawnData(
                    playerId: player.PlayerId.Value,
                    characterId: player.HeroId,
                    attributeTemplateId: player.AttributeTemplateId,
                    level: player.Level,
                    basicAttackSkillId: player.BasicAttackSkillId,
                    skillIds: player.SkillIds,
                    characterName: $"Hero_{player.HeroId}",
                    teamId: player.TeamId,
                    x: x,
                    y: 0f,
                    z: z,
                    rotY: spawnLayout.RotationY,
                    scale: spawnLayout.Scale,
                    hp: 0f,
                    maxHp: 0f);

                spawnList.Add(spawnData);
                Log.Info($"[PlayerSpawnBuilder] Room player converted: PlayerId={player.PlayerId.Value}, HeroId={player.HeroId}, Team={player.TeamId}, LocalPlayer={localPlayerId.Value}");
            }

            return spawnList;
        }

        /// <summary>
        /// 从正式 MOBA 配置库构建生成列表
        /// </summary>
        public static List<ETPlayerSpawnData> BuildSpawnListFromConfig(ITextAssetLoader loader, int localPlayerId, ETTeamSpawnLayout spawnLayout = null, int localTeamId = 1)
        {
            if (loader == null)
            {
                return new List<ETPlayerSpawnData>();
            }

            var registry = MobaConfigRegistry.Instance;
            var configDatabase = new MobaConfigDatabase(registry, JsonNetMobaConfigDtoDeserializer.Instance, new LubanMobaConfigDtoBytesDeserializer(), loader);
            new LubanGroupsMobaConfigLoadProfile(loader).Load(configDatabase);

            return BuildSpawnListFromConfig(configDatabase, localPlayerId, spawnLayout, localTeamId);
        }

        public static List<ETPlayerSpawnData> BuildSpawnListFromConfig(MobaConfigDatabase configDatabase, int localPlayerId, ETTeamSpawnLayout spawnLayout = null, int localTeamId = 1)
        {
            var players = new List<ETPlayerSpawnData>();
            spawnLayout ??= ETTeamSpawnLayout.CreateDefaultLaneLayout();

            if (configDatabase == null)
            {
                return players;
            }

            int playerIdBase = localPlayerId > 0 ? localPlayerId : 1;

            if (configDatabase.TryGetCharacter(1001, out var heroConfig))
            {
                var spawnData = BuildSpawnData(configDatabase, heroConfig, playerIdBase.ToString(), 1, 0, localTeamId, spawnLayout, "player");
                players.Add(spawnData);
            }

            for (int i = 2; i <= 3; i++)
            {
                int heroId = 1000 + i;
                if (configDatabase.TryGetCharacter(heroId, out var aiConfig))
                {
                    var spawnData = BuildSpawnData(configDatabase, aiConfig, (playerIdBase + i).ToString(), 1, i - 1, localTeamId, spawnLayout, "AI");
                    players.Add(spawnData);
                }
            }

            for (int i = 1; i <= 3; i++)
            {
                int heroId = 1000 + i;
                if (configDatabase.TryGetCharacter(heroId, out var enemyConfig))
                {
                    var spawnData = BuildSpawnData(configDatabase, enemyConfig, (2000 + i).ToString(), 2, i - 1, localTeamId, spawnLayout, "enemy");
                    players.Add(spawnData);
                }
            }

            return players;
        }

        private static ETPlayerSpawnData BuildSpawnData(MobaConfigDatabase configDatabase, CharacterMO character, string playerId, int teamId, int teamSlotIndex, int localTeamId, ETTeamSpawnLayout spawnLayout, string role)
        {
            configDatabase.TryGetAttributeTemplate(character.AttributeTemplateId, out var attributes);
            float hp = attributes?.Hp ?? 500f;
            float maxHp = attributes?.MaxHp > 0 ? attributes.MaxHp : hp;
            var skillIds = RequireSkillIds(character);

            spawnLayout.ResolvePosition(teamId, teamSlotIndex, localTeamId, out var x, out var z);
            var spawnData = new ETPlayerSpawnData(playerId, character.Id, character.AttributeTemplateId, 1, skillIds[0], skillIds, character.Name, teamId, x, 0f, z, spawnLayout.RotationY, spawnLayout.Scale, hp, maxHp);
            Log.Info($"[PlayerSpawnBuilder] Loaded {role}: {character.Name} (Team {teamId}, PlayerId={playerId})");
            return spawnData;
        }

        private static int[] RequireSkillIds(CharacterMO config)
        {
            if (config == null)
            {
                throw new System.ArgumentNullException(nameof(config));
            }

            if (config.SkillIds == null || config.SkillIds.Count == 0)
            {
                throw new System.InvalidOperationException($"Character {config.Id} requires explicit skill ids for MOBA battle startup.");
            }

            var skillIds = new int[config.SkillIds.Count];
            for (var i = 0; i < skillIds.Length; i++)
            {
                skillIds[i] = config.SkillIds[i];
            }

            return skillIds;
        }
    }
}
