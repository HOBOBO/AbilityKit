using AbilityKit.Ability.FrameSync;

namespace AbilityKit.Ability.Share.Common.Record.Core
{
    public interface IEventTrackWriter
    {
        void Append(FrameIndex frame, RecordEventType eventType, byte[] payload);
    }
}
