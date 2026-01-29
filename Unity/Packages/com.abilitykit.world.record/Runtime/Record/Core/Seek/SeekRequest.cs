using AbilityKit.Ability.FrameSync;

namespace AbilityKit.Ability.Share.Common.Record.Core
{
    public readonly struct SeekRequest
    {
        public readonly FrameIndex Frame;
        public readonly SeekMode Mode;

        public SeekRequest(FrameIndex frame, SeekMode mode)
        {
            Frame = frame;
            Mode = mode;
        }
    }

    public enum SeekMode
    {
        Absolute = 0,
        Relative = 1,
    }
}
