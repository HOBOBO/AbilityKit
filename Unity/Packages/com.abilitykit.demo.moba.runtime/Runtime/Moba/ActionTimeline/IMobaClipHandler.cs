using AbilityKit.ActionSchema;

namespace AbilityKit.Demo.Moba.ActionTimeline
{
    public interface IMobaClipHandler
    {
        bool TryHandle(float time, ClipDto clip, IMobaTimelineEventSink sink);
    }
}
