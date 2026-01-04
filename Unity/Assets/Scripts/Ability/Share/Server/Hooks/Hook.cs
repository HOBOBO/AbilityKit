using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Server.Hooks
{
    public sealed class Hook
    {
        private readonly List<(int order, Action handler)> _handlers = new List<(int, Action)>(8);

        public void Add(Action handler, int order = 0)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _handlers.Add((order, handler));
            _handlers.Sort((a, b) => a.order.CompareTo(b.order));
        }

        public bool Remove(Action handler)
        {
            if (handler == null) return false;
            for (int i = 0; i < _handlers.Count; i++)
            {
                if (ReferenceEquals(_handlers[i].handler, handler))
                {
                    _handlers.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public void Clear()
        {
            _handlers.Clear();
        }

        public void Invoke()
        {
            for (int i = 0; i < _handlers.Count; i++)
            {
                _handlers[i].handler?.Invoke();
            }
        }
    }

    public sealed class Hook<T>
    {
        private readonly List<(int order, Action<T> handler)> _handlers = new List<(int, Action<T>)>(8);

        public void Add(Action<T> handler, int order = 0)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _handlers.Add((order, handler));
            _handlers.Sort((a, b) => a.order.CompareTo(b.order));
        }

        public bool Remove(Action<T> handler)
        {
            if (handler == null) return false;
            for (int i = 0; i < _handlers.Count; i++)
            {
                if (ReferenceEquals(_handlers[i].handler, handler))
                {
                    _handlers.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public void Clear()
        {
            _handlers.Clear();
        }

        public void Invoke(T arg)
        {
            for (int i = 0; i < _handlers.Count; i++)
            {
                _handlers[i].handler?.Invoke(arg);
            }
        }
    }

    public sealed class Hook<T1, T2>
    {
        private readonly List<(int order, Action<T1, T2> handler)> _handlers = new List<(int, Action<T1, T2>)>(8);

        public void Add(Action<T1, T2> handler, int order = 0)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _handlers.Add((order, handler));
            _handlers.Sort((a, b) => a.order.CompareTo(b.order));
        }

        public bool Remove(Action<T1, T2> handler)
        {
            if (handler == null) return false;
            for (int i = 0; i < _handlers.Count; i++)
            {
                if (ReferenceEquals(_handlers[i].handler, handler))
                {
                    _handlers.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public void Clear()
        {
            _handlers.Clear();
        }

        public void Invoke(T1 a1, T2 a2)
        {
            for (int i = 0; i < _handlers.Count; i++)
            {
                _handlers[i].handler?.Invoke(a1, a2);
            }
        }
    }

    public sealed class Hook<T1, T2, T3>
    {
        private readonly List<(int order, Action<T1, T2, T3> handler)> _handlers = new List<(int, Action<T1, T2, T3>)>(8);

        public void Add(Action<T1, T2, T3> handler, int order = 0)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _handlers.Add((order, handler));
            _handlers.Sort((a, b) => a.order.CompareTo(b.order));
        }

        public bool Remove(Action<T1, T2, T3> handler)
        {
            if (handler == null) return false;
            for (int i = 0; i < _handlers.Count; i++)
            {
                if (ReferenceEquals(_handlers[i].handler, handler))
                {
                    _handlers.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public void Clear()
        {
            _handlers.Clear();
        }

        public void Invoke(T1 a1, T2 a2, T3 a3)
        {
            for (int i = 0; i < _handlers.Count; i++)
            {
                _handlers[i].handler?.Invoke(a1, a2, a3);
            }
        }
    }
}
