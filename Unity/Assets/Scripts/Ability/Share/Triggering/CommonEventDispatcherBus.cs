using System;
using AbilityKit.Ability.Share.Common.Event;

namespace AbilityKit.Ability.Triggering
{
    public sealed class CommonEventDispatcherBus : IEventBus
    {
        private readonly EventDispatcher _dispatcher;

        public CommonEventDispatcherBus(EventDispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public IEventSubscription Subscribe(string eventId, IEventHandler handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var sub = _dispatcher.Subscribe<TriggerEvent>(eventId, evt =>
            {
                try
                {
                    handler.Handle(in evt);
                }
                catch
                {
                }
            });

            return new SubscriptionAdapter(sub);
        }

        public void Publish(in TriggerEvent evt)
        {
            if (evt.Id == null) return;

            try
            {
                _dispatcher.Publish(evt.Id, evt, autoReleaseArgs: false);
            }
            finally
            {
                if (evt.Args is IDisposable d)
                {
                    try
                    {
                        d.Dispose();
                    }
                    catch
                    {
                    }
                }
            }
        }

        private sealed class SubscriptionAdapter : IEventSubscription
        {
            private AbilityKit.Ability.Share.Common.Event.IEventSubscription _sub;

            public SubscriptionAdapter(AbilityKit.Ability.Share.Common.Event.IEventSubscription sub)
            {
                _sub = sub;
            }

            public void Unsubscribe()
            {
                var s = _sub;
                if (s == null) return;
                _sub = null;
                s.Unsubscribe();
            }
        }
    }
}
