using System;
using System.Collections.Generic;
using AbilityKit.Core.Pooling;

namespace AbilityKit.Core.Eventing
{
    /// <remarks>
    /// 通道负责监听项排序、分发快照及一次性回调状态；通道登记和参数释放由分发器管理。
    /// </remarks>
    public sealed partial class EventDispatcher
    {
        /// <summary>
        /// 为通道内的监听项提供不暴露参数类型的身份标识。
        /// </summary>
        private interface IListener
        {
        }

        /// <summary>
        /// 封装通道的空状态及按监听项身份退订的内部操作。
        /// </summary>
        private interface IChannel
        {
            bool IsEmpty { get; }
            void Remove(IListener listener);
        }

        /// <summary>
        /// 保存订阅回调与排序信息，并保证一次性监听项在嵌套发布中最多执行一次。
        /// </summary>
        private sealed class Listener<TArgs> : IListener
        {
            private readonly Action<TArgs> _handler;
            private readonly bool _once;
            private bool _hasBeenInvoked;

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
                if (_once)
                {
                    /*
                     * 嵌套发布可能先消费外层快照中尚未调用的一次性监听项。
                     * 只从实时列表移除不能撤销旧快照，必须在调用回调前记录已执行状态。
                     */
                    if (_hasBeenInvoked) return;
                    _hasBeenInvoked = true;
                }

                _handler?.Invoke(args);
            }
        }

        /// <summary>
        /// 持有按优先级排列的监听项，并通过池化快照支持回调中的订阅变更与嵌套发布。
        /// </summary>
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
                        /*
                         * 保留既有异常隔离契约，避免单个用户回调失败打断事件处理。
                         */
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
                            /*
                             * 保留既有异常隔离契约，避免单个用户回调失败打断事件处理。
                             */
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
    }
}
