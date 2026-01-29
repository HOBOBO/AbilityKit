namespace AbilityKit.Ability.Share.Impl.Moba.ActionTimeline
{
    public interface IMobaTimelineEventSink
    {
        void OnTriggerLog(float time, string message);
    }
}
