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

    internal sealed class EventChannel<TArgs> : IFlushableChannel
    {
        private readonly List<Action<TArgs, ExecutionControl>> _handlers = new List<Action<TArgs, ExecutionControl>>(8);
        private List<Item> _queue = new List<Item>(16);
        private List<Item> _dispatch = new List<Item>(16);

        public bool HasPending => _queue.Count > 0;

        public IDisposable Subscribe(Action<TArgs> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            Action<TArgs, ExecutionControl> adapter = (args, _) => handler(args);
            _handlers.Add(adapter);
            return new Subscription(this, adapter);
        }

        public IDisposable Subscribe(Action<TArgs, ExecutionControl> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _handlers.Add(handler);
            return new Subscription(this, handler);
        }

        public void Enqueue(TArgs args, ExecutionControl control)
        {
            _queue.Add(new Item(args, control));
        }

        public void DispatchImmediate(TArgs args, ExecutionControl control)
        {
            for (int i = 0; i < _handlers.Count; i++)
            {
                _handlers[i](args, control);
            }
        }

        public bool FlushOnce()
        {
            if (_queue.Count == 0) return false;

            var tmp = _dispatch;
            _dispatch = _queue;
            _queue = tmp;

            for (int e = 0; e < _dispatch.Count; e++)
            {
                var item = _dispatch[e];
                for (int i = 0; i < _handlers.Count; i++)
                {
                    _handlers[i](item.Args, item.Control);
                }
            }

            _dispatch.Clear();
            return true;
        }

        private void Unsubscribe(Action<TArgs, ExecutionControl> handler)
        {
            _handlers.Remove(handler);
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
