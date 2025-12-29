using AbilityKit.ActionSchema;

namespace AbilityKit.Ability.Share.Impl.Moba.ActionTimeline
{
    public interface IMobaClipHandler
    {
        bool TryHandle(float time, ClipDto clip, IMobaTimelineEventSink sink);
    }
}
