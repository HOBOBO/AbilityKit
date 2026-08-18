using System;
using System.Collections.Generic;
using AbilityKit.Ability.Flow.Pooling;

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
                    if (s.TryGetValue(typeof(T), out var scoped) && Matches<T>(scoped))
                    {
                        value = As<T>(scoped);
                        return true;
                    }
                }
            }

            if (_map.TryGetValue(typeof(T), out var obj) && Matches<T>(obj))
            {
                value = As<T>(obj);
                return true;
            }

            value = default;
            return false;
        }

        private static bool Matches<T>(object stored)
        {
            // 显式存入的 null 对引用类型槽是"存在但为 null"，必须可见（is T 对 null 恒 false）。
            return stored is T || (stored == null && default(T) == null);
        }

        private static T As<T>(object stored)
        {
            return stored == null ? default : (T)stored;
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
            _scopes.Push(FlowPools.RentContextScopeMap());
            return new ScopeHandle(this);
        }

        private void EndScope()
        {
            if (_scopes.Count <= 0) return;
            FlowPools.ReleaseContextScopeMap(_scopes.Pop());
        }

        public void Clear()
        {
            _map.Clear();
            while (_scopes.Count > 0)
            {
                FlowPools.ReleaseContextScopeMap(_scopes.Pop());
            }
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
