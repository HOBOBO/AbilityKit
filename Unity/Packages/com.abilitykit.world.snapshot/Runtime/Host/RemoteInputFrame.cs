using AbilityKit.Ability.FrameSync;

namespace AbilityKit.Ability.Host
{
    public readonly struct RemoteInputFrame
    {
        public RemoteInputFrame(FrameIndex frame, PlayerInputCommand[] commands)
        {
            Frame = frame;
            Commands = commands;
        }

        public FrameIndex Frame { get; }

        public PlayerInputCommand[] Commands { get; }
    }
}
