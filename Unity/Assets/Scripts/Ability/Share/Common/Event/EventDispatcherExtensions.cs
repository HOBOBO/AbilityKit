using System;

namespace AbilityKit.Ability.Share.Common.Event
{
    public static class EventDispatcherExtensions
    {
        public static IEventSubscription SubscribeOnce<TArgs>(this EventDispatcher dispatcher, string eventId, Action<TArgs> handler, int priority = 0)
        {
            if (dispatcher == null) throw new ArgumentNullException(nameof(dispatcher));
            return dispatcher.Subscribe(eventId, handler, priority, once: true);
        }
    }
}
