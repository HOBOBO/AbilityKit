using System;
using System.Collections.Generic;

namespace AbilityKit.Trace
{
    /// <summary>
    /// 溯源树节点记录（内部使用）
    /// </summary>
    internal readonly struct TraceContextRecord
    {
        public readonly long ContextId;
        public readonly long RootId;
        public readonly long ParentId;
        public readonly int Kind;
        public readonly int ConfigId;
        public readonly long SourceActorId;
        public readonly long TargetActorId;
        public readonly long OriginSourceId;
        public readonly string OriginSourceDisplay;
        public readonly long OriginTargetId;
        public readonly string OriginTargetDisplay;
        public readonly int CreatedFrame;
        public readonly int EndedFrame;
        public readonly int EndReason;

        public TraceContextRecord(
            long contextId,
            long rootId,
            long parentId,
            int kind,
            int configId,
            long sourceActorId,
            long targetActorId,
            long originSourceId,
            string originSourceDisplay,
            long originTargetId,
            string originTargetDisplay,
            int createdFrame,
            int endedFrame,
            int endReason)
        {
            ContextId = contextId;
            RootId = rootId;
            ParentId = parentId;
            Kind = kind;
            ConfigId = configId;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            OriginSourceId = originSourceId;
            OriginSourceDisplay = originSourceDisplay;
            OriginTargetId = originTargetId;
            OriginTargetDisplay = originTargetDisplay;
            CreatedFrame = createdFrame;
            EndedFrame = endedFrame;
            EndReason = endReason;
        }

        public bool IsEnded => EndedFrame != 0;
    }

    /// <summary>
    /// 溯源根记录（内部使用）
    /// </summary>
    internal readonly struct TraceRootRecord
    {
        public readonly int ActiveCount;
        public readonly int ExternalRefCount;
        public readonly int LastTouchedFrame;

        public TraceRootRecord(int activeCount, int externalRefCount, int lastTouchedFrame)
        {
            ActiveCount = activeCount;
            ExternalRefCount = externalRefCount;
            LastTouchedFrame = lastTouchedFrame;
        }

        public TraceRootRecord WithActiveCount(int count) =>
            new TraceRootRecord(count, ExternalRefCount, LastTouchedFrame);

        public TraceRootRecord WithExternalRefCount(int count) =>
            new TraceRootRecord(ActiveCount, count, LastTouchedFrame);

        public TraceRootRecord WithLastTouchedFrame(int frame) =>
            new TraceRootRecord(ActiveCount, ExternalRefCount, frame);
    }

    /// <summary>
    /// 溯源树注册表
    /// 负责构建和维护溯源树结构，提供节点创建、销毁、查询能力
    /// 线程不安全，请在主线程使用
    /// </summary>
    public sealed partial class TraceTreeRegistry : IDisposable
    {
        private readonly Dictionary<long, TraceContextRecord> _contexts;
        private readonly Dictionary<long, TraceRootRecord> _roots;
        private readonly Dictionary<long, List<long>> _childrenByParent;
        private readonly ITraceMetadataStore _metadataStore;
        private readonly ITraceContextSource _contextSource;
        private long _nextId;

        /// <summary>
        /// 创建溯源树注册表
        /// </summary>
        /// <param name="metadataStore">元数据存储，默认使用空实现</param>
        /// <param name="contextSource">上下文来源，默认使用简单实现</param>
        public TraceTreeRegistry(
            ITraceMetadataStore metadataStore = null,
            ITraceContextSource contextSource = null)
        {
            _contexts = new Dictionary<long, TraceContextRecord>();
            _roots = new Dictionary<long, TraceRootRecord>();
            _childrenByParent = new Dictionary<long, List<long>>();
            _metadataStore = metadataStore ?? NullTraceMetadataStore.Instance;
            _contextSource = contextSource ?? SimpleTraceContextSource.Instance;
            _nextId = 1;
        }

        /// <summary>
        /// 获取当前帧号（子类可覆盖）
        /// </summary>
        protected virtual int Frame => 0;

        /// <summary>
        /// 生成新的唯一 ID
        /// </summary>
        protected long NewId()
        {
            return _nextId++;
        }

        /// <summary>
        /// 获取元数据存储
        /// </summary>
        public ITraceMetadataStore MetadataStore => _metadataStore;

        /// <summary>
        /// 获取上下文来源
        /// </summary>
        public ITraceContextSource ContextSource => _contextSource;

        /// <summary>
        /// 获取根节点数量
        /// </summary>
        public int RootCount => _roots.Count;

        /// <summary>
        /// 获取总节点数量
        /// </summary>
        public int TotalNodeCount => _contexts.Count;

        /// <summary>
        /// 获取根节点 ID 列表
        /// </summary>
        public IEnumerable<long> RootIds => _roots.Keys;

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Clear();
        }
    }
}
