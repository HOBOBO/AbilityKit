namespace AbilityKit.Ability.Share.Common.Event
{
    public static class GlobalEventDispatcher
    {
        public static readonly EventDispatcher Instance = new EventDispatcher();

        public static int GetOrRegisterEventId(string eventId)
        {
            return Instance.GetOrRegisterEventId(eventId);
        }

        public static IEventSubscription Subscribe<TArgs>(string eventId, System.Action<TArgs> handler, int priority = 0, bool once = false)
        {
            return Instance.Subscribe(eventId, handler, priority, once);
        }

        public static IEventSubscription Subscribe<TArgs>(int eventId, System.Action<TArgs> handler, int priority = 0, bool once = false)
        {
            return Instance.Subscribe(eventId, handler, priority, once);
        }

        public static void Publish<TArgs>(string eventId, in TArgs args, bool autoReleaseArgs = true)
        {
            Instance.Publish(eventId, in args, autoReleaseArgs);
        }

        public static void Publish<TArgs>(int eventId, in TArgs args, bool autoReleaseArgs = true)
        {
            Instance.Publish(eventId, in args, autoReleaseArgs);
        }
    }
}
