namespace AbilityKit.Triggering.Eventing
{
    public enum EEventDispatchMode
    {
        Queued = 0,
        Immediate = 1,
    }

    public readonly struct EventBusOptions
    {
        public readonly EEventDispatchMode DispatchMode;
        public readonly int MaxFlushPasses;

        public EventBusOptions(EEventDispatchMode dispatchMode, int maxFlushPasses)
        {
            DispatchMode = dispatchMode;
            MaxFlushPasses = maxFlushPasses;
        }

        public static EventBusOptions Default => new EventBusOptions(EEventDispatchMode.Immediate, 1024);
    }
}
