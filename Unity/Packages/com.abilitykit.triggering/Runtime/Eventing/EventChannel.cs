using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Runtime;

namespace AbilityKit.Triggering.Eventing
{
    internal interface IFlushableChannel
    {
        bool HasPending { get; }
        bool FlushOnce();
    }

    /// <summary>
    /// 带优先级的 Handler 条目
    /// </summary>
    internal readonly struct OrderedHandler<TArgs>
    {
        public readonly Action<TArgs, ExecutionControl> Handler;
        public readonly int Order;

        public OrderedHandler(Action<TArgs, ExecutionControl> handler, int order)
        {
            Handler = handler;
            Order = order;
        }
    }

    internal sealed class EventChannel<TArgs> : IFlushableChannel
    {
        private OrderedHandler<TArgs>[] _handlers = new OrderedHandler<TArgs>[8];
        private int _handlerCount = 0;
        private bool _handlersDirty = false;

        private List<Item> _queue = new List<Item>(16);
        private List<Item> _dispatch = new List<Item>(16);

        public bool HasPending => _queue.Count > 0;

        public IDisposable Subscribe(Action<TArgs> handler)
        {
            return Subscribe(handler, 0);
        }

        public IDisposable Subscribe(Action<TArgs> handler, int order)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            Action<TArgs, ExecutionControl> adapter = (args, _) => handler(args);
            return SubscribeInternal(adapter, order);
        }

        public IDisposable Subscribe(Action<TArgs, ExecutionControl> handler)
        {
            return Subscribe(handler, 0);
        }

        public IDisposable Subscribe(Action<TArgs, ExecutionControl> handler, int order)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            return SubscribeInternal(handler, order);
        }

        private IDisposable SubscribeInternal(Action<TArgs, ExecutionControl> handler, int order)
        {
            EnsureHandlerCapacity(_handlerCount + 1);
            _handlers[_handlerCount++] = new OrderedHandler<TArgs>(handler, order);
            _handlersDirty = true;
            return new Subscription(this, handler);
        }

        public void Enqueue(TArgs args, ExecutionControl control)
        {
            _queue.Add(new Item(args, control));
        }

        public void DispatchImmediate(TArgs args, ExecutionControl control)
        {
            SortHandlersIfNeeded();
            for (int i = 0; i < _handlerCount; i++)
            {
                // ShortCircuit: 如果已停止传播，跳过后续 Handler
                if (control != null && control.StopPropagation) break;
                _handlers[i].Handler(args, control);
            }
        }

        public bool FlushOnce()
        {
            if (_queue.Count == 0) return false;

            SortHandlersIfNeeded();

            var tmp = _dispatch;
            _dispatch = _queue;
            _queue = tmp;

            for (int e = 0; e < _dispatch.Count; e++)
            {
                var item = _dispatch[e];

                for (int i = 0; i < _handlerCount; i++)
                {
                    // ShortCircuit: 如果已停止传播，跳过后续 Handler
                    if (item.Control != null && item.Control.StopPropagation) break;
                    _handlers[i].Handler(item.Args, item.Control);
                }
            }

            _dispatch.Clear();
            return true;
        }

        private void SortHandlersIfNeeded()
        {
            if (!_handlersDirty) return;
            _handlersDirty = false;

            // 插入排序：Handler 数量通常较少（<20），插入排序更快
            for (int i = 1; i < _handlerCount; i++)
            {
                var key = _handlers[i];
                int j = i - 1;
                while (j >= 0 && _handlers[j].Order > key.Order)
                {
                    _handlers[j + 1] = _handlers[j];
                    j--;
                }
                _handlers[j + 1] = key;
            }
        }

        private void EnsureHandlerCapacity(int min)
        {
            if (_handlers.Length >= min) return;
            var newArray = new OrderedHandler<TArgs>[Math.Max(_handlers.Length * 2, min)];
            Array.Copy(_handlers, newArray, _handlerCount);
            _handlers = newArray;
        }

        private void Unsubscribe(Action<TArgs, ExecutionControl> handler)
        {
            for (int i = 0; i < _handlerCount; i++)
            {
                if (_handlers[i].Handler == handler)
                {
                    // 用最后一个元素覆盖，保持数组紧凑
                    _handlers[i] = _handlers[--_handlerCount];
                    _handlers[_handlerCount] = default;
                    return;
                }
            }
        }

        private readonly struct Item
        {
            public readonly TArgs Args;
            public readonly ExecutionControl Control;

            public Item(TArgs args, ExecutionControl control)
            {
                Args = args;
                Control = control;
            }
        }

        private sealed class Subscription : IDisposable
        {
            private EventChannel<TArgs> _channel;
            private Action<TArgs, ExecutionControl> _handler;

            public Subscription(EventChannel<TArgs> channel, Action<TArgs, ExecutionControl> handler)
            {
                _channel = channel;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_channel == null) return;
                _channel.Unsubscribe(_handler);
                _channel = null;
                _handler = null;
            }
        }
    }
}
