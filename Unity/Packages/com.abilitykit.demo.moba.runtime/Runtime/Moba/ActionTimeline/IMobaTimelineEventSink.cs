namespace AbilityKit.Demo.Moba.ActionTimeline
{
    public interface IMobaTimelineEventSink
    {
        void OnTriggerLog(float time, string message);
    }
}
