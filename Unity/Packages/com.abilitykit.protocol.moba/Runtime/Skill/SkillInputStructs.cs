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
            byte[] payload = null,
            int predictionKey = 0,
            int targetFrame = 0,
            long inputSequence = 0L,
            int entityVersion = 0,
            int interruptEpoch = 0)
        {
            Slot = slot;
            Phase = phase;
            PointerId = pointerId;
            TargetActorId = targetActorId;
            AimPos = aimPos;
            AimDir = aimDir;
            OpCode = opCode;
            Payload = payload;
            PredictionKey = predictionKey;
            TargetFrame = targetFrame;
            InputSequence = inputSequence;
            EntityVersion = entityVersion;
            InterruptEpoch = interruptEpoch;
        }
    }
}
