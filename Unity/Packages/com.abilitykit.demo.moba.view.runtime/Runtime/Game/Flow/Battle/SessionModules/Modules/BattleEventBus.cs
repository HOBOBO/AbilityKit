using System;
using System.Collections.Generic;

namespace AbilityKit.Game.Flow.Battle.Modules
{
    public sealed class BattleEventBus : IDisposable
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers = new Dictionary<Type, List<Delegate>>();
        private readonly Queue<object> _queue = new Queue<object>(32);

        public IDisposable Subscribe<T>(Action<T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var t = typeof(T);
            if (!_handlers.TryGetValue(t, out var list) || list == null)
            {
                list = new List<Delegate>(4);
                _handlers[t] = list;
            }

            list.Add(handler);
            return new Subscription(() => Unsubscribe(handler));
        }

        private void Unsubscribe<T>(Action<T> handler)
        {
            var t = typeof(T);
            if (!_handlers.TryGetValue(t, out var list) || list == null) return;
            list.Remove(handler);
            if (list.Count == 0) _handlers.Remove(t);
        }

        public void Publish<T>(T evt)
        {
            _queue.Enqueue(evt);
        }

        public void Flush()
        {
            while (_queue.Count > 0)
            {
                var evt = _queue.Dequeue();
                if (evt == null) continue;

                var t = evt.GetType();
                if (!_handlers.TryGetValue(t, out var list) || list == null || list.Count == 0) continue;

                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] is Action<object> o)
                    {
                        o.Invoke(evt);
                    }
                    else
                    {
                        list[i].DynamicInvoke(evt);
                    }
                }
            }
        }

        public void Dispose()
        {
            _handlers.Clear();
            _queue.Clear();
        }

        private sealed class Subscription : IDisposable
        {
            private Action _dispose;

            public Subscription(Action dispose)
            {
                _dispose = dispose;
            }

            public void Dispose()
            {
                var d = _dispose;
                _dispose = null;
                d?.Invoke();
            }
        }
    }

    public sealed class Hook
    {
        private readonly List<Action> _handlers = new List<Action>(4);

        public IDisposable Add(Action handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _handlers.Add(handler);
            return new HookSubscription(() => _handlers.Remove(handler));
        }

        public void Invoke()
        {
            for (int i = 0; i < _handlers.Count; i++)
            {
                _handlers[i]?.Invoke();
            }
        }

        private sealed class HookSubscription : IDisposable
        {
            private Action _dispose;

            public HookSubscription(Action dispose)
            {
                _dispose = dispose;
            }

            public void Dispose()
            {
                var d = _dispose;
                _dispose = null;
                d?.Invoke();
            }
        }
    }

    public sealed class Hook<T>
    {
        private readonly List<Action<T>> _handlers = new List<Action<T>>(4);

        public IDisposable Add(Action<T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _handlers.Add(handler);
            return new HookSubscription(() => _handlers.Remove(handler));
        }

        public void Invoke(T arg)
        {
            for (int i = 0; i < _handlers.Count; i++)
            {
                _handlers[i]?.Invoke(arg);
            }
        }

        private sealed class HookSubscription : IDisposable
        {
            private Action _dispose;

            public HookSubscription(Action dispose)
            {
                _dispose = dispose;
            }

            public void Dispose()
            {
                var d = _dispose;
                _dispose = null;
                d?.Invoke();
            }
        }
    }

    public sealed class Hook<T1, T2>
    {
        private readonly List<Action<T1, T2>> _handlers = new List<Action<T1, T2>>(4);

        public IDisposable Add(Action<T1, T2> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _handlers.Add(handler);
            return new HookSubscription(() => _handlers.Remove(handler));
        }

        public void Invoke(T1 arg1, T2 arg2)
        {
            for (int i = 0; i < _handlers.Count; i++)
            {
                _handlers[i]?.Invoke(arg1, arg2);
            }
        }

        private sealed class HookSubscription : IDisposable
        {
            private Action _dispose;

            public HookSubscription(Action dispose)
            {
                _dispose = dispose;
            }

            public void Dispose()
            {
                var d = _dispose;
                _dispose = null;
                d?.Invoke();
            }
        }
    }

    public sealed class InterceptHook<T>
    {
        private readonly List<Func<T, bool>> _handlers = new List<Func<T, bool>>(4);

        public IDisposable Add(Func<T, bool> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _handlers.Add(handler);
            return new HookSubscription(() => _handlers.Remove(handler));
        }

        public bool Invoke(T arg)
        {
            for (int i = 0; i < _handlers.Count; i++)
            {
                if (_handlers[i]?.Invoke(arg) == true) return true;
            }

            return false;
        }

        private sealed class HookSubscription : IDisposable
        {
            private Action _dispose;

            public HookSubscription(Action dispose)
            {
                _dispose = dispose;
            }

            public void Dispose()
            {
                var d = _dispose;
                _dispose = null;
                d?.Invoke();
            }
        }
    }
}
