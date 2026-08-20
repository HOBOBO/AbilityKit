using System;
using System.Collections.Generic;
using AbilityKit.Core.Identifiers;
using AbilityKit.Core.Pooling;

namespace AbilityKit.Core.Eventing
{
    /// <summary>
    /// Dispatches strongly typed events in stable priority order.
    /// Handler failures are isolated, and the dispatcher is not thread-safe.
    /// </summary>
    public sealed class EventDispatcher
    {
        private readonly Dictionary<EventKey, IChannel> _channels = new Dictionary<EventKey, IChannel>();
        private StableStringIdRegistry _stringIdRegistry = new StableStringIdRegistry();
        private int _orderSequence;

        /// <summary>Gets or registers the deterministic integer identifier for a string event name.</summary>
        /// <param name="eventId">The non-null event name.</param>
        /// <returns>The stable identifier associated with <paramref name="eventId"/>.</returns>
        public int GetOrRegisterEventId(string eventId)
        {
            if (eventId == null) throw new ArgumentNullException(nameof(eventId));
            return _stringIdRegistry.GetOrRegister(eventId);
        }

        /// <summary>Subscribes a typed handler to a string event name.</summary>
        /// <typeparam name="TArgs">The event argument type.</typeparam>
        /// <param name="eventId">The non-null event name.</param>
        /// <param name="handler">The handler to invoke.</param>
        /// <param name="priority">The priority; higher values run first.</param>
        /// <param name="once">Whether to remove the handler before its first invocation.</param>
        /// <returns>An idempotent subscription handle.</returns>
        public IEventSubscription Subscribe<TArgs>(string eventId, Action<TArgs> handler, int priority = 0, bool once = false)
        {
            if (eventId == null) throw new ArgumentNullException(nameof(eventId));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var id = GetOrRegisterEventId(eventId);
            return Subscribe(id, handler, priority, once);
        }

        /// <summary>Subscribes a typed handler to an integer event identifier.</summary>
        /// <typeparam name="TArgs">The event argument type.</typeparam>
        /// <param name="eventId">The event identifier.</param>
        /// <param name="handler">The handler to invoke.</param>
        /// <param name="priority">The priority; higher values run first.</param>
        /// <param name="once">Whether to remove the handler before its first invocation.</param>
        /// <returns>An idempotent subscription handle.</returns>
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

        /// <summary>Subscribes a typed handler that is removed before its first invocation.</summary>
        /// <typeparam name="TArgs">The event argument type.</typeparam>
        /// <param name="eventId">The non-null event name.</param>
        /// <param name="handler">The handler to invoke.</param>
        /// <param name="priority">The priority; higher values run first.</param>
        /// <returns>An idempotent subscription handle.</returns>
        public IEventSubscription SubscribeOnce<TArgs>(string eventId, Action<TArgs> handler, int priority = 0)
        {
            if (eventId == null) throw new ArgumentNullException(nameof(eventId));
            return Subscribe(eventId, handler, priority, once: true);
        }

        /// <summary>
        /// Removes all subscriptions and registered string event identifiers.
        /// </summary>
        public void Clear()
        {
            _channels.Clear();
            _stringIdRegistry = new StableStringIdRegistry();
            _orderSequence = 0;
        }

        /// <summary>
        /// Publishes typed arguments to a string event name. Handler failures do not escape.
        /// </summary>
        /// <typeparam name="TArgs">The event argument type.</typeparam>
        /// <param name="eventId">The event name; a null value is ignored for compatibility.</param>
        /// <param name="args">The arguments passed to each handler.</param>
        /// <param name="autoReleaseArgs">
        /// Whether to dispose the arguments or return pool-backed arguments after dispatch.
        /// </param>
        public void Publish<TArgs>(string eventId, in TArgs args, bool autoReleaseArgs = true)
        {
            if (eventId == null) return;

            try
            {
                var id = GetOrRegisterEventId(eventId);
                Publish(id, in args, autoReleaseArgs: false);
            }
            finally
            {
                if (autoReleaseArgs)
                {
                    ReleaseArgs(in args);
                }
            }
        }

        /// <summary>
        /// Publishes typed arguments to an integer event identifier. Handler failures do not escape.
        /// </summary>
        /// <typeparam name="TArgs">The event argument type.</typeparam>
        /// <param name="eventId">The event identifier.</param>
        /// <param name="args">The arguments passed to each handler.</param>
        /// <param name="autoReleaseArgs">
        /// Whether to dispose the arguments or return pool-backed arguments after dispatch.
        /// </param>
        public void Publish<TArgs>(int eventId, in TArgs args, bool autoReleaseArgs = true)
        {
            var key = new EventKey(eventId, typeof(TArgs));

            try
            {
                if (_channels.TryGetValue(key, out var raw))
                {
                    var channel = raw as Channel<TArgs>;
                    if (channel == null) return;
                    try
                    {
                        channel.Publish(in args);
                    }
                    finally
                    {
                        RemoveChannelIfEmpty(key, channel);
                    }
                }
            }
            finally
            {
                if (autoReleaseArgs)
                {
                    ReleaseArgs(in args);
                }
            }
        }

        private static void ReleaseArgs<TArgs>(in TArgs args)
        {
            if (args is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch
                {
                }

                return;
            }

            object? boxed = args;
            if (boxed == null) return;
            if (!Pools.TryRelease(boxed) && boxed is IPoolable poolable)
            {
                try
                {
                    poolable.OnPoolRelease();
                }
                catch
                {
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
                RemoveChannelIfEmpty(key, raw);
            }
        }

        private void RemoveChannelIfEmpty(EventKey key, IChannel channel)
        {
            if (!channel.IsEmpty) return;

            // A reentrant publish may have removed this channel and installed a replacement.
            // Only remove the same instance that was observed by the current operation.
            if (_channels.TryGetValue(key, out var current) && ReferenceEquals(current, channel))
            {
                _channels.Remove(key);
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
                if (listener is Listener<TArgs> typedListener)
                {
                    _listeners.Remove(typedListener);
                }
            }

            public void Publish(in TArgs args)
            {
                if (_listeners.Count == 0) return;

                if (_listeners.Count == 1)
                {
                    var single = _listeners[0];
                    if (single.Once)
                    {
                        _listeners.Remove(single);
                    }

                    try
                    {
                        single.Invoke(in args);
                    }
                    catch
                    {
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
                        if (l.Once)
                        {
                            _listeners.Remove(l);
                        }

                        try
                        {
                            l.Invoke(in args);
                        }
                        catch
                        {
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
            private IListener? _listener;

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
