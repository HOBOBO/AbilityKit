using AbilityKit.Ability.Host;
using AbilityKit.Ability.FrameSync;

namespace AbilityKit.Ability.Host.Extensions.FrameSync
{
    public readonly struct LocalPlayerInputEvent
    {
        public readonly FrameIndex Frame;
        public readonly PlayerId PlayerId;
        public readonly int OpCode;
        public readonly byte[] Payload;
        public readonly bool CanRetargetIfStale;

        public LocalPlayerInputEvent(
            FrameIndex frame,
            PlayerId playerId,
            int opCode,
            byte[] payload,
            bool canRetargetIfStale = false)
        {
            Frame = frame;
            PlayerId = playerId;
            OpCode = opCode;
            Payload = payload;
            CanRetargetIfStale = canRetargetIfStale;
        }

        public LocalPlayerInputEvent(PlayerId playerId, int opCode, byte[] payload)
            : this(new FrameIndex(-1), playerId, opCode, payload)
        {
        }
    }
}
