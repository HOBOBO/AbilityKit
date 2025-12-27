using System;
using System.Collections.Generic;

namespace AbilityKit.Triggering
{
    public sealed class EventBus : IEventBus
    {
        private readonly Dictionary<string, List<IEventHandler>> _handlersByEventId = new Dictionary<string, List<IEventHandler>>(StringComparer.Ordinal);

        public IEventSubscription Subscribe(string eventId, IEventHandler handler)
        {
            if (eventId == null) throw new ArgumentNullException(nameof(eventId));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            if (!_handlersByEventId.TryGetValue(eventId, out var handlers))
            {
                handlers = new List<IEventHandler>();
                _handlersByEventId[eventId] = handlers;
            }

            handlers.Add(handler);
            return new Subscription(this, eventId, handler);
        }

        public void Publish(in TriggerEvent evt)
        {
            if (evt.Id == null) return;

            if (_handlersByEventId.TryGetValue(evt.Id, out var handlers))
            {
                for (int i = 0; i < handlers.Count; i++)
                {
                    handlers[i]?.Handle(in evt);
                }
            }
        }

        private void Unsubscribe(string eventId, IEventHandler handler)
        {
            if (eventId == null || handler == null) return;

            if (_handlersByEventId.TryGetValue(eventId, out var handlers))
            {
                handlers.Remove(handler);
                if (handlers.Count == 0)
                {
                    _handlersByEventId.Remove(eventId);
                }
            }
        }

        private sealed class Subscription : IEventSubscription
        {
            private readonly EventBus _bus;
            private readonly string _eventId;
            private IEventHandler _handler;

            public Subscription(EventBus bus, string eventId, IEventHandler handler)
            {
                _bus = bus;
                _eventId = eventId;
                _handler = handler;
            }

            public void Unsubscribe()
            {
                var h = _handler;
                if (h == null) return;
                _handler = null;
                _bus.Unsubscribe(_eventId, h);
            }
        }
    }
}
