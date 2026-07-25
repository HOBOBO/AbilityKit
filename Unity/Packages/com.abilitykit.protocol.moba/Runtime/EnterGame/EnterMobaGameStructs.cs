using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using MemoryPack;

namespace AbilityKit.Protocol.Moba
{
    [MemoryPackable]
    public readonly partial struct MobaPlayerLoadout
    {
        [MemoryPackOrder(0)] public readonly PlayerId PlayerId;
        [MemoryPackOrder(1)] public readonly int TeamId;
        [MemoryPackOrder(2)] public readonly int HeroId;
        [MemoryPackOrder(3)] public readonly int AttributeTemplateId;
        [MemoryPackOrder(4)] public readonly int Level;
        [MemoryPackOrder(5)] public readonly int BasicAttackSkillId;
        [MemoryPackOrder(6)] public readonly int[] SkillIds;
        [MemoryPackOrder(7)] public readonly int SpawnIndex;
        [MemoryPackOrder(8)] public readonly int UnitSubType;
        [MemoryPackOrder(9)] public readonly int MainType;
        [MemoryPackOrder(10)] public readonly int HasSpawnPosition;
        [MemoryPackOrder(11)] public readonly float SpawnX;
        [MemoryPackOrder(12)] public readonly float SpawnY;
        [MemoryPackOrder(13)] public readonly float SpawnZ;

        [MemoryPackConstructor]
        public MobaPlayerLoadout(
            PlayerId playerId,
            int teamId,
            int heroId,
            int attributeTemplateId,
            int level,
            int basicAttackSkillId,
            int[] skillIds,
            int spawnIndex,
            int unitSubType = 1,
            int mainType = 1,
            int hasSpawnPosition = 0,
            float spawnX = 0f,
            float spawnY = 0f,
            float spawnZ = 0f)
        {
            PlayerId = playerId;
            TeamId = teamId;
            HeroId = heroId;
            AttributeTemplateId = attributeTemplateId;
            Level = level;
            BasicAttackSkillId = basicAttackSkillId;
            SkillIds = skillIds;
            SpawnIndex = spawnIndex;
            UnitSubType = unitSubType;
            MainType = mainType;

            HasSpawnPosition = hasSpawnPosition;
            SpawnX = spawnX;
            SpawnY = spawnY;
            SpawnZ = spawnZ;
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaPlayerEntry
    {
        [MemoryPackOrder(0)] public readonly PlayerId PlayerId;
        [MemoryPackOrder(1)] public readonly int TeamId;
        [MemoryPackOrder(2)] public readonly int HeroId;
        [MemoryPackOrder(3)] public readonly int SpawnIndex;

        [MemoryPackConstructor]
        public MobaPlayerEntry(PlayerId playerId, int teamId, int heroId, int spawnIndex)
        {
            PlayerId = playerId;
            TeamId = teamId;
            HeroId = heroId;
            SpawnIndex = spawnIndex;
        }
    }

    [MemoryPackable]
    public readonly partial struct EnterMobaGameReq
    {
        [MemoryPackOrder(0)] public readonly PlayerId PlayerId;
        [MemoryPackOrder(1)] public readonly string MatchId;
        [MemoryPackOrder(2)] public readonly int MapId;

        [MemoryPackOrder(3)] public readonly int RandomSeed;
        [MemoryPackOrder(4)] public readonly int TickRate;
        [MemoryPackOrder(5)] public readonly int InputDelayFrames;

        [MemoryPackOrder(6)] public readonly int OpCode;
        [MemoryPackOrder(7)] public readonly byte[] Payload;

        [MemoryPackOrder(8)] public readonly MobaPlayerLoadout[] Players;
        [MemoryPackOrder(9)] public readonly int GameplayId;

        [MemoryPackConstructor]
        public EnterMobaGameReq(
            PlayerId playerId,
            string matchId,
            int mapId,
            int randomSeed,
            int tickRate,
            int inputDelayFrames,
            int opCode = 0,
            byte[] payload = null,
            MobaPlayerLoadout[] players = null,
            int gameplayId = 0)
        {
            PlayerId = playerId;
            MatchId = matchId;
            MapId = mapId;
            GameplayId = gameplayId;

            RandomSeed = randomSeed;
            TickRate = tickRate;
            InputDelayFrames = inputDelayFrames;
            OpCode = opCode;
            Payload = payload;

            Players = players;
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaGameStartSpec
    {
        [MemoryPackOrder(0)] public readonly EnterMobaGameReq EnterReq;

        [MemoryPackConstructor]
        public MobaGameStartSpec(in EnterMobaGameReq enterReq)
        {
            EnterReq = enterReq;
        }
    }

    [MemoryPackable]
    public readonly partial struct EnterMobaGameRes
    {
        [MemoryPackOrder(0)] public readonly WorldId WorldId;
        [MemoryPackOrder(1)] public readonly PlayerId PlayerId;
        [MemoryPackOrder(2)] public readonly int LocalActorId;

        [MemoryPackOrder(3)] public readonly int RandomSeed;
        [MemoryPackOrder(4)] public readonly int TickRate;
        [MemoryPackOrder(5)] public readonly int InputDelayFrames;

        [MemoryPackOrder(6)] public readonly MobaPlayerEntry[] Players;
        [MemoryPackOrder(7)] public readonly int OpCode;
        [MemoryPackOrder(8)] public readonly byte[] Payload;
        [MemoryPackOrder(9)] public readonly MobaPlayerLoadout[] PlayersLoadout;

        [MemoryPackConstructor]
        public EnterMobaGameRes(
            WorldId worldId,
            PlayerId playerId,
            int localActorId,
            int randomSeed,
            int tickRate,
            int inputDelayFrames,
            MobaPlayerEntry[] players = null,
            int opCode = 0,
            byte[] payload = null,
            MobaPlayerLoadout[] playersLoadout = null)
        {
            WorldId = worldId;
            PlayerId = playerId;
            LocalActorId = localActorId;
            RandomSeed = randomSeed;
            TickRate = tickRate;
            InputDelayFrames = inputDelayFrames;
            Players = players;
            OpCode = opCode;
            Payload = payload;
            PlayersLoadout = playersLoadout;
        }
    }
    
        public enum MobaProtocolValidationCode
        {
            Ok = 0,
            MissingLocalPlayerId = 1,
            MissingMatchId = 2,
            InvalidMapId = 3,
            InvalidRandomSeed = 4,
            InvalidTickRate = 5,
            InvalidInputDelayFrames = 6,
            MissingPlayers = 7,
            MissingPlayerId = 8,
            InvalidTeamId = 9,
            InvalidHeroId = 10,
            InvalidLevel = 11,
            InvalidAttributeTemplateId = 12,
            InvalidBasicAttackSkillId = 13,
            MissingSkillIds = 14,
            InvalidSkillId = 15,
            InvalidSpawnIndex = 16,
            InvalidSpawnPositionFlag = 17,
            MissingLocalPlayerLoadout = 18,
        }
    
        public readonly struct MobaProtocolValidationResult
        {
            public static readonly MobaProtocolValidationResult Ok = new MobaProtocolValidationResult(MobaProtocolValidationCode.Ok, null, -1);
    
            public readonly MobaProtocolValidationCode Code;
            public readonly string Message;
            public readonly int PlayerIndex;
    
            public bool IsValid => Code == MobaProtocolValidationCode.Ok;
    
            public MobaProtocolValidationResult(MobaProtocolValidationCode code, string message, int playerIndex = -1)
            {
                Code = code;
                Message = message;
                PlayerIndex = playerIndex;
            }
    
            public override string ToString()
            {
                if (IsValid)
                {
                    return nameof(MobaProtocolValidationCode.Ok);
                }
    
                return PlayerIndex >= 0
                    ? $"{Code}: {Message} (playerIndex={PlayerIndex})"
                    : $"{Code}: {Message}";
            }
        }
    
        public static class MobaProtocolValidation
        {
            public const int MinTickRate = 1;
            public const int MaxTickRate = 120;
            public const int MaxInputDelayFrames = 30;
    
            public static MobaProtocolValidationResult ValidateGameStartSpec(in MobaGameStartSpec spec)
            {
                return ValidateEnterGameReq(in spec.EnterReq);
            }
    
            public static MobaProtocolValidationResult ValidateEnterGameReqEnvelope(in EnterMobaGameReq req)
            {
                var header = ValidateEnterGameHeader(in req);
                if (!header.IsValid)
                {
                    return header;
                }
    
                if (req.Players == null || req.Players.Length == 0)
                {
                    return Fail(MobaProtocolValidationCode.MissingPlayers, "players are required");
                }
    
                bool containsLocalPlayer = false;
                for (int i = 0; i < req.Players.Length; i++)
                {
                    var loadout = req.Players[i];
                    if (IsEmpty(loadout.PlayerId))
                    {
                        return Fail(MobaProtocolValidationCode.MissingPlayerId, "player id is required", i);
                    }
    
                    if (loadout.TeamId <= 0)
                    {
                        return Fail(MobaProtocolValidationCode.InvalidTeamId, $"team id must be positive, actual={loadout.TeamId}", i);
                    }
    
                    if (loadout.HeroId <= 0)
                    {
                        return Fail(MobaProtocolValidationCode.InvalidHeroId, $"hero id must be positive, actual={loadout.HeroId}", i);
                    }
    
                    if (loadout.SpawnIndex < 0)
                    {
                        return Fail(MobaProtocolValidationCode.InvalidSpawnIndex, $"spawn index cannot be negative, actual={loadout.SpawnIndex}", i);
                    }
    
                    if (loadout.HasSpawnPosition != 0 && loadout.HasSpawnPosition != 1)
                    {
                        return Fail(MobaProtocolValidationCode.InvalidSpawnPositionFlag, $"has spawn position must be 0 or 1, actual={loadout.HasSpawnPosition}", i);
                    }
    
                    if (loadout.PlayerId.Value == req.PlayerId.Value)
                    {
                        containsLocalPlayer = true;
                    }
                }
    
                return containsLocalPlayer
                    ? MobaProtocolValidationResult.Ok
                    : Fail(MobaProtocolValidationCode.MissingLocalPlayerLoadout, $"local player id was not found in players, playerId={req.PlayerId.Value}");
            }
    
            public static MobaProtocolValidationResult ValidateEnterGameReq(in EnterMobaGameReq req)
            {
                var envelope = ValidateEnterGameReqEnvelope(in req);
                if (!envelope.IsValid)
                {
                    return envelope;
                }
    
                bool containsLocalPlayer = false;
                for (int i = 0; i < req.Players.Length; i++)
                {
                    var result = ValidatePlayerLoadout(in req.Players[i], i);
                    if (!result.IsValid)
                    {
                        return result;
                    }
    
                    if (req.Players[i].PlayerId.Value == req.PlayerId.Value)
                    {
                        containsLocalPlayer = true;
                    }
                }
    
                return containsLocalPlayer
                    ? MobaProtocolValidationResult.Ok
                    : Fail(MobaProtocolValidationCode.MissingLocalPlayerLoadout, $"local player id was not found in players, playerId={req.PlayerId.Value}");
            }
    
            public static MobaProtocolValidationResult ValidateCreateWorldSpecEnvelope(PlayerId localPlayerId, in MobaCreateWorldSpec spec)
            {
                return ValidateEnterGameReqEnvelope(spec.ToEnterReq(localPlayerId, opCode: 0, payload: null));
            }
    
            public static MobaProtocolValidationResult ValidateCreateWorldSpec(PlayerId localPlayerId, in MobaCreateWorldSpec spec)
            {
                return ValidateEnterGameReq(spec.ToEnterReq(localPlayerId, opCode: 0, payload: null));
            }
    
            public static MobaProtocolValidationResult ValidatePlayerLoadout(in MobaPlayerLoadout loadout, int playerIndex = -1)
            {
                if (IsEmpty(loadout.PlayerId))
                {
                    return Fail(MobaProtocolValidationCode.MissingPlayerId, "player id is required", playerIndex);
                }
    
                if (loadout.TeamId <= 0)
                {
                    return Fail(MobaProtocolValidationCode.InvalidTeamId, $"team id must be positive, actual={loadout.TeamId}", playerIndex);
                }
    
                if (loadout.HeroId <= 0)
                {
                    return Fail(MobaProtocolValidationCode.InvalidHeroId, $"hero id must be positive, actual={loadout.HeroId}", playerIndex);
                }
    
                if (loadout.Level <= 0)
                {
                    return Fail(MobaProtocolValidationCode.InvalidLevel, $"level must be positive, actual={loadout.Level}", playerIndex);
                }
    
                if (loadout.AttributeTemplateId <= 0)
                {
                    return Fail(MobaProtocolValidationCode.InvalidAttributeTemplateId, $"attribute template id must be positive, actual={loadout.AttributeTemplateId}", playerIndex);
                }
    
                if (loadout.BasicAttackSkillId <= 0)
                {
                    return Fail(MobaProtocolValidationCode.InvalidBasicAttackSkillId, $"basic attack skill id must be positive, actual={loadout.BasicAttackSkillId}", playerIndex);
                }
    
                if (loadout.SkillIds == null || loadout.SkillIds.Length == 0)
                {
                    return Fail(MobaProtocolValidationCode.MissingSkillIds, "skill ids are required", playerIndex);
                }
    
                for (int i = 0; i < loadout.SkillIds.Length; i++)
                {
                    if (loadout.SkillIds[i] <= 0)
                    {
                        return Fail(MobaProtocolValidationCode.InvalidSkillId, $"skill id must be positive, index={i}, actual={loadout.SkillIds[i]}", playerIndex);
                    }
                }
    
                if (loadout.SpawnIndex < 0)
                {
                    return Fail(MobaProtocolValidationCode.InvalidSpawnIndex, $"spawn index cannot be negative, actual={loadout.SpawnIndex}", playerIndex);
                }
    
                if (loadout.HasSpawnPosition != 0 && loadout.HasSpawnPosition != 1)
                {
                    return Fail(MobaProtocolValidationCode.InvalidSpawnPositionFlag, $"has spawn position must be 0 or 1, actual={loadout.HasSpawnPosition}", playerIndex);
                }
    
                return MobaProtocolValidationResult.Ok;
            }
    
            private static MobaProtocolValidationResult ValidateEnterGameHeader(in EnterMobaGameReq req)
            {
                if (IsEmpty(req.PlayerId))
                {
                    return Fail(MobaProtocolValidationCode.MissingLocalPlayerId, "local player id is required");
                }
    
                if (string.IsNullOrWhiteSpace(req.MatchId))
                {
                    return Fail(MobaProtocolValidationCode.MissingMatchId, "match id is required");
                }
    
                if (req.MapId <= 0)
                {
                    return Fail(MobaProtocolValidationCode.InvalidMapId, $"map id must be positive, actual={req.MapId}");
                }
    
                if (req.RandomSeed == 0)
                {
                    return Fail(MobaProtocolValidationCode.InvalidRandomSeed, "random seed must be non-zero");
                }
    
                if (req.TickRate < MinTickRate || req.TickRate > MaxTickRate)
                {
                    return Fail(MobaProtocolValidationCode.InvalidTickRate, $"tick rate must be in [{MinTickRate}, {MaxTickRate}], actual={req.TickRate}");
                }
    
                if (req.InputDelayFrames < 0 || req.InputDelayFrames > MaxInputDelayFrames)
                {
                    return Fail(MobaProtocolValidationCode.InvalidInputDelayFrames, $"input delay frames must be in [0, {MaxInputDelayFrames}], actual={req.InputDelayFrames}");
                }
    
                return MobaProtocolValidationResult.Ok;
            }
    
            private static bool IsEmpty(PlayerId playerId)
            {
                return string.IsNullOrWhiteSpace(playerId.Value);
            }
    
            private static MobaProtocolValidationResult Fail(MobaProtocolValidationCode code, string message, int playerIndex = -1)
            {
                return new MobaProtocolValidationResult(code, message, playerIndex);
            }
        }
}
