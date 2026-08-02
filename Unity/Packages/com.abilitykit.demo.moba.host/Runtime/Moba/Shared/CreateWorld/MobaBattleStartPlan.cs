using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.Moba.Struct;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.CreateWorld;

namespace AbilityKit.Ability.Host.Extensions.Moba.CreateWorld
{
    public readonly struct MobaBattleStartPlan
    {
        public readonly PlayerId LocalPlayerId;
        public readonly MobaCreateWorldSpec CreateWorldSpec;
        public readonly int EnterGameOpCode;
        public readonly byte[] EnterGamePayload;

        public MobaBattleStartPlan(
            PlayerId localPlayerId,
            in MobaCreateWorldSpec createWorldSpec,
            int enterGameOpCode = 0,
            byte[] enterGamePayload = null)
        {
            LocalPlayerId = localPlayerId;
            CreateWorldSpec = createWorldSpec;
            EnterGameOpCode = enterGameOpCode;
            EnterGamePayload = enterGamePayload;
        }

        public static MobaBattleStartPlan FromRoomSpec(
            PlayerId localPlayerId,
            in MobaRoomGameStartSpec roomSpec,
            int enterGameOpCode = 0,
            byte[] enterGamePayload = null)
        {
            var createWorldSpec = MobaHostCreateWorldSpec.FromRoomSpec(in roomSpec).ToProtocolSpec();
            return new MobaBattleStartPlan(localPlayerId, in createWorldSpec, enterGameOpCode, enterGamePayload);
        }

        public static MobaBattleStartPlan FromEnterReq(in EnterMobaGameReq req)
        {
            var createWorldSpec = MobaCreateWorldSpec.FromEnterReq(in req);
            return new MobaBattleStartPlan(req.PlayerId, in createWorldSpec, req.OpCode, req.Payload);
        }

        public EnterMobaGameReq ToEnterReq()
        {
            return CreateWorldSpec.ToEnterReq(LocalPlayerId, EnterGameOpCode, EnterGamePayload);
        }

        public MobaCreateWorldInitPayload ToCreateWorldInitPayload()
        {
            var req = ToEnterReq();
            return new MobaCreateWorldInitPayload(req.PlayerId, in CreateWorldSpec, req.OpCode, req.Payload);
        }

        public WorldInitData ToWorldInitData(int initOpCode)
        {
            var initPayload = ToCreateWorldInitPayload();
            return new WorldInitData(initOpCode, MobaCreateWorldInitCodec.Serialize(in initPayload));
        }

        public MobaGameStartSpec ToGameStartSpec()
        {
            var req = ToEnterReq();
            return new MobaGameStartSpec(in req);
        }
    }

    public static class MobaBattleStartPlanBuilder
    {
        public static MobaBattleStartPlan FromRoomSpec(
            PlayerId localPlayerId,
            in MobaRoomGameStartSpec roomSpec,
            int enterGameOpCode = 0,
            byte[] enterGamePayload = null)
        {
            return MobaBattleStartPlan.FromRoomSpec(localPlayerId, in roomSpec, enterGameOpCode, enterGamePayload);
        }

        public static MobaBattleStartPlan FromCreateWorldSpec(
            PlayerId localPlayerId,
            in MobaCreateWorldSpec createWorldSpec,
            int enterGameOpCode = 0,
            byte[] enterGamePayload = null)
        {
            return new MobaBattleStartPlan(localPlayerId, in createWorldSpec, enterGameOpCode, enterGamePayload);
        }

        public static MobaBattleStartPlan FromEnterReq(in EnterMobaGameReq req)
        {
            return MobaBattleStartPlan.FromEnterReq(in req);
        }

        public static MobaBattleStartPlan FromHostSpawns(
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
            throw new System.InvalidOperationException("MobaHostSpawnData-based MOBA battle start is obsolete. Build explicit player loadouts instead.");
        }

        public static WorldInitData CreateWorldInitDataFromHostSpawns(
            MobaHostSpawnData[] spawns,
            PlayerId localPlayerId,
            string matchId,
            int mapId,
            int initOpCode,
            int tickRate = 30,
            int inputDelayFrames = 0,
            int randomSeed = 0,
            int gameplayId = 0,
            int enterGameOpCode = 0,
            byte[] enterGamePayload = null)
        {
            throw new System.InvalidOperationException("MobaHostSpawnData-based world init is obsolete. Build explicit player loadouts instead.");
        }
    }
}
