using System;
using System.Collections.Generic;

namespace AbilityKit.Trace
{
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

    /// <summary>
    /// 溯源上下文来源接口
    /// 允许不同玩法自定义"来源"如何提取和存储
    /// 默认实现将所有对象转为字符串描述
    /// </summary>
    public interface ITraceContextSource
    {
        /// <summary>
        /// 从给定的原始来源对象中提取可追溯的标识
        /// </summary>
        /// <param name="origin">原始来源对象</param>
        /// <param name="traceableId">输出：溯源用的唯一标识</param>
        /// <param name="displayName">输出：调试用显示名称</param>
        /// <returns>是否成功提取</returns>
        bool TryExtractTraceId(object origin, out long traceableId, out string displayName);
    }

    /// <summary>
    /// 默认的空溯源上下文来源
    /// 所有对象都返回 false，由调用方自行处理
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
    /// 支持 int/long 直接转换，以及 DateTime.Ticks 作为唯一标识
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

    /// <summary>
    /// 溯源元数据存储接口
    /// 允许不同玩法自定义根节点上存储的元数据类型
    /// </summary>
    public interface ITraceMetadataStore
    {
        /// <summary>
        /// 设置整数元数据
        /// </summary>
        void SetInt(long rootId, string key, int value);

        /// <summary>
        /// 获取整数元数据
        /// </summary>
        bool TryGetInt(long rootId, string key, out int value);

        /// <summary>
        /// 获取根节点的所有元数据快照
        /// </summary>
        IReadOnlyDictionary<string, int> GetAllMetadata(long rootId);

        /// <summary>
        /// 清除根节点的所有元数据
        /// </summary>
        void Clear(long rootId);
    }

    /// <summary>
    /// 简单的字典元数据存储
    /// 使用 Dictionary&lt;long, Dictionary&lt;string, int&gt;&gt; 实现
    /// </summary>
    public sealed class DictionaryTraceMetadataStore : ITraceMetadataStore
    {
        private readonly Dictionary<long, Dictionary<string, int>> _metadata = new();
        private readonly Dictionary<string, int> _empty = new(0);

        public void SetInt(long rootId, string key, int value)
        {
            if (!_metadata.TryGetValue(rootId, out var dict))
            {
                dict = new Dictionary<string, int>();
                _metadata[rootId] = dict;
            }
            dict[key] = value;
        }

        public bool TryGetInt(long rootId, string key, out int value)
        {
            if (_metadata.TryGetValue(rootId, out var dict) && dict.TryGetValue(key, out value))
                return true;
            value = 0;
            return false;
        }

        public IReadOnlyDictionary<string, int> GetAllMetadata(long rootId)
        {
            return _metadata.TryGetValue(rootId, out var dict) ? dict : _empty;
        }

        public void Clear(long rootId)
        {
            _metadata.Remove(rootId);
        }

        /// <summary>
        /// 获取所有根ID
        /// </summary>
        public IEnumerable<long> GetAllRootIds()
        {
            return _metadata.Keys;
        }
    }

    /// <summary>
    /// 空元数据存储
    /// 用于不需要元数据存储的场景
    /// </summary>
    public sealed class NullTraceMetadataStore : ITraceMetadataStore
    {
        public static readonly NullTraceMetadataStore Instance = new NullTraceMetadataStore();

        public void SetInt(long rootId, string key, int value) { }

        public bool TryGetInt(long rootId, string key, out int value)
        {
            value = 0;
            return false;
        }

        public IReadOnlyDictionary<string, int> GetAllMetadata(long rootId)
        {
            return EmptyDict;
        }

        public void Clear(long rootId) { }

        private static readonly Dictionary<string, int> EmptyDict = new Dictionary<string, int>(0);
        private NullTraceMetadataStore() { }
    }

    /// <summary>
    /// 溯源快照接口
    /// </summary>
    public interface ITraceSnapshot
    {
        long ContextId { get; }
        long RootId { get; }
        long ParentId { get; }
        int Kind { get; }
        int EndReason { get; }
        long SourceActorId { get; }
        long TargetActorId { get; }
        long OriginSourceId { get; }
        string OriginSourceDisplay { get; }
        long OriginTargetId { get; }
        string OriginTargetDisplay { get; }
        int CreatedFrame { get; }
        int EndedFrame { get; }
        bool IsEnded { get; }
    }

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
}
