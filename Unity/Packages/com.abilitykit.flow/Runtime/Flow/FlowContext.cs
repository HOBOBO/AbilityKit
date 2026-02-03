using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Flow
{
    public sealed class FlowContext
    {
        private readonly Dictionary<Type, object> _map = new Dictionary<Type, object>();

        private readonly Stack<Dictionary<Type, object>> _scopes = new Stack<Dictionary<Type, object>>();

        public void Set<T>(T value)
        {
            if (_scopes.Count > 0)
            {
                _scopes.Peek()[typeof(T)] = value;
                return;
            }

            _map[typeof(T)] = value;
        }

        public bool TryGet<T>(out T value)
        {
            if (_scopes.Count > 0)
            {
                foreach (var s in _scopes)
                {
                    if (s.TryGetValue(typeof(T), out var scoped) && scoped is T scopedTyped)
                    {
                        value = scopedTyped;
                        return true;
                    }
                }
            }

            if (_map.TryGetValue(typeof(T), out var obj) && obj is T typed)
            {
                value = typed;
                return true;
            }

            value = default;
            return false;
        }

        public T Get<T>()
        {
            if (TryGet<T>(out var v)) return v;
            throw new InvalidOperationException($"FlowContext missing value: {typeof(T).FullName}");
        }

        public void Remove<T>()
        {
            if (_scopes.Count > 0)
            {
                _scopes.Peek().Remove(typeof(T));
                return;
            }

            _map.Remove(typeof(T));
        }

        public IDisposable BeginScope()
        {
            _scopes.Push(new Dictionary<Type, object>());
            return new ScopeHandle(this);
        }

        private void EndScope()
        {
            if (_scopes.Count <= 0) return;
            _scopes.Pop();
        }

        public void Clear()
        {
            _map.Clear();
            _scopes.Clear();
        }

        private sealed class ScopeHandle : IDisposable
        {
            private FlowContext _ctx;

            public ScopeHandle(FlowContext ctx)
            {
                _ctx = ctx;
            }

            public void Dispose()
            {
                var c = _ctx;
                if (c == null) return;
                _ctx = null;
                c.EndScope();
            }
        }
    }
}
