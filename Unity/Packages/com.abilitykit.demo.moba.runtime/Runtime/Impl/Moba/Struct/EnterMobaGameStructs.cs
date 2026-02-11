using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.Share;
using System;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;

namespace AbilityKit.Ability.Share.Impl.Moba.Struct
{
    public readonly struct MobaPlayerLoadout
    {
        [BinaryMember(0)] public readonly PlayerId PlayerId;
        [BinaryMember(1)] public readonly int TeamId;
        [BinaryMember(2)] public readonly int HeroId;
        [BinaryMember(3)] public readonly int Level;
        [BinaryMember(4)] public readonly int BasicAttackSkillId;
        [BinaryMember(5)] public readonly int[] SkillIds;
        [BinaryMember(6)] public readonly int SpawnIndex;
        [BinaryMember(7)] public readonly int UnitSubType;
        [BinaryMember(8)] public readonly int MainType;
        [BinaryMember(9)] public readonly int HasSpawnPosition;
        [BinaryMember(10)] public readonly float SpawnX;
        [BinaryMember(11)] public readonly float SpawnY;
        [BinaryMember(12)] public readonly float SpawnZ;
        [BinaryMember(13)] public readonly int AttributeTemplateId;

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

    public readonly struct MobaPlayerEntry
    {
        [BinaryMember(0)] public readonly PlayerId PlayerId;
        [BinaryMember(1)] public readonly int TeamId;
        [BinaryMember(2)] public readonly int HeroId;
        [BinaryMember(3)] public readonly int SpawnIndex;

        public MobaPlayerEntry(PlayerId playerId, int teamId, int heroId, int spawnIndex)
        {
            PlayerId = playerId;
            TeamId = teamId;
            HeroId = heroId;
            SpawnIndex = spawnIndex;
        }
    }

    public readonly struct EnterMobaGameReq
    {
        [BinaryMember(0)] public readonly PlayerId PlayerId;
        [BinaryMember(1)] public readonly string MatchId;
        [BinaryMember(2)] public readonly int MapId;

        [BinaryMember(3)] public readonly int RandomSeed;
        [BinaryMember(4)] public readonly int TickRate;
        [BinaryMember(5)] public readonly int InputDelayFrames;

        [BinaryMember(6)] public readonly int OpCode;
        [BinaryMember(7)] public readonly byte[] Payload;

        [BinaryMember(8)] public readonly MobaPlayerLoadout[] Players;

        public EnterMobaGameReq(
            PlayerId playerId,
            string matchId,
            int mapId,
            int randomSeed,
            int tickRate,
            int inputDelayFrames,
            int opCode = 0,
            byte[] payload = null,
            MobaPlayerLoadout[] players = null)
        {
            PlayerId = playerId;
            MatchId = matchId;
            MapId = mapId;

            RandomSeed = randomSeed;
            TickRate = tickRate;
            InputDelayFrames = inputDelayFrames;
            OpCode = opCode;
            Payload = payload;

            Players = players;
        }
    }

    public readonly struct MobaGameStartSpec
    {
        [BinaryMember(0)] public readonly EnterMobaGameReq EnterReq;

        public MobaGameStartSpec(in EnterMobaGameReq enterReq)
        {
            EnterReq = enterReq;
        }
    }

    public static class MobaGameStartSpecNormalizer
    {
        public static EnterMobaGameReq Normalize(MobaConfigDatabase config, in EnterMobaGameReq req)
        {
            if (config == null) return req;
            if (req.Players == null || req.Players.Length == 0) return req;

            try
            {
                var src = req.Players;
                var dst = new MobaPlayerLoadout[src.Length];

                for (int i = 0; i < src.Length; i++)
                {
                    var p = src[i];

                    var attributeTemplateId = p.AttributeTemplateId;
                    int[] skillIds = p.SkillIds;

                    if ((attributeTemplateId <= 0 || skillIds == null) && config.TryGetCharacter(p.HeroId, out var character) && character != null)
                    {
                        if (attributeTemplateId <= 0)
                        {
                            attributeTemplateId = character.AttributeTemplateId;
                        }

                        if (skillIds == null)
                        {
                            var list = character.SkillIds;
                            if (list is int[] arr) skillIds = arr;
                            else if (list == null || list.Count == 0) skillIds = Array.Empty<int>();
                            else
                            {
                                var tmp = new int[list.Count];
                                for (int j = 0; j < list.Count; j++) tmp[j] = list[j];
                                skillIds = tmp;
                            }
                        }
                    }

                    dst[i] = new MobaPlayerLoadout(
                        playerId: p.PlayerId,
                        teamId: p.TeamId,
                        heroId: p.HeroId,
                        attributeTemplateId: attributeTemplateId,
                        level: p.Level,
                        basicAttackSkillId: p.BasicAttackSkillId,
                        skillIds: skillIds,
                        spawnIndex: p.SpawnIndex,
                        unitSubType: p.UnitSubType,
                        mainType: p.MainType,
                        hasSpawnPosition: p.HasSpawnPosition,
                        spawnX: p.SpawnX,
                        spawnY: p.SpawnY,
                        spawnZ: p.SpawnZ);
                }

                return new EnterMobaGameReq(
                    playerId: req.PlayerId,
                    matchId: req.MatchId,
                    mapId: req.MapId,
                    randomSeed: req.RandomSeed,
                    tickRate: req.TickRate,
                    inputDelayFrames: req.InputDelayFrames,
                    opCode: req.OpCode,
                    payload: req.Payload,
                    players: dst);
            }
            catch
            {
                return req;
            }
        }
    }

    public readonly struct EnterMobaGameRes
    {
        [BinaryMember(0)] public readonly WorldId WorldId;
        [BinaryMember(1)] public readonly PlayerId PlayerId;
        [BinaryMember(2)] public readonly int LocalActorId;

        [BinaryMember(3)] public readonly int RandomSeed;
        [BinaryMember(4)] public readonly int TickRate;
        [BinaryMember(5)] public readonly int InputDelayFrames;

        [BinaryMember(6)] public readonly MobaPlayerEntry[] Players;

        [BinaryMember(9)] public readonly MobaPlayerLoadout[] PlayersLoadout;

        [BinaryMember(7)] public readonly int OpCode;
        [BinaryMember(8)] public readonly byte[] Payload;

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
}
