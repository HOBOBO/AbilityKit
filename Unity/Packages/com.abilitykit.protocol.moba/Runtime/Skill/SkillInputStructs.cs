using AbilityKit.Core.Mathematics;
using MemoryPack;

namespace AbilityKit.Protocol.Moba
{
    public enum SkillInputPhase
    {
        Press = 1,
        Hold = 2,
        Release = 3,
        Cancel = 4,
    }

    [MemoryPackable]
    public readonly partial struct SkillInputEvent
    {
        [MemoryPackOrder(0)] public readonly int Slot;
        [MemoryPackOrder(1)] public readonly SkillInputPhase Phase;
        [MemoryPackOrder(2)] public readonly int PointerId;
        [MemoryPackOrder(3)] public readonly int TargetActorId;
        [MemoryPackOrder(4)] public readonly Vec3 AimPos;
        [MemoryPackOrder(5)] public readonly Vec3 AimDir;
        [MemoryPackOrder(6)] public readonly int OpCode;
        [MemoryPackOrder(7)] public readonly byte[] Payload;

        [MemoryPackConstructor]
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
