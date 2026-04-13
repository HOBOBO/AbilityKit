using AbilityKit.Ability.FrameSync;

namespace AbilityKit.Core.Common.Record.Core
{
    public interface IEventTrackWriter
    {
        void Append(FrameIndex frame, RecordEventType eventType, byte[] payload);
    }
}
