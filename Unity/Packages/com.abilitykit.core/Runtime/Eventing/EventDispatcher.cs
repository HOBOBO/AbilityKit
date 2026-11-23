using System;
using System.Collections.Generic;
using AbilityKit.Core.Identifiers;
using AbilityKit.Core.Pooling;

namespace AbilityKit.Core.Eventing
{
    /// <summary>
    /// 按事件标识与参数类型隔离通道，执行具有稳定优先级顺序的同步事件分发。
    /// </summary>
    /// <remarks>
    /// 在发布线程同步调用，优先级较高者先执行，同优先级按注册顺序执行。
    /// 非线程安全；订阅、退订、发布及清空需要单一所有者或外部同步。
    /// 多监听器按发布时的快照分发；新增或移除订阅不撤销当前快照中的普通回调。
    /// 一次性订阅在回调开始前被标记为已执行，嵌套发布及回调异常均不会使其重复执行。
    /// </remarks>
    public sealed partial class EventDispatcher
    {
        private readonly Dictionary<EventKey, IChannel> _channels = new Dictionary<EventKey, IChannel>();
        private StableStringIdRegistry _stringIdRegistry = new StableStringIdRegistry();
        private int _orderSequence;

        /// <summary>
        /// 获取或注册字符串事件名的稳定整数标识。
        /// </summary>
        /// <remarks>
        /// 空引用会抛出参数异常，空字符串保留为有效事件名。
        /// 哈希冲突由稳定标识注册表报告；该标识与直接使用同值整数订阅共享事件通道。
        /// </remarks>
        /// <param name="eventId">事件名称，不能是空引用。</param>
        /// <returns>由稳定标识注册表分配的整数标识。</returns>
        public int GetOrRegisterEventId(string eventId)
        {
            if (eventId == null) throw new ArgumentNullException(nameof(eventId));
            return _stringIdRegistry.GetOrRegister(eventId);
        }

        /// <summary>
        /// 向字符串事件名称注册指定参数类型的回调。
        /// </summary>
        /// <remarks>
        /// 在发布线程同步调用，优先级较高者先执行，同优先级按注册顺序执行。
        /// 非线程安全；订阅、退订、发布及清空需要单一所有者或外部同步。
        /// </remarks>
        /// <typeparam name="TArgs">事件参数的静态类型；不同类型使用独立通道。</typeparam>
        /// <param name="eventId">事件名称或整数标识；字符串不能是空引用。</param>
        /// <param name="handler">同步执行的回调，不能是空引用。</param>
        /// <param name="priority">优先级，较大的值先执行。</param>
        /// <param name="once">是否在第一次回调前移除订阅，并保证最多执行一次。</param>
        /// <returns>由调用方持有的幂等退订句柄；不会释放回调目标。</returns>
        public IEventSubscription Subscribe<TArgs>(string eventId, Action<TArgs> handler, int priority = 0, bool once = false)
        {
            if (eventId == null) throw new ArgumentNullException(nameof(eventId));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var id = GetOrRegisterEventId(eventId);
            return Subscribe(id, handler, priority, once);
        }

        /// <summary>
        /// 向整数事件标识注册指定参数类型的回调。
        /// </summary>
        /// <remarks>
        /// 在发布线程同步调用，优先级较高者先执行，同优先级按注册顺序执行。
        /// 非线程安全；订阅、退订、发布及清空需要单一所有者或外部同步。
        /// </remarks>
        /// <typeparam name="TArgs">事件参数的静态类型；不同类型使用独立通道。</typeparam>
        /// <param name="eventId">事件名称或整数标识；字符串不能是空引用。</param>
        /// <param name="handler">同步执行的回调，不能是空引用。</param>
        /// <param name="priority">优先级，较大的值先执行。</param>
        /// <param name="once">是否在第一次回调前移除订阅，并保证最多执行一次。</param>
        /// <returns>由调用方持有的幂等退订句柄；不会释放回调目标。</returns>
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

        /// <summary>
        /// 注册最多执行一次的回调；在回调执行前移除订阅并记录执行状态。
        /// </summary>
        /// <remarks>
        /// 嵌套发布或回调异常不重试已执行的监听项。
        /// </remarks>
        /// <typeparam name="TArgs">事件参数的静态类型；不同类型使用独立通道。</typeparam>
        /// <param name="eventId">事件名称或整数标识；字符串不能是空引用。</param>
        /// <param name="handler">同步执行的回调，不能是空引用。</param>
        /// <param name="priority">优先级，较大的值先执行。</param>
        /// <returns>由调用方持有的幂等退订句柄；不会释放回调目标。</returns>
        public IEventSubscription SubscribeOnce<TArgs>(string eventId, Action<TArgs> handler, int priority = 0)
        {
            if (eventId == null) throw new ArgumentNullException(nameof(eventId));
            return Subscribe(eventId, handler, priority, once: true);
        }

        /// <summary>
        /// 清空订阅及字符串标识注册表；旧句柄不能移除之后创建的新订阅。
        /// </summary>
        /// <remarks>
        /// 正在执行的发布保留已取得的快照并继续分发。清空不会释放回调目标或参数，
        /// 也不会改变事件名称对应的稳定哈希算法。
        /// </remarks>
        public void Clear()
        {
            _channels.Clear();
            _stringIdRegistry = new StableStringIdRegistry();
            _orderSequence = 0;
        }

        /// <summary>
        /// 同步发布字符串事件，并按选项在分发完成后释放参数。
        /// </summary>
        /// <remarks>
        /// 事件名为空引用时直接忽略，参数也不会被自动释放；空字符串是有效事件名。
        /// 即使没有监听器，启用自动释放后也会处理参数。参数实现可释放接口时优先释放，
        /// 否则尝试归还全局对象池；未成功归还且实现池化接口时调用释放回调。
        /// 可释放接口及池化兜底回调的异常被隔离；对象池自身的归还异常仍按池契约传播。
        /// 处理器异常被隔离。关闭自动释放时，参数所有权和最终释放责任由调用方保留。
        /// 嵌套发布同一参数时，调用方必须避免多次自动释放。
        /// </remarks>
        /// <typeparam name="TArgs">事件参数的静态类型。</typeparam>
        /// <param name="eventId">事件名称或整数标识。</param>
        /// <param name="args">本次发布共享的参数；自动释放后不得继续保留或使用。</param>
        /// <param name="autoReleaseArgs">是否在本次发布结束时执行参数释放流程。</param>
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
        /// 同步发布整数事件，并按选项在分发完成后释放参数。
        /// </summary>
        /// <remarks>
        /// 即使没有监听器，启用自动释放后也会处理参数。参数实现可释放接口时优先释放，
        /// 否则尝试归还全局对象池；未成功归还且实现池化接口时调用释放回调。
        /// 可释放接口及池化兜底回调的异常被隔离；对象池自身的归还异常仍按池契约传播。
        /// 处理器异常被隔离。关闭自动释放时，参数所有权和最终释放责任由调用方保留。
        /// 嵌套发布同一参数时，调用方必须避免多次自动释放。
        /// </remarks>
        /// <typeparam name="TArgs">事件参数的静态类型。</typeparam>
        /// <param name="eventId">事件名称或整数标识。</param>
        /// <param name="args">本次发布共享的参数；自动释放后不得继续保留或使用。</param>
        /// <param name="autoReleaseArgs">是否在本次发布结束时执行参数释放流程。</param>
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
                    /*
                     * 保留既有异常隔离契约，避免单个用户回调失败打断事件处理。
                     */
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
                    /*
                     * 保留既有异常隔离契约，避免单个用户回调失败打断事件处理。
                     */
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

            /*
             * 重入发布可能已经移除原通道并安装替代通道。
             * 只能移除当前操作观察到的同一个实例，避免误删新订阅。
             */
            if (_channels.TryGetValue(key, out var current) && ReferenceEquals(current, channel))
            {
                _channels.Remove(key);
            }
        }

        /// <summary>
        /// 持有单项监听身份；退订先撤销句柄引用，再请求原分发器移除该监听项。
        /// </summary>
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
