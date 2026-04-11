using System;

namespace AbilityKit.Trace
{
    /// <summary>
    /// 溯源树作用域
    /// RAII 模式的 IDisposable，用于自动结束溯源节点
    /// </summary>
    public readonly struct TraceTreeScope : IDisposable
    {
        private readonly TraceTreeRegistryBase _registry;
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
            _registry = registry;
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
        public bool IsValid => _registry != null && _contextId != 0;

        /// <summary>
        /// 结束此作用域（手动提前结束）
        /// </summary>
        public void End()
        {
            if (IsValid)
            {
                _registry.End(_contextId, _reason);
            }
        }

        /// <summary>
        /// 结束此作用域并设置结束原因
        /// </summary>
        public void End(int reason)
        {
            if (IsValid)
            {
                _registry.End(_contextId, reason);
            }
        }

        /// <summary>
        /// 结束此作用域（隐式，用于 using 语句）
        /// </summary>
        public void Dispose()
        {
            if (IsValid)
            {
                _registry.End(_contextId, _reason);
            }
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
        private readonly TraceTreeRegistryBase _registry;
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
            _registry = registry;
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
        public bool IsValid => _registry != null && _rootId != 0;

        /// <summary>
        /// 保留根节点（增加外部引用计数）
        /// </summary>
        public void Retain()
        {
            if (IsValid)
            {
                _registry.RetainRoot(_rootId);
            }
        }

        /// <summary>
        /// 释放根节点（减少外部引用计数）
        /// </summary>
        public void Release()
        {
            if (IsValid)
            {
                _registry.ReleaseRoot(_rootId);
            }
        }

        /// <summary>
        /// 结束根节点及其所有子节点
        /// </summary>
        public int End(int reason = 0)
        {
            if (!IsValid)
                return 0;
            return _registry.EndRoot(_rootId, reason);
        }

        /// <summary>
        /// 结束此作用域（隐式，用于 using 语句）
        /// 会 Release 外部引用计数
        /// </summary>
        public void Dispose()
        {
            if (IsValid)
            {
                _registry.ReleaseRoot(_rootId);
            }
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
    }
}
