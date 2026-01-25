using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.Common.Pool;

namespace AbilityKit.Ability.Triggering
{
    public sealed class EventBus : IEventBus
    {
        private readonly Dictionary<string, List<IEventHandler>> _handlersByEventId = new Dictionary<string, List<IEventHandler>>(StringComparer.Ordinal);

        private static readonly ObjectPool<List<IEventHandler>> _handlerListPool = Pools.GetPool(
            createFunc: () => new List<IEventHandler>(16),
            onRelease: list => list.Clear(),
            defaultCapacity: 16,
            maxSize: 256,
            collectionCheck: false);

        public IEventSubscription Subscribe(string eventId, IEventHandler handler)
        {
            if (eventId == null) throw new ArgumentNullException(nameof(eventId));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            if (!_handlersByEventId.TryGetValue(eventId, out var handlers))
            {
                handlers = new List<IEventHandler>(4);
                _handlersByEventId[eventId] = handlers;
            }

            handlers.Add(handler);
            return new Subscription(this, eventId, handler);
        }

        public void Publish(in TriggerEvent evt)
        {
            if (evt.Id == null) return;

            try
            {
                if (_handlersByEventId.TryGetValue(evt.Id, out var handlers))
                {
                    if (handlers.Count == 0) return;

                    if (handlers.Count == 1)
                    {
                        try
                        {
                            handlers[0]?.Handle(in evt);
                        }
                        catch
                        {
                        }
                        return;
                    }

                    var snapshot = _handlerListPool.Get();
                    snapshot.AddRange(handlers);

                    try
                    {
                        for (int i = 0; i < snapshot.Count; i++)
                        {
                            try
                            {
                                snapshot[i]?.Handle(in evt);
                            }
                            catch
                            {
                            }
                        }
                    }
                    finally
                    {
                        _handlerListPool.Release(snapshot);
                    }
                }
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

            if (handler is IDisposable d)
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
