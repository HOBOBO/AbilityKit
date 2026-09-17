using System;

namespace AbilityKit.Trace
{
    // Copies of a value-type scope share ownership of its initial retain.
    internal sealed class TraceScopeLease
    {
        private readonly TraceTreeRegistryBase _registry;
        private readonly long _contextId;
        private readonly long _rootId;
        private int _retainedCount = 1;
        private bool _disposed;

        internal TraceScopeLease(TraceTreeRegistryBase registry, long contextId)
        {
            _registry = registry;
            _contextId = contextId;
            _rootId = registry._contexts.TryGetValue(contextId, out var node) ? node.RootId : 0;
        }

        internal bool IsValid => !_disposed && _registry.Contains(_contextId);

        internal void EndChild(int reason)
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                _registry.End(_contextId, reason);
            }
            finally
            {
                Release();
            }
        }

        internal int EndRoot(int reason) => IsValid ? _registry.EndRoot(_rootId, reason) : 0;

        internal void Retain()
        {
            if (!IsValid || _retainedCount == int.MaxValue) return;
            _retainedCount++;
            _registry.RetainRoot(_rootId);
        }

        internal void Release()
        {
            if (_retainedCount == 0) return;
            _retainedCount--;
            _registry.ReleaseRoot(_rootId);
        }

        internal void DisposeRoot()
        {
            if (_disposed) return;
            _disposed = true;
            Release();
        }
    }

    /// <summary>
    /// 溯源树作用域
    /// RAII 模式的 IDisposable，用于自动结束溯源节点并释放其根保留引用
    /// </summary>
    public readonly struct TraceTreeScope : IDisposable
    {
        private readonly TraceScopeLease _lease;
        private readonly long _contextId;
        private readonly int _frame;
        private readonly int _reason;

        /// <summary>
        /// 内部构造函数
        /// </summary>
        internal TraceTreeScope(
            TraceTreeRegistryBase registry,
            long contextId,
            int frame,
            int reason = 0)
        {
            _lease = new TraceScopeLease(registry, contextId);
            _contextId = contextId;
            _frame = frame;
            _reason = reason;
        }

        /// <summary>
        /// 获取上下文 ID
        /// </summary>
        public long ContextId => _contextId;

        /// <summary>
        /// 获取创建时的帧号
        /// </summary>
        public int CreatedFrame => _frame;

        /// <summary>
        /// 是否有效
        /// </summary>
        public bool IsValid => _lease != null && _lease.IsValid;

        /// <summary>
        /// 结束此作用域（手动提前结束）
        /// </summary>
        public void End()
        {
            _lease?.EndChild(_reason);
        }

        /// <summary>
        /// 结束此作用域并设置结束原因
        /// </summary>
        public void End(int reason)
        {
            _lease?.EndChild(reason);
        }

        /// <summary>
        /// 结束此作用域（隐式，用于 using 语句）
        /// </summary>
        public void Dispose()
        {
            _lease?.EndChild(_reason);
        }

        /// <summary>
        /// 转换为字符串表示
        /// </summary>
        public override string ToString()
        {
            if (!IsValid)
                return "TraceTreeScope[Invalid]";
            return $"TraceTreeScope[Id={_contextId}, Frame={_frame}]";
        }
    }

    /// <summary>
    /// 溯源树根作用域
    /// RAII 模式，自动 Retain 和 Release 根节点
    /// </summary>
    public readonly struct TraceRootScope : IDisposable
    {
        private readonly TraceScopeLease _lease;
        private readonly long _rootId;
        private readonly int _frame;

        /// <summary>
        /// 内部构造函数
        /// </summary>
        internal TraceRootScope(
            TraceTreeRegistryBase registry,
            long rootId,
            int frame)
        {
            _lease = new TraceScopeLease(registry, rootId);
            _rootId = rootId;
            _frame = frame;
        }

        /// <summary>
        /// 获取根节点 ID
        /// </summary>
        public long RootId => _rootId;

        /// <summary>
        /// 获取创建时的帧号
        /// </summary>
        public int CreatedFrame => _frame;

        /// <summary>
        /// 是否有效
        /// </summary>
        public bool IsValid => _lease != null && _lease.IsValid;

        /// <summary>
        /// 保留根节点（额外的手动引用，需要与 Release 配对）
        /// </summary>
        public void Retain()
        {
            _lease?.Retain();
        }

        /// <summary>
        /// 释放本作用域持有的一个引用；不会释放其他消费者的引用
        /// </summary>
        public void Release()
        {
            _lease?.Release();
        }

        /// <summary>
        /// 结束根节点及其所有子节点
        /// </summary>
        public int End(int reason = 0)
        {
            return _lease?.EndRoot(reason) ?? 0;
        }

        /// <summary>
        /// 结束此作用域（隐式，用于 using 语句）
        /// 最多释放一个引用；重复 Dispose 和结构体副本共享释放状态
        /// </summary>
        public void Dispose()
        {
            _lease?.DisposeRoot();
        }

        /// <summary>
        /// 转换为字符串表示
        /// </summary>
        public override string ToString()
        {
            if (!IsValid)
                return "TraceRootScope[Invalid]";
            return $"TraceRootScope[RootId={_rootId}, Frame={_frame}]";
        }
    }

    /// <summary>
    /// TraceTreeRegistry&lt;T&gt; 的扩展方法，用于创建作用域
    /// </summary>
    public static class TraceTreeRegistryExtensions
    {
        /// <summary>
        /// 创建根节点作用域
        /// </summary>
        public static TraceRootScope CreateRootScope<T>(
            this TraceTreeRegistry<T> registry,
            int kind,
            long sourceActorId = 0,
            long targetActorId = 0,
            object originSource = null,
            object originTarget = null,
            int configId = 0)
            where T : TraceMetadata
        {
            var rootId = registry.BeginRoot(kind, sourceActorId, targetActorId, originSource, originTarget, configId);
            return new TraceRootScope(registry, rootId, registry.GetCurrentFrame());
        }

        public static TraceRootScope CreateRootScope<T>(
            this TraceTreeRegistry<T> registry,
            in TraceOrigin origin)
            where T : TraceMetadata
        {
            var rootId = registry.BeginRoot(origin);
            return new TraceRootScope(registry, rootId, registry.GetCurrentFrame());
        }

        /// <summary>
        /// 创建子节点作用域
        /// </summary>
        public static TraceTreeScope CreateChildScope<T>(
            this TraceTreeRegistry<T> registry,
            long parentContextId,
            int kind,
            long sourceActorId = 0,
            long targetActorId = 0,
            object originSource = null,
            object originTarget = null,
            int configId = 0)
            where T : TraceMetadata
        {
            var childId = registry.BeginChild(parentContextId, kind, sourceActorId, targetActorId, originSource, originTarget, configId);
            return new TraceTreeScope(registry, childId, registry.GetCurrentFrame(), 0);
        }

        public static TraceTreeScope CreateChildScope<T>(
            this TraceTreeRegistry<T> registry,
            in TraceOrigin origin)
            where T : TraceMetadata
        {
            var childId = registry.BeginChild(origin);
            return new TraceTreeScope(registry, childId, registry.GetCurrentFrame(), 0);
        }

        /// <summary>
        /// 创建子节点作用域（带结束原因）
        /// </summary>
        public static TraceTreeScope CreateChildScope<T>(
            this TraceTreeRegistry<T> registry,
            long parentContextId,
            int kind,
            int endReason,
            long sourceActorId = 0,
            long targetActorId = 0,
            object originSource = null,
            object originTarget = null,
            int configId = 0)
            where T : TraceMetadata
        {
            var childId = registry.BeginChild(parentContextId, kind, sourceActorId, targetActorId, originSource, originTarget, configId);
            return new TraceTreeScope(registry, childId, registry.GetCurrentFrame(), endReason);
        }

        public static TraceTreeScope CreateChildScope<T>(
            this TraceTreeRegistry<T> registry,
            in TraceOrigin origin,
            int endReason)
            where T : TraceMetadata
        {
            var childId = registry.BeginChild(origin);
            return new TraceTreeScope(registry, childId, registry.GetCurrentFrame(), endReason);
        }
    }
}
