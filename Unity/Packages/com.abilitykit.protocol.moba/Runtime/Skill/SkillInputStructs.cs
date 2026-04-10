using AbilityKit.Core.Generic;
using AbilityKit.Core.Math;

namespace AbilityKit.Ability.Share.Impl.Moba.Struct
{
    public enum SkillInputPhase
    {
        Press = 1,
        Hold = 2,
        Release = 3,
        Cancel = 4,
    }

    public readonly struct SkillInputEvent
    {
        [BinaryMember(0)] public readonly int Slot;
        [BinaryMember(1)] public readonly SkillInputPhase Phase;
        [BinaryMember(2)] public readonly int PointerId;
        [BinaryMember(3)] public readonly int TargetActorId;
        [BinaryMember(4)] public readonly Vec3 AimPos;
        [BinaryMember(5)] public readonly Vec3 AimDir;
        [BinaryMember(6)] public readonly int OpCode;
        [BinaryMember(7)] public readonly byte[] Payload;

        public SkillInputEvent(
            int slot,
            SkillInputPhase phase,
            int pointerId = 0,
            int targetActorId = 0,
            in Vec3 aimPos = default,
            in Vec3 aimDir = default,
            int opCode = 0,
            byte[] payload = null)
        {
            Slot = slot;
            Phase = phase;
            PointerId = pointerId;
            TargetActorId = targetActorId;
            AimPos = aimPos;
            AimDir = aimDir;
            OpCode = opCode;
            Payload = payload;
        }
    }
}
