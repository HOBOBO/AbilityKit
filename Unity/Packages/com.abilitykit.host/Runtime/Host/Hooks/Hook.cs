using System;
using AbilityKit.Core.Collections;

namespace AbilityKit.Ability.Host.Hooks
{
    public sealed class Hook
    {
        private readonly StablePriorityList<Action> _handlers = new StablePriorityList<Action>(capacity: 8);

        public void Add(Action handler, int order = 0)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _handlers.Add(handler, order);
        }

        public bool Remove(Action handler)
        {
            if (handler == null) return false;
            return _handlers.RemoveFirst(item => ReferenceEquals(item, handler));
        }

        public void Clear()
        {
            _handlers.Clear();
        }

        public void Invoke()
        {
            for (int i = 0; i < _handlers.Count; i++)
            {
                _handlers[i]?.Invoke();
            }
        }
    }

    public sealed class Hook<T>
    {
        private readonly StablePriorityList<Action<T>> _handlers = new StablePriorityList<Action<T>>(capacity: 8);

        public void Add(Action<T> handler, int order = 0)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _handlers.Add(handler, order);
        }

        public bool Remove(Action<T> handler)
        {
            if (handler == null) return false;
            return _handlers.RemoveFirst(item => ReferenceEquals(item, handler));
        }

        public void Clear()
        {
            _handlers.Clear();
        }

        public void Invoke(T arg)
        {
            for (int i = 0; i < _handlers.Count; i++)
            {
                _handlers[i]?.Invoke(arg);
            }
        }
    }

    public sealed class Hook<T1, T2>
    {
        private readonly StablePriorityList<Action<T1, T2>> _handlers = new StablePriorityList<Action<T1, T2>>(capacity: 8);

        public void Add(Action<T1, T2> handler, int order = 0)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _handlers.Add(handler, order);
        }

        public bool Remove(Action<T1, T2> handler)
        {
            if (handler == null) return false;
            return _handlers.RemoveFirst(item => ReferenceEquals(item, handler));
        }

        public void Clear()
        {
            _handlers.Clear();
        }

        public void Invoke(T1 a1, T2 a2)
        {
            for (int i = 0; i < _handlers.Count; i++)
            {
                _handlers[i]?.Invoke(a1, a2);
            }
        }
    }

    public sealed class Hook<T1, T2, T3>
    {
        private readonly StablePriorityList<Action<T1, T2, T3>> _handlers = new StablePriorityList<Action<T1, T2, T3>>(capacity: 8);

        public void Add(Action<T1, T2, T3> handler, int order = 0)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _handlers.Add(handler, order);
        }

        public bool Remove(Action<T1, T2, T3> handler)
        {
            if (handler == null) return false;
            return _handlers.RemoveFirst(item => ReferenceEquals(item, handler));
        }

        public void Clear()
        {
            _handlers.Clear();
        }

        public void Invoke(T1 a1, T2 a2, T3 a3)
        {
            for (int i = 0; i < _handlers.Count; i++)
            {
                _handlers[i]?.Invoke(a1, a2, a3);
            }
        }
    }
}
