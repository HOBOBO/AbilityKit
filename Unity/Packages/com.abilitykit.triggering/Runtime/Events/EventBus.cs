using System;
using System.Collections.Generic;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Runtime;

namespace AbilityKit.Triggering.Eventing
{
    /// <summary>
    /// 按事件键和载荷类型维护订阅，并以立即或排队模式派发事件。
    /// </summary>
    public sealed class EventBus : IEventBus
    {
        private readonly Dictionary<Type, object> _channelsByArgsType = new Dictionary<Type, object>();
        private readonly List<IFlushableChannel> _flushables = new List<IFlushableChannel>(64);
        private readonly EventBusOptions _options;
        /// <summary>
        /// 获取构造时选定的派发模式。
        /// </summary>
        public EEventDispatchMode DispatchMode => _options.DispatchMode;

        /// <summary>
        /// 创建默认立即派发的事件总线。
        /// </summary>
        public EventBus()
            : this(EventBusOptions.Default)
        {
        }

        /// <summary>
        /// 按显式派发模式与排队推进上限创建事件总线。
        /// </summary>
        public EventBus(EventBusOptions options)
        {
            _options = options;
        }

        /// <summary>
        /// 向现有类型化通道发布事件；立即模式同步派发，排队模式在推进时派发。
        /// </summary>
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

        /// <summary>
        /// 向现有类型化通道发布事件，并传递调用方提供的执行控制。
        /// </summary>
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

        /// <summary>
        /// 注册只接收载荷的回调。调用方负责释放返回的订阅句柄。
        /// </summary>
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

        /// <summary>
        /// 注册接收载荷与执行控制的回调。调用方负责释放返回的订阅句柄。
        /// </summary>
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

        /// <summary>
        /// 查询指定事件是否存在活动订阅，不创建通道。
        /// </summary>
        public bool HasSubscribers<TArgs>(EventKey<TArgs> key)
        {
            if (!TryGetChannelDictionary<TArgs>(out var dict)) return false;
            return dict.TryGetValue(key, out var channel) && channel.SubscriberCount > 0;
        }

        /// <summary>
        /// 由宿主推进排队派发；立即模式下不执行操作。
        /// </summary>
        /// <exception cref="InvalidOperationException">达到推进轮数上限且未提前结束时抛出。</exception>
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
