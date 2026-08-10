using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.Moba.StartSources;
using AbilityKit.Ability.Host.Extensions.Moba.Struct;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.CreateWorld;

namespace AbilityKit.Ability.Host.Extensions.Moba.CreateWorld
{
    public readonly struct MobaBattleStartPlan
    {
        private readonly MobaCreateWorldSpec _createWorldSpec;
        private readonly byte[] _enterGamePayload;

        public readonly PlayerId LocalPlayerId;
        public readonly int EnterGameOpCode;

        public MobaCreateWorldSpec CreateWorldSpec =>
            CloneCreateWorldSpec(in _createWorldSpec);
        public byte[] EnterGamePayload => CloneBytes(_enterGamePayload);

        public MobaBattleStartPlan(
            PlayerId localPlayerId,
            in MobaCreateWorldSpec createWorldSpec,
            int enterGameOpCode = 0,
            byte[] enterGamePayload = null)
        {
            LocalPlayerId = localPlayerId;
            _createWorldSpec = CloneCreateWorldSpec(in createWorldSpec);
            EnterGameOpCode = enterGameOpCode;
            _enterGamePayload = CloneBytes(enterGamePayload);
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
            var createWorldSpec = CloneCreateWorldSpec(in _createWorldSpec);
            return createWorldSpec.ToEnterReq(
                LocalPlayerId,
                EnterGameOpCode,
                CloneBytes(_enterGamePayload));
        }

        public MobaCreateWorldInitPayload ToCreateWorldInitPayload()
        {
            var req = ToEnterReq();
            var createWorldSpec = CloneCreateWorldSpec(in _createWorldSpec);
            return new MobaCreateWorldInitPayload(
                req.PlayerId,
                in createWorldSpec,
                req.OpCode,
                req.Payload);
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

        private static MobaCreateWorldSpec CloneCreateWorldSpec(
            in MobaCreateWorldSpec source)
        {
            return new MobaCreateWorldSpec(
                source.MatchId,
                source.MapId,
                source.RandomSeed,
                source.TickRate,
                source.InputDelayFrames,
                ClonePlayers(source.Players),
                source.GameplayId);
        }

        private static MobaPlayerLoadout[] ClonePlayers(
            MobaPlayerLoadout[] players)
        {
            if (players == null) return null;
            if (players.Length == 0) return Array.Empty<MobaPlayerLoadout>();

            var clone = new MobaPlayerLoadout[players.Length];
            for (var i = 0; i < players.Length; i++)
            {
                var player = players[i];
                clone[i] = new MobaPlayerLoadout(
                    player.PlayerId,
                    player.TeamId,
                    player.HeroId,
                    player.AttributeTemplateId,
                    player.Level,
                    player.BasicAttackSkillId,
                    CloneInts(player.SkillIds),
                    player.SpawnIndex,
                    player.UnitSubType,
                    player.MainType,
                    player.HasSpawnPosition,
                    player.SpawnX,
                    player.SpawnY,
                    player.SpawnZ,
                    player.BrainId,
                    player.EnableBrainOnSpawn);
            }

            return clone;
        }

        private static int[] CloneInts(int[] values)
        {
            if (values == null) return null;
            if (values.Length == 0) return Array.Empty<int>();
            return (int[])values.Clone();
        }

        private static byte[] CloneBytes(byte[] values)
        {
            if (values == null) return null;
            if (values.Length == 0) return Array.Empty<byte>();
            return (byte[])values.Clone();
        }
    }

    public static class MobaBattleStartPlanValidator
    {
        public static MobaBattleStartPlanValidationResult Validate(
            in MobaBattleStartPlan plan)
        {
            var enterReq = plan.ToEnterReq();
            var enterValidation =
                MobaProtocolValidation.ValidateEnterGameReq(in enterReq);
            if (!enterValidation.IsValid)
            {
                return MobaBattleStartPlanValidationResult.Fail(
                    "battle start plan enter-game projection invalid. " +
                    enterValidation);
            }

            if (plan.LocalPlayerId.Value != enterReq.PlayerId.Value)
            {
                return MobaBattleStartPlanValidationResult.Fail(
                    $"battle start plan local player mismatch. " +
                    $"plan={plan.LocalPlayerId.Value}, " +
                    $"enterReq={enterReq.PlayerId.Value}");
            }

            return MobaBattleStartPlanValidationResult.Success;
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
