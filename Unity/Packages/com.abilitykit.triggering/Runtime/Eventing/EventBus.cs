using System;
using System.Collections.Generic;
using AbilityKit.Core.Common.Event;
using AbilityKit.Triggering.Runtime;

namespace AbilityKit.Triggering.Eventing
{
    public sealed class EventBus : IEventBus
    {
        private readonly Dictionary<Type, object> _channelsByArgsType = new Dictionary<Type, object>();
        private readonly List<IFlushableChannel> _flushables = new List<IFlushableChannel>(64);
        private readonly EventBusOptions _options;

        public EventBus()
            : this(EventBusOptions.Default)
        {
        }

        public EventBus(EventBusOptions options)
        {
            _options = options;
        }

        public void Publish<TArgs>(EventKey<TArgs> key, in TArgs args)
        {
            if (!TryGetChannelDictionary<TArgs>(out var dict)) return;
            if (!dict.TryGetValue(key, out var channel)) return;

            if (_options.DispatchMode == EEventDispatchMode.Immediate)
            {
                channel.DispatchImmediate(args, null);
                return;
            }

            channel.Enqueue(args, null);
        }

        public void Publish<TArgs>(EventKey<TArgs> key, in TArgs args, ExecutionControl control)
        {
            if (!TryGetChannelDictionary<TArgs>(out var dict)) return;
            if (!dict.TryGetValue(key, out var channel)) return;

            if (_options.DispatchMode == EEventDispatchMode.Immediate)
            {
                channel.DispatchImmediate(args, control);
                return;
            }

            channel.Enqueue(args, control);
        }

        public IDisposable Subscribe<TArgs>(EventKey<TArgs> key, Action<TArgs> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var dict = GetOrCreateChannelDictionary<TArgs>();
            if (!dict.TryGetValue(key, out var channel))
            {
                channel = new EventChannel<TArgs>();
                dict.Add(key, channel);
                _flushables.Add(channel);
            }

            return channel.Subscribe(handler);
        }

        public IDisposable Subscribe<TArgs>(EventKey<TArgs> key, Action<TArgs, ExecutionControl> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var dict = GetOrCreateChannelDictionary<TArgs>();
            if (!dict.TryGetValue(key, out var channel))
            {
                channel = new EventChannel<TArgs>();
                dict.Add(key, channel);
                _flushables.Add(channel);
            }

            return channel.Subscribe(handler);
        }

        public void Flush()
        {
            if (_options.DispatchMode == EEventDispatchMode.Immediate) return;

            var maxPasses = _options.MaxFlushPasses <= 0 ? 1 : _options.MaxFlushPasses;
            for (int pass = 0; pass < maxPasses; pass++)
            {
                var flushedAny = false;
                for (int i = 0; i < _flushables.Count; i++)
                {
                    var channel = _flushables[i];
                    if (!channel.HasPending) continue;
                    flushedAny |= channel.FlushOnce();
                }

                if (!flushedAny) return;
            }

            throw new InvalidOperationException("EventBus.Flush exceeded MaxFlushPasses. Possible infinite event loop.");
        }

        private Dictionary<EventKey<TArgs>, EventChannel<TArgs>> GetOrCreateChannelDictionary<TArgs>()
        {
            var type = typeof(TArgs);
            if (_channelsByArgsType.TryGetValue(type, out var obj)) return (Dictionary<EventKey<TArgs>, EventChannel<TArgs>>)obj;

            var dict = new Dictionary<EventKey<TArgs>, EventChannel<TArgs>>();
            _channelsByArgsType.Add(type, dict);
            return dict;
        }

        private bool TryGetChannelDictionary<TArgs>(out Dictionary<EventKey<TArgs>, EventChannel<TArgs>> dict)
        {
            var type = typeof(TArgs);
            if (_channelsByArgsType.TryGetValue(type, out var obj))
            {
                dict = (Dictionary<EventKey<TArgs>, EventChannel<TArgs>>)obj;
                return true;
            }

            dict = null;
            return false;
        }
    }
}
