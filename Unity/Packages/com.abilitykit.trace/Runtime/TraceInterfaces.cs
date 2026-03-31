using System;
using System.Collections.Generic;

namespace AbilityKit.Trace
{
    // ============================================================================
    // 枚举
    // ============================================================================

    /// <summary>
    /// 溯源节点种类枚举
    /// 具体值由外部扩展，业务层可以定义自己的枚举值
    /// </summary>
    public enum TraceNodeKind : byte
    {
        None = 0,
    }

    /// <summary>
    /// 溯源结束原因枚举
    /// 具体值由外部扩展，业务层可以定义自己的枚举值
    /// </summary>
    public enum TraceEndReason : byte
    {
        None = 0,
    }

    // ============================================================================
    // ITraceContextSource
    // ============================================================================

    /// <summary>
    /// 溯源上下文来源接口
    /// 允许不同玩法自定义"来源"如何提取和存储
    /// </summary>
    public interface ITraceContextSource
    {
        bool TryExtractTraceId(object origin, out long traceableId, out string displayName);
    }

    /// <summary>
    /// 默认的空溯源上下文来源
    /// </summary>
    public sealed class DefaultTraceContextSource : ITraceContextSource
    {
        public static readonly DefaultTraceContextSource Instance = new DefaultTraceContextSource();

        public bool TryExtractTraceId(object origin, out long traceableId, out string displayName)
        {
            traceableId = 0;
            displayName = null;
            return false;
        }

        private DefaultTraceContextSource() { }
    }

    /// <summary>
    /// 简单溯源上下文来源
    /// 支持 int/long/Guid 直接转换
    /// </summary>
    public sealed class SimpleTraceContextSource : ITraceContextSource
    {
        public static readonly SimpleTraceContextSource Instance = new SimpleTraceContextSource();

        public bool TryExtractTraceId(object origin, out long traceableId, out string displayName)
        {
            if (origin is long l)
            {
                traceableId = l;
                displayName = l.ToString();
                return true;
            }
            if (origin is int i)
            {
                traceableId = i;
                displayName = i.ToString();
                return true;
            }
            if (origin is ulong ul)
            {
                traceableId = (long)ul;
                displayName = ul.ToString();
                return true;
            }
            if (origin is uint ui)
            {
                traceableId = ui;
                displayName = ui.ToString();
                return true;
            }
            if (origin is Guid g)
            {
                traceableId = g.GetHashCode();
                displayName = g.ToString();
                return true;
            }
            traceableId = 0;
            displayName = origin?.ToString();
            return false;
        }

        private SimpleTraceContextSource() { }
    }

    // ============================================================================
    // TraceMetadata（抽象基类，业务层继承）
    // ============================================================================

    /// <summary>
    /// 溯源元数据抽象基类
    /// 业务层继承此类，添加任意字段（如 SkillId, Damage, BuffLevel 等）
    /// 每个根节点对应一个 TMetadata 实例，由 ITraceMetadataStore 统一管理生命周期
    /// </summary>
    public abstract class TraceMetadata { }

    // ============================================================================
    // ITraceMetadataStore<T>
    // ============================================================================

    /// <summary>
    /// 溯源元数据存储接口（泛型）
    /// 负责按 rootId 存取 TraceMetadata 实例
    /// 业务层可注入自己的实现（如接入数据库、对象池等）
    /// </summary>
    public interface ITraceMetadataStore<T>
        where T : TraceMetadata
    {
        void SetMetadata(long rootId, T metadata);
        bool TryGetMetadata(long rootId, out T metadata);
        void Clear(long rootId);
    }

    /// <summary>
    /// 字典实现的元数据存储
    /// </summary>
    public sealed class DictionaryTraceMetadataStore<T> : ITraceMetadataStore<T>
        where T : TraceMetadata
    {
        private readonly Dictionary<long, T> _metadata = new();

        public void SetMetadata(long rootId, T metadata) => _metadata[rootId] = metadata;
        public bool TryGetMetadata(long rootId, out T metadata) => _metadata.TryGetValue(rootId, out metadata);
        public void Clear(long rootId) => _metadata.Remove(rootId);
        public IEnumerable<long> GetAllRootIds() => _metadata.Keys;
    }

    /// <summary>
    /// 空元数据存储
    /// </summary>
    public sealed class NullTraceMetadataStore<T> : ITraceMetadataStore<T>
        where T : TraceMetadata
    {
        public static readonly NullTraceMetadataStore<T> Instance = new NullTraceMetadataStore<T>();

        public void SetMetadata(long rootId, T metadata) { }
        public bool TryGetMetadata(long rootId, out T metadata) { metadata = null; return false; }
        public void Clear(long rootId) { }

        private NullTraceMetadataStore() { }
    }

    // ============================================================================
    // RootState / RootStats
    // ============================================================================

    /// <summary>
    /// 根节点状态快照
    /// </summary>
    public readonly struct RootState
    {
        public readonly long RootId;
        public readonly int ActiveCount;
        public readonly int ExternalRefCount;
        public readonly int LastTouchedFrame;

        public RootState(long rootId, int activeCount, int externalRefCount, int lastTouchedFrame)
        {
            RootId = rootId;
            ActiveCount = activeCount;
            ExternalRefCount = externalRefCount;
            LastTouchedFrame = lastTouchedFrame;
        }
    }

    /// <summary>
    /// 根节点统计信息
    /// </summary>
    public readonly struct RootStats
    {
        public readonly long RootId;
        public readonly int TotalNodes;
        public readonly int ActiveNodes;
        public readonly int EndedNodes;
        public readonly int MaxDepth;

        public RootStats(long rootId, int totalNodes, int activeNodes, int endedNodes, int maxDepth)
        {
            RootId = rootId;
            TotalNodes = totalNodes;
            ActiveNodes = activeNodes;
            EndedNodes = endedNodes;
            MaxDepth = maxDepth;
        }
    }

    // ============================================================================
    // ITraceLeafDataStore
    // ============================================================================

    /// <summary>
    /// 叶子节点附加数据存储接口
    /// 用于为最终叶节点挂接额外的快照数据（例如数值快照、调试信息等）
    /// </summary>
    public interface ITraceLeafDataStore
    {
        void Set(long contextId, object data);
        bool TryGet(long contextId, out object data);
        void Clear(long contextId);
    }

    /// <summary>
    /// 字典实现的叶子节点数据存储
    /// </summary>
    public sealed class DictionaryTraceLeafDataStore : ITraceLeafDataStore
    {
        private readonly Dictionary<long, object> _data = new();

        public void Set(long contextId, object data)
        {
            if (data == null) { _data.Remove(contextId); return; }
            _data[contextId] = data;
        }

        public bool TryGet(long contextId, out object data)
            => _data.TryGetValue(contextId, out data);

        public void Clear(long contextId) => _data.Remove(contextId);
    }

    /// <summary>
    /// 空叶子节点数据存储
    /// </summary>
    public sealed class NullTraceLeafDataStore : ITraceLeafDataStore
    {
        public static readonly NullTraceLeafDataStore Instance = new NullTraceLeafDataStore();

        public void Set(long contextId, object data) { }
        public bool TryGet(long contextId, out object data) { data = null; return false; }
        public void Clear(long contextId) { }

        private NullTraceLeafDataStore() { }
    }
}
