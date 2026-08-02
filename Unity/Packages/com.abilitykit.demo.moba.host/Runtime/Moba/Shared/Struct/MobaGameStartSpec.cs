using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.Moba.CreateWorld;
using AbilityKit.Protocol.Moba;

namespace AbilityKit.Ability.Host.Extensions.Moba.Struct
{
    public readonly struct MobaRoomLoadoutOverrides
    {
        public readonly int Level;
        public readonly int AttributeTemplateId;
        public readonly int BasicAttackSkillId;
        public readonly int[] SkillIds;
        public readonly int BrainId;
        public readonly bool EnableBrainOnSpawn;

        public MobaRoomLoadoutOverrides(
            int level,
            int attributeTemplateId,
            int basicAttackSkillId,
            int[] skillIds,
            int brainId = 0,
            bool enableBrainOnSpawn = true)
        {
            Level = level;
            AttributeTemplateId = attributeTemplateId;
            BasicAttackSkillId = basicAttackSkillId;
            SkillIds = skillIds;
            BrainId = brainId;
            EnableBrainOnSpawn = enableBrainOnSpawn;
        }

        public bool HasAnyOverride => Level > 0 || AttributeTemplateId > 0 || BasicAttackSkillId > 0
            || (SkillIds != null && SkillIds.Length > 0) || BrainId > 0;
    }

    public readonly struct MobaHostSpawnData
    {
        public readonly int PlayerId;
        public readonly int HeroId;
        public readonly int TeamId;
        public readonly float X;
        public readonly float Y;
        public readonly float Z;
        public readonly string Name;

        public MobaHostSpawnData(int playerId, int heroId, int teamId, float x, float y, float z, string name = null)
        {
            PlayerId = playerId;
            HeroId = heroId;
            TeamId = teamId;
            X = x;
            Y = y;
            Z = z;
            Name = name;
        }

        public static MobaHostSpawnData CreateLocalPlayer(int playerId, int heroId, float x, float z)
        {
            return new MobaHostSpawnData(playerId, heroId, 1, x, 0f, z, "LocalPlayer");
        }
    }

    public readonly struct MobaRoomPlayerSlot
    {
        public readonly PlayerId PlayerId;
        public readonly int TeamId;
        public readonly int HeroId;
        public readonly int SpawnPointId;
        public readonly MobaRoomLoadoutOverrides Overrides;

        public MobaRoomPlayerSlot(PlayerId playerId, int teamId, int heroId, int spawnPointId, in MobaRoomLoadoutOverrides overrides)
        {
            PlayerId = playerId;
            TeamId = teamId;
            HeroId = heroId;
            SpawnPointId = spawnPointId;
            Overrides = overrides;
        }

        public MobaPlayerLoadout ToPlayerLoadout(int spawnIndexFallback)
        {
            var ov = Overrides;
            var loadout = new MobaPlayerLoadout(
                playerId: PlayerId,
                teamId: TeamId,
                heroId: HeroId,
                attributeTemplateId: ov.AttributeTemplateId,
                level: ov.Level,
                basicAttackSkillId: ov.BasicAttackSkillId,
                skillIds: ov.SkillIds,
                spawnIndex: SpawnPointId >= 0 ? SpawnPointId : spawnIndexFallback,
                brainId: ov.BrainId,
                enableBrainOnSpawn: ov.EnableBrainOnSpawn);

            var validation = MobaProtocolValidation.ValidatePlayerLoadout(in loadout);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException("invalid MOBA room player loadout. " + validation);
            }

            return loadout;
        }
    }

    public readonly struct MobaRoomGameStartSpec
    {
        public readonly string MatchId;
        public readonly int MapId;
        public readonly int GameplayId;

        public readonly int RandomSeed;
        public readonly int TickRate;
        public readonly int InputDelayFrames;

        public readonly MobaRoomPlayerSlot[] Players;

        public MobaRoomGameStartSpec(string matchId, int mapId, int randomSeed, int tickRate, int inputDelayFrames, MobaRoomPlayerSlot[] players, int gameplayId = 0)
        {
            MatchId = matchId;
            MapId = mapId;
            GameplayId = gameplayId;
            RandomSeed = randomSeed;
            TickRate = tickRate;
            InputDelayFrames = inputDelayFrames;
            Players = players;
        }

        public EnterMobaGameReq ToEnterReq(PlayerId localPlayerId)
        {
            var ps = Players;
            if (ps == null || ps.Length == 0)
            {
                throw new InvalidOperationException("MOBA room game start spec requires explicit player loadouts.");
            }

            var loadouts = new MobaPlayerLoadout[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                loadouts[i] = ps[i].ToPlayerLoadout(spawnIndexFallback: i);
            }

            return new EnterMobaGameReq(
                playerId: localPlayerId,
                matchId: MatchId,
                mapId: MapId,
                randomSeed: RandomSeed,
                tickRate: TickRate,
                inputDelayFrames: InputDelayFrames,
                opCode: 0,
                payload: null,
                players: loadouts,
                gameplayId: GameplayId);
        }

    }

    public static class MobaHostSpawnPlanBuilder
    {

        public static MobaPlayerLoadout[] ToLoadouts(MobaHostSpawnData[] spawns, int startIndex = 0)
        {
            throw new InvalidOperationException("MobaHostSpawnData cannot be converted to MOBA player loadouts without explicit loadout fields.");
        }

        public static MobaPlayerLoadout ToLoadout(MobaHostSpawnData spawn, int spawnIndex)
        {
            throw new InvalidOperationException("MobaHostSpawnData cannot be converted to a MOBA player loadout without explicit loadout fields.");
        }

        public static EnterMobaGameReq ToEnterReq(
            MobaHostSpawnData[] spawns,
            PlayerId localPlayerId,
            string matchId,
            int mapId,
            int tickRate = 30,
            int inputDelayFrames = 0,
            int randomSeed = 0,
            int gameplayId = 0,
            int enterGameOpCode = 0,
            byte[] enterGamePayload = null)
        {
            throw new InvalidOperationException("Host spawn start plans are obsolete for MOBA. Build a protocol loadout spec from explicit player loadouts instead.");
        }

        public static MobaBattleStartPlan ToStartPlan(
            MobaHostSpawnData[] spawns,
            PlayerId localPlayerId,
            string matchId,
            int mapId,
            int tickRate = 30,
            int inputDelayFrames = 0,
            int randomSeed = 0,
            int gameplayId = 0,
            int enterGameOpCode = 0,
            byte[] enterGamePayload = null)
        {
            throw new InvalidOperationException("Host spawn start plans are obsolete for MOBA. Build a protocol loadout spec from explicit player loadouts instead.");
        }

    }
}

