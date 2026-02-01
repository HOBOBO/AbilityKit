using AbilityKit.Ability.Host;

namespace AbilityKit.Ability.Host.Extensions.FrameSync
{
    public readonly struct LocalPlayerInputEvent
    {
        public readonly PlayerId PlayerId;
        public readonly int OpCode;
        public readonly byte[] Payload;

        public LocalPlayerInputEvent(PlayerId playerId, int opCode, byte[] payload)
        {
            PlayerId = playerId;
            OpCode = opCode;
            Payload = payload;
        }
    }
}
