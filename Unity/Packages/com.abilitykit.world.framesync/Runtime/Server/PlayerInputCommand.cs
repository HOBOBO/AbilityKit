using AbilityKit.Ability.FrameSync;

namespace AbilityKit.Ability.Server
{
    public readonly struct PlayerInputCommand
    {
        public readonly FrameIndex Frame;
        public readonly PlayerId Player;
        public readonly int OpCode;
        public readonly byte[] Payload;

        public PlayerInputCommand(FrameIndex frame, PlayerId player, int opCode, byte[] payload)
        {
            Frame = frame;
            Player = player;
            OpCode = opCode;
            Payload = payload;
        }
    }
}
