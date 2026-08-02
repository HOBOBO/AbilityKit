using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.Moba.Struct;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Orleans.Grains.Rooms;

internal static class OrleansRoomBattleStartMapper
{
    public static BattleInitParams ToBattleInitParams(
        string roomId,
        in MobaRoomGameStartSpec spec,
        int ruleSetId,
        int configVersion,
        int protocolVersion,
        string? worldType,
        string? clientId,
        string? roomType)
    {
        var players = spec.Players;
        var initPlayers = players == null || players.Length == 0
            ? new List<PlayerInitInfo>()
            : new List<PlayerInitInfo>(players.Length);

        if (players != null)
        {
            for (int i = 0; i < players.Length; i++)
            {
                var slot = players[i];
                var loadout = slot.ToPlayerLoadout(i);
                initPlayers.Add(new PlayerInitInfo
                {
                    PlayerId = ToUIntPlayerId(loadout.PlayerId, i + 1),
                    ActorId = i + 1,
                    HeroId = loadout.HeroId,
                    PosX = loadout.SpawnX,
                    PosY = loadout.SpawnY,
                    PosZ = loadout.SpawnZ,
                    HasSpawnPosition = loadout.HasSpawnPosition,
                    SpawnIndex = loadout.SpawnIndex,
                    TeamId = loadout.TeamId,
                    Level = loadout.Level,
                    AttributeTemplateId = loadout.AttributeTemplateId,
                    BasicAttackSkillId = loadout.BasicAttackSkillId,
                    SkillIds = loadout.SkillIds == null ? null : new List<int>(loadout.SkillIds)
                });
            }
        }

        return new BattleInitParams
        {
            WorldId = RoomGatewayIds.CreateNumericRoomId(roomId),
            TickRate = spec.TickRate > 0 ? spec.TickRate : 30,
            Players = initPlayers,
            MapId = spec.MapId > 0 ? spec.MapId : 1,
            GameplayId = spec.GameplayId,
            RuleSetId = ruleSetId,
            ConfigVersion = configVersion,
            ProtocolVersion = protocolVersion,
            RandomSeed = spec.RandomSeed,
            InputDelayFrames = spec.InputDelayFrames,
            WorldType = string.IsNullOrWhiteSpace(worldType) ? GameplayRoomTypes.Default : worldType,
            ClientId = string.IsNullOrWhiteSpace(clientId) ? "orleans_room" : clientId,
            RoomType = string.IsNullOrWhiteSpace(roomType) ? GameplayRoomTypes.Default : roomType
        };
    }

    private static uint ToUIntPlayerId(PlayerId playerId, int fallback)
    {
        return uint.TryParse(playerId.Value, out var value) && value > 0
            ? value
            : (uint)Math.Max(1, fallback);
    }
}
