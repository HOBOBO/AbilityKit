using System;
using System.Collections.Generic;
using AbilityKit.Ability.Flow.Pooling;

namespace AbilityKit.Ability.Flow
{
    /// <summary>
    /// 按类型保存流程数据，局部作用域优先于根数据；由宿主串行访问。
    /// </summary>
    public sealed class FlowContext
    {
        private readonly Dictionary<Type, object> _map = new Dictionary<Type, object>();

        private readonly Stack<Dictionary<Type, object>> _scopes = new Stack<Dictionary<Type, object>>();
        private ScopeBoundary _boundary;

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

        /// <summary>
        /// 创建局部槽位，调用方须按后进先出顺序释放，不得跨越正在运行的流程边界。
        /// </summary>
        public IDisposable BeginScope()
        {
            _boundary ??= new ScopeBoundary(this, null, 0);
            _scopes.Push(FlowPools.RentContextScopeMap());
            return new ScopeHandle(_boundary);
        }

        internal IDisposable BeginRunScope()
        {
            _scopes.Push(FlowPools.RentContextScopeMap());
            _boundary = new ScopeBoundary(this, _boundary, _scopes.Count);
            return _boundary;
        }

        private void EndScope(ScopeBoundary boundary)
        {
            if (!ReferenceEquals(_boundary, boundary))
            {
                throw new InvalidOperationException("A context scope cannot close across an active flow run.");
            }

            if (_scopes.Count > boundary.Depth)
            {
                FlowPools.ReleaseContextScopeMap(_scopes.Pop());
            }
        }

        private void EndRunScope(ScopeBoundary boundary)
        {
            while (_scopes.Count >= boundary.Depth)
            {
                FlowPools.ReleaseContextScopeMap(_scopes.Pop());
            }

            CloseBoundariesTo(boundary.Parent);
        }

        private void CloseBoundariesTo(ScopeBoundary parent)
        {
            while (!ReferenceEquals(_boundary, parent))
            {
                var boundary = _boundary;
                _boundary = boundary.Parent;
                boundary.Invalidate();
            }
        }

        /// <summary>
        /// 清空根数据和作用域，并使旧句柄失效；不释放槽位中的业务对象。
        /// </summary>
        public void Clear()
        {
            _map.Clear();
            while (_scopes.Count > 0)
            {
                FlowPools.ReleaseContextScopeMap(_scopes.Pop());
            }

            CloseBoundariesTo(null);
        }

        /// <summary>
        /// 标记一次运行的根槽位深度，也用于使该次运行的所有局部作用域句柄失效。
        /// </summary>
        private sealed class ScopeBoundary : IDisposable
        {
            internal ScopeBoundary(FlowContext context, ScopeBoundary parent, int depth)
            {
                Context = context;
                Parent = parent;
                Depth = depth;
            }

            internal FlowContext Context { get; private set; }
            internal ScopeBoundary Parent { get; private set; }
            internal int Depth { get; }

            internal void Invalidate()
            {
                Context = null;
                Parent = null;
            }

            /// <summary>
            /// 清理运行根槽位和仍未释放的内部槽位。
            /// </summary>
            public void Dispose() => Context?.EndRunScope(this);
        }

        /// <summary>
        /// 释放所属边界的最内层普通槽位；已关闭边界的句柄不再访问上下文。
        /// </summary>
        private sealed class ScopeHandle : IDisposable
        {
            private ScopeBoundary _owner;

            internal ScopeHandle(ScopeBoundary owner)
            {
                _owner = owner;
            }

            /// <summary>
            /// 释放局部槽位；闭合后重复调用无效。
            /// </summary>
            public void Dispose()
            {
                _owner?.Context?.EndScope(_owner);
                _owner = null;
            }
        }
    }
}
