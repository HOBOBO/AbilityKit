using AbilityKit.Core.Mathematics;

namespace AbilityKit.Protocol.Moba
{
    public enum SkillInputPhase
    {
        Press = 1,
        Hold = 2,
        Release = 3,
        Cancel = 4,
    }

    public partial struct SkillInputEvent
    {
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
