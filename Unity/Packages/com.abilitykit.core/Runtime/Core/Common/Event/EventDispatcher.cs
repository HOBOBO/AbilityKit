using System;
using System.Collections.Generic;
using AbilityKit.Core.Generic;
using AbilityKit.Core.Common.Pool;

namespace AbilityKit.Core.Common.Event
{
    public sealed class EventDispatcher
    {
        private readonly Dictionary<EventKey, IChannel> _channels = new Dictionary<EventKey, IChannel>();
        private readonly StableStringIdRegistry _stringIdRegistry = new StableStringIdRegistry();
        private int _orderSequence;

        public int GetOrRegisterEventId(string eventId)
        {
            if (eventId == null) throw new ArgumentNullException(nameof(eventId));
            return _stringIdRegistry.GetOrRegister(eventId);
        }

        public IEventSubscription Subscribe<TArgs>(string eventId, Action<TArgs> handler, int priority = 0, bool once = false)
        {
            if (eventId == null) throw new ArgumentNullException(nameof(eventId));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var id = GetOrRegisterEventId(eventId);
            return Subscribe(id, handler, priority, once);
        }

        public IEventSubscription Subscribe<TArgs>(int eventId, Action<TArgs> handler, int priority = 0, bool once = false)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var key = new EventKey(eventId, typeof(TArgs));
            if (!_channels.TryGetValue(key, out var raw))
            {
                raw = new Channel<TArgs>();
                _channels[key] = raw;
            }

            var channel = raw as Channel<TArgs>;
            if (channel == null) throw new InvalidOperationException($"Event channel type mismatch: eventId={eventId}");

            var listener = new Listener<TArgs>(handler, priority, ++_orderSequence, once);
            channel.Add(listener);
            return new Subscription(this, key, listener);
        }

        public IEventSubscription SubscribeOnce<TArgs>(string eventId, Action<TArgs> handler, int priority = 0)
        {
            if (eventId == null) throw new ArgumentNullException(nameof(eventId));
            return Subscribe(eventId, handler, priority, once: true);
        }

        public void Publish<TArgs>(string eventId, in TArgs args, bool autoReleaseArgs = true)
        {
            if (eventId == null) return;

            try
            {
                var id = GetOrRegisterEventId(eventId);
                Publish(id, in args, autoReleaseArgs);
            }
            finally
            {
                if (autoReleaseArgs)
                {
                    if (args is IDisposable d)
                    {
                        try
                        {
                            d.Dispose();
                        }
                        catch
                        {
                        }
                    }
                    else
                    {
                        var boxed = (object)args;
                        if (!Pools.TryRelease(boxed) && boxed is IPoolable p)
                        {
                            try
                            {
                                p.OnPoolRelease();
                            }
                            catch
                            {
                            }
                        }
                    }
                }
            }
        }

        public void Publish<TArgs>(int eventId, in TArgs args, bool autoReleaseArgs = true)
        {
            var key = new EventKey(eventId, typeof(TArgs));

            try
            {
                if (_channels.TryGetValue(key, out var raw))
                {
                    var channel = raw as Channel<TArgs>;
                    if (channel == null) return;
                    channel.Publish(in args);
                }
            }
            finally
            {
                if (autoReleaseArgs)
                {
                    if (args is IDisposable d)
                    {
                        try
                        {
                            d.Dispose();
                        }
                        catch
                        {
                        }
                    }
                    else
                    {
                        var boxed = (object)args;
                        if (!Pools.TryRelease(boxed) && boxed is IPoolable p)
                        {
                            try
                            {
                                p.OnPoolRelease();
                            }
                            catch
                            {
                            }
                        }
                    }
                }
            }
        }

        private void Unsubscribe(EventKey key, IListener listener)
        {
            if (!_channels.TryGetValue(key, out var raw)) return;

            try
            {
                raw.Remove(listener);
            }
            finally
            {
                if (raw.IsEmpty)
                {
                    _channels.Remove(key);
                }
            }
        }

        private interface IListener
        {
        }

        private interface IChannel
        {
            bool IsEmpty { get; }
            void Remove(IListener listener);
        }

        private sealed class Listener<TArgs> : IListener
        {
            private readonly Action<TArgs> _handler;
            private readonly bool _once;

            public Listener(Action<TArgs> handler, int priority, int order, bool once)
            {
                _handler = handler;
                Priority = priority;
                Order = order;
                _once = once;
            }

            public int Priority { get; }
            public int Order { get; }
            public bool Once => _once;

            public void Invoke(in TArgs args)
            {
                _handler?.Invoke(args);
            }
        }

        private sealed class Channel<TArgs> : IChannel
        {
            private static readonly ObjectPool<List<Listener<TArgs>>> _snapshotPool = Pools.GetPool(
                createFunc: () => new List<Listener<TArgs>>(32),
                onRelease: list => list.Clear(),
                defaultCapacity: 32,
                maxSize: 256,
                collectionCheck: false);

            private readonly List<Listener<TArgs>> _listeners = new List<Listener<TArgs>>(8);

            public bool IsEmpty => _listeners.Count == 0;

            public void Add(Listener<TArgs> listener)
            {
                var idx = FindInsertIndex(listener.Priority, listener.Order);
                _listeners.Insert(idx, listener);
            }

            public void Remove(IListener listener)
            {
                if (listener == null) return;
                _listeners.Remove(listener as Listener<TArgs>);
            }

            public void Publish(in TArgs args)
            {
                if (_listeners.Count == 0) return;

                if (_listeners.Count == 1)
                {
                    var single = _listeners[0];
                    try
                    {
                        single.Invoke(in args);
                    }
                    catch
                    {
                    }

                    if (single.Once)
                    {
                        _listeners.RemoveAt(0);
                    }

                    return;
                }

                var snapshot = _snapshotPool.Get();
                snapshot.AddRange(_listeners);

                try
                {
                    for (int i = 0; i < snapshot.Count; i++)
                    {
                        var l = snapshot[i];
                        try
                        {
                            l.Invoke(in args);
                        }
                        catch
                        {
                        }

                        if (l.Once)
                        {
                            _listeners.Remove(l);
                        }
                    }
                }
                finally
                {
                    _snapshotPool.Release(snapshot);
                }
            }

            private int FindInsertIndex(int priority, int order)
            {
                int lo = 0;
                int hi = _listeners.Count;

                while (lo < hi)
                {
                    int mid = lo + ((hi - lo) >> 1);
                    var m = _listeners[mid];

                    if (m.Priority > priority)
                    {
                        lo = mid + 1;
                        continue;
                    }

                    if (m.Priority < priority)
                    {
                        hi = mid;
                        continue;
                    }

                    if (m.Order <= order)
                    {
                        lo = mid + 1;
                        continue;
                    }

                    hi = mid;
                }

                return lo;
            }
        }

        private sealed class Subscription : IEventSubscription
        {
            private readonly EventDispatcher _dispatcher;
            private readonly EventKey _key;
            private IListener _listener;

            public Subscription(EventDispatcher dispatcher, EventKey key, IListener listener)
            {
                _dispatcher = dispatcher;
                _key = key;
                _listener = listener;
            }

            public void Unsubscribe()
            {
                var l = _listener;
                if (l == null) return;
                _listener = null;
                _dispatcher.Unsubscribe(_key, l);
            }
        }
    }
}
