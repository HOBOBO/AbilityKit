using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Game.Flow
{
    internal static class BattleInputCommandFactory
    {
        public static PlayerInputCommand CreateMove(int frame, PlayerId playerId, float dx, float dz)
        {
            return Create(frame, playerId, MobaOpCodes.Input.Move, MobaMoveCodec.Serialize(dx, dz));
        }

        public static PlayerInputCommand CreateSkillSlot(int frame, PlayerId playerId, int slot)
        {
            var evt = new SkillInputEvent(slot: slot, phase: SkillInputPhase.Press);
            return Create(frame, playerId, MobaOpCodes.Input.SkillInput, SkillInputCodec.Serialize(in evt));
        }

        public static PlayerInputCommand CreateDebugSpawnUnit(
            int frame,
            PlayerId playerId,
            MobaDebugSpawnUnitRelation relation)
        {
            return Create(
                frame,
                playerId,
                MobaOpCodes.Input.DebugSpawnUnit,
                MobaDebugSpawnUnitCodec.Serialize(relation));
        }

        public static PlayerInputCommand CreateDebugReplaceHero(
            int frame,
            PlayerId playerId,
            int heroId)
        {
            return Create(
                frame,
                playerId,
                MobaOpCodes.Input.DebugReplaceHero,
                MobaDebugReplaceHeroCodec.Serialize(heroId));
        }

        public static PlayerInputCommand CreateSkillAimRelease(
            int frame,
            PlayerId playerId,
            int slot,
            float aimPosX,
            float aimPosY,
            float aimPosZ,
            float aimDirX,
            float aimDirY,
            float aimDirZ)
        {
            var aimPos = new Vec3(aimPosX, aimPosY, aimPosZ);
            var aimDir = new Vec3(aimDirX, aimDirY, aimDirZ);
            var evt = new SkillInputEvent(slot: slot, phase: SkillInputPhase.Release, aimPos: in aimPos, aimDir: in aimDir);
            return Create(frame, playerId, MobaOpCodes.Input.SkillInput, SkillInputCodec.Serialize(in evt));
        }

        private static PlayerInputCommand Create(int frame, PlayerId playerId, int opCode, byte[] payload)
        {
            return new PlayerInputCommand(new FrameIndex(frame), playerId, opCode, payload);
        }

    }
}
