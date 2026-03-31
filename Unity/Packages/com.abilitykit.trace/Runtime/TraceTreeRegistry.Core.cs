using System;
using System.Collections.Generic;

namespace AbilityKit.Trace
{
    /// <summary>
    /// 溯源树节点记录（内部使用）
    /// 仅包含框架核心必需字段，玩法相关数据通过 ITraceMetadataStore 存取
    /// </summary>
    internal readonly struct TraceContextRecord
    {
        public readonly long ContextId;
        public readonly long RootId;
        public readonly long ParentId;
        public readonly int Kind;
        public readonly int EndedFrame;
        public readonly int EndReason;

        public TraceContextRecord(
            long contextId,
            long rootId,
            long parentId,
            int kind,
            int endedFrame,
            int endReason)
        {
            ContextId = contextId;
            RootId = rootId;
            ParentId = parentId;
            Kind = kind;
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
    /// 溯源树注册表基类（非泛型，供 TraceTreeScope 等内部类型使用）
    /// </summary>
    public abstract class TraceTreeRegistryBase : IDisposable
    {
        internal readonly Dictionary<long, TraceContextRecord> _contexts;
        internal readonly Dictionary<long, TraceRootRecord> _roots;
        internal readonly Dictionary<long, List<long>> _childrenByParent;
        internal readonly ITraceLeafDataStore _leafDataStore;
        internal readonly ITraceContextSource _contextSource;
        internal long _nextId;

        protected TraceTreeRegistryBase(
            ITraceContextSource contextSource,
            ITraceLeafDataStore leafDataStore)
        {
            _contexts = new Dictionary<long, TraceContextRecord>();
            _roots = new Dictionary<long, TraceRootRecord>();
            _childrenByParent = new Dictionary<long, List<long>>();
            _contextSource = contextSource ?? SimpleTraceContextSource.Instance;
            _leafDataStore = leafDataStore ?? NullTraceLeafDataStore.Instance;
            _nextId = 1;
        }

        /// <summary>
        /// 获取当前帧号（子类可覆盖）
        /// </summary>
        protected virtual int Frame => 0;

        /// <summary>
        /// 生成新的唯一 ID
        /// </summary>
        protected long NewId() => _nextId++;

        /// <summary>
        /// 获取叶子节点数据存储
        /// </summary>
        public ITraceLeafDataStore LeafDataStore => _leafDataStore;

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
        public void Dispose() => Clear();

        /// <summary>
        /// 清理所有数据
        /// </summary>
        public void Clear()
        {
            _contexts.Clear();
            _roots.Clear();
            _childrenByParent.Clear();
            _nextId = 1;
            OnClear();
        }

        /// <summary>
        /// 清理时触发（子类可覆盖）
        /// </summary>
        protected virtual void OnClear() { }
    }

    /// <summary>
    /// 溯源树注册表（泛型）
    /// T 是业务层定义的 TraceMetadata 子类
    /// 子类必须实现 CreateMetadata 方法返回自己的元数据类型
    /// </summary>
    public abstract class TraceTreeRegistry<T> : TraceTreeRegistryBase
        where T : TraceMetadata
    {
        private readonly ITraceMetadataStore<T> _metadataStore;

        protected TraceTreeRegistry(
            ITraceMetadataStore<T> metadataStore,
            ITraceContextSource contextSource = null,
            ITraceLeafDataStore leafDataStore = null)
            : base(contextSource, leafDataStore)
        {
            _metadataStore = metadataStore ?? NullTraceMetadataStore<T>.Instance;
        }

        /// <summary>
        /// 获取元数据存储
        /// </summary>
        public ITraceMetadataStore<T> MetadataStore => _metadataStore;

        /// <summary>
        /// 创建根节点
        /// </summary>
        public long CreateRoot(
            int kind,
            long sourceActorId = 0,
            long targetActorId = 0,
            object originSource = null,
            object originTarget = null,
            int configId = 0)
        {
            var contextId = NewId();
            var (originId, originDisplay) = ExtractOrigin(originSource);
            var (targetId, targetDisplay) = ExtractOrigin(originTarget);

            var record = new TraceContextRecord(
                contextId: contextId,
                rootId: contextId,
                parentId: 0,
                kind: kind,
                endedFrame: 0,
                endReason: 0);

            _contexts[contextId] = record;
            _roots[contextId] = new TraceRootRecord(1, 0, Frame);
            _childrenByParent[contextId] = new List<long>();

            var metadata = CreateMetadata(
                contextId, kind, sourceActorId, targetActorId,
                originId, originDisplay, targetId, targetDisplay, configId);
            _metadataStore.SetMetadata(contextId, metadata);

            return contextId;
        }

        /// <summary>
        /// 创建子节点
        /// </summary>
        public long CreateChild(
            long parentContextId,
            int kind,
            long sourceActorId = 0,
            long targetActorId = 0,
            object originSource = null,
            object originTarget = null,
            int configId = 0)
        {
            if (!_contexts.TryGetValue(parentContextId, out var parentRecord))
                throw new ArgumentException($"Parent context {parentContextId} not found", nameof(parentContextId));

            var contextId = NewId();
            var rootId = parentRecord.RootId;

            long originId; string originDisplay;
            if (originSource != null)
                (originId, originDisplay) = ExtractOrigin(originSource);
            else if (_metadataStore.TryGetMetadata(rootId, out var m))
                (originId, originDisplay) = (GetOriginSourceId(m), GetOriginSourceDisplay(m));
            else
                (originId, originDisplay) = (0, null);

            long targetId; string targetDisplay;
            if (originTarget != null)
                (targetId, targetDisplay) = ExtractOrigin(originTarget);
            else if (_metadataStore.TryGetMetadata(rootId, out var m2))
                (targetId, targetDisplay) = (GetOriginTargetId(m2), GetOriginTargetDisplay(m2));
            else
                (targetId, targetDisplay) = (0, null);

            if (sourceActorId == 0 && _metadataStore.TryGetMetadata(rootId, out var m3))
                sourceActorId = GetSourceActorId(m3);
            if (targetActorId == 0 && _metadataStore.TryGetMetadata(rootId, out var m4))
                targetActorId = GetTargetActorId(m4);

            var record = new TraceContextRecord(
                contextId: contextId,
                rootId: rootId,
                parentId: parentContextId,
                kind: kind,
                endedFrame: 0,
                endReason: 0);

            _contexts[contextId] = record;

            if (!_childrenByParent.TryGetValue(parentContextId, out var children))
            {
                children = new List<long>();
                _childrenByParent[parentContextId] = children;
            }
            children.Add(contextId);

            if (_roots.TryGetValue(rootId, out var rootRec))
            {
                _roots[rootId] = rootRec.WithActiveCount(rootRec.ActiveCount + 1)
                    .WithLastTouchedFrame(Frame);
            }

            return contextId;
        }

        /// <summary>
        /// 开始根节点作用域（相当于 CreateRoot + RetainRoot）
        /// </summary>
        public long BeginRoot(
            int kind,
            long sourceActorId = 0,
            long targetActorId = 0,
            object originSource = null,
            object originTarget = null,
            int configId = 0)
        {
            var rootId = CreateRoot(kind, sourceActorId, targetActorId, originSource, originTarget, configId);
            RetainRoot(rootId);
            return rootId;
        }

        /// <summary>
        /// 开始子节点作用域（相当于 CreateChild + RetainRoot）
        /// </summary>
        public long BeginChild(
            long parentContextId,
            int kind,
            long sourceActorId = 0,
            long targetActorId = 0,
            object originSource = null,
            object originTarget = null,
            int configId = 0)
        {
            var childId = CreateChild(parentContextId, kind, sourceActorId, targetActorId, originSource, originTarget, configId);
            var parentRecord = _contexts[parentContextId];
            RetainRoot(parentRecord.RootId);
            return childId;
        }

        /// <summary>
        /// 确保根节点存在，如果不存在则创建
        /// </summary>
        public long EnsureRoot(
            long contextId,
            int kind,
            long sourceActorId = 0,
            long targetActorId = 0,
            object originSource = null,
            object originTarget = null,
            int configId = 0)
        {
            if (contextId != 0 && _contexts.TryGetValue(contextId, out var record))
            {
                if (_roots.TryGetValue(record.RootId, out var rootRec))
                    _roots[record.RootId] = rootRec.WithLastTouchedFrame(Frame);
                return record.RootId;
            }
            return CreateRoot(kind, sourceActorId, targetActorId, originSource, originTarget, configId);
        }

        /// <summary>
        /// 获取节点快照
        /// </summary>
        public TraceSnapshot<T> TryGetSnapshot(long contextId)
        {
            if (!_contexts.TryGetValue(contextId, out var record))
                return default;
            return CreateSnapshot(in record);
        }

        /// <summary>
        /// 检查节点是否存在
        /// </summary>
        public bool Contains(long contextId) => _contexts.ContainsKey(contextId);

        /// <summary>
        /// 判断指定节点是否为叶子节点
        /// </summary>
        public bool IsLeaf(long contextId)
        {
            if (!_contexts.ContainsKey(contextId))
                return false;
            if (_childrenByParent.TryGetValue(contextId, out var children) && children?.Count > 0)
                return false;
            return true;
        }

        /// <summary>
        /// 为叶子节点设置附加快照数据
        /// </summary>
        public void SetLeafData(long contextId, object data)
        {
            if (!IsLeaf(contextId))
                throw new InvalidOperationException($"Context {contextId} is not a leaf node.");
            _leafDataStore.Set(contextId, data);
        }

        /// <summary>
        /// 尝试获取叶子节点附加快照数据
        /// </summary>
        public bool TryGetLeafData(long contextId, out object data)
        {
            if (!IsLeaf(contextId)) { data = null; return false; }
            return _leafDataStore.TryGet(contextId, out data);
        }

        /// <summary>
        /// 获取根节点状态
        /// </summary>
        public bool TryGetRootState(long rootId, out RootState state)
        {
            if (_roots.TryGetValue(rootId, out var record))
            {
                state = new RootState(rootId, record.ActiveCount, record.ExternalRefCount, record.LastTouchedFrame);
                return true;
            }
            state = default;
            return false;
        }

        /// <summary>
        /// 获取根节点的所有子节点 ID
        /// </summary>
        public bool TryGetChildren(long parentId, out IReadOnlyList<long> children)
        {
            if (_childrenByParent.TryGetValue(parentId, out var list))
            {
                children = list;
                return true;
            }
            children = EmptyChildrenList;
            return false;
        }

        private static readonly List<long> EmptyChildrenList = new List<long>();

        /// <summary>
        /// 构建从指定节点到根节点的链路
        /// </summary>
        public bool TryBuildChain(long contextId, List<TraceSnapshot<T>> chain)
        {
            chain.Clear();
            if (!_contexts.ContainsKey(contextId))
                return false;

            var current = contextId;
            while (current != 0)
            {
                if (!_contexts.TryGetValue(current, out var record))
                    break;
                chain.Add(CreateSnapshot(in record));
                if (current == record.RootId)
                    break;
                current = record.ParentId;
            }
            return chain.Count > 0;
        }

        /// <summary>
        /// 获取根节点的统计信息
        /// </summary>
        public bool TryGetRootStats(long rootId, out RootStats stats)
        {
            if (!_roots.ContainsKey(rootId)) { stats = default; return false; }

            var totalNodes = 0;
            var activeNodes = 0;
            var endedNodes = 0;
            var maxDepth = 0;
            CollectStats(rootId, rootId, ref totalNodes, ref activeNodes, ref endedNodes, ref maxDepth, 0);
            stats = new RootStats(rootId, totalNodes, activeNodes, endedNodes, maxDepth);
            return true;
        }

        private void CollectStats(
            long contextId, long rootId,
            ref int totalNodes, ref int activeNodes, ref int endedNodes, ref int maxDepth,
            int currentDepth)
        {
            if (!_contexts.TryGetValue(contextId, out var record) || record.RootId != rootId)
                return;
            totalNodes++;
            if (record.IsEnded) endedNodes++; else activeNodes++;
            maxDepth = Math.Max(maxDepth, currentDepth);
            if (_childrenByParent.TryGetValue(contextId, out var children))
                foreach (var childId in children)
                    CollectStats(childId, rootId, ref totalNodes, ref activeNodes, ref endedNodes, ref maxDepth, currentDepth + 1);
        }

        /// <summary>
        /// 获取指定种类的所有节点
        /// </summary>
        public IEnumerable<TraceSnapshot<T>> GetNodesByKind(int kind)
        {
            foreach (var kvp in _contexts)
                if (kvp.Value.Kind == kind)
                    yield return CreateSnapshot(in kvp.Value);
        }

        /// <summary>
        /// 获取指定根节点下的所有节点
        /// </summary>
        public IEnumerable<TraceSnapshot<T>> GetNodesByRoot(long rootId)
        {
            foreach (var kvp in _contexts)
                if (kvp.Value.RootId == rootId)
                    yield return CreateSnapshot(in kvp.Value);
        }

        /// <summary>
        /// 结束指定节点
        /// </summary>
        public bool End(long contextId, int reason = 0)
        {
            if (!_contexts.TryGetValue(contextId, out var record) || record.IsEnded)
                return false;

            _contexts[contextId] = new TraceContextRecord(
                contextId: record.ContextId,
                rootId: record.RootId,
                parentId: record.ParentId,
                kind: record.Kind,
                endedFrame: Frame,
                endReason: reason);

            if (_roots.TryGetValue(record.RootId, out var rootRec))
                _roots[record.RootId] = rootRec.WithActiveCount(rootRec.ActiveCount - 1)
                    .WithLastTouchedFrame(Frame);
            return true;
        }

        /// <summary>
        /// 结束根节点及其所有子节点
        /// </summary>
        public int EndRoot(long rootId, int reason = 0)
        {
            if (!_contexts.ContainsKey(rootId))
                return 0;
            return EndSubtree(rootId, reason);
        }

        private int EndSubtree(long contextId, int reason)
        {
            var count = End(contextId, reason) ? 1 : 0;
            if (_childrenByParent.TryGetValue(contextId, out var children))
                foreach (var childId in children)
                    count += EndSubtree(childId, reason);
            return count;
        }

        /// <summary>
        /// 保留根节点（增加外部引用计数）
        /// </summary>
        public void RetainRoot(long rootId)
        {
            if (_roots.TryGetValue(rootId, out var record))
                _roots[rootId] = record.WithExternalRefCount(record.ExternalRefCount + 1);
        }

        /// <summary>
        /// 释放根节点（减少外部引用计数）
        /// </summary>
        public void ReleaseRoot(long rootId)
        {
            if (_roots.TryGetValue(rootId, out var record))
                _roots[rootId] = record.WithExternalRefCount(Math.Max(0, record.ExternalRefCount - 1));
        }

        /// <summary>
        /// 清理已结束的根节点
        /// </summary>
        public int Purge(int currentFrame, int keepEndedFrames = 0)
        {
            var purgedCount = 0;
            var toRemove = new List<long>();

            foreach (var kvp in _roots)
            {
                var rootId = kvp.Key;
                var record = kvp.Value;
                if (record.ActiveCount > 0 || record.ExternalRefCount > 0)
                    continue;

                if (keepEndedFrames > 0)
                {
                    if (!_contexts.TryGetValue(rootId, out var ctxRec) || !ctxRec.IsEnded)
                        continue;
                    if (currentFrame - ctxRec.EndedFrame < keepEndedFrames)
                        continue;
                }
                toRemove.Add(rootId);
            }

            foreach (var rootId in toRemove)
            {
                PurgeRoot(rootId);
                purgedCount++;
            }
            return purgedCount;
        }

        /// <summary>
        /// 清理指定根节点及其所有子节点
        /// </summary>
        public void PurgeRoot(long rootId)
        {
            if (_childrenByParent.TryGetValue(rootId, out var children))
                foreach (var childId in children)
                    PurgeRoot(childId);
            _contexts.Remove(rootId);
            _roots.Remove(rootId);
            _childrenByParent.Remove(rootId);
            _metadataStore.Clear(rootId);
            _leafDataStore.Clear(rootId);
        }

        /// <summary>
        /// 获取所有活跃根节点
        /// </summary>
        public IEnumerable<RootState> GetActiveRoots()
        {
            foreach (var kvp in _roots)
                if (kvp.Value.ActiveCount > 0)
                    yield return new RootState(kvp.Key, kvp.Value.ActiveCount, kvp.Value.ExternalRefCount, kvp.Value.LastTouchedFrame);
        }

        /// <summary>
        /// 获取所有已结束的根节点
        /// </summary>
        public IEnumerable<RootState> GetEndedRoots()
        {
            foreach (var kvp in _roots)
                if (kvp.Value.ActiveCount == 0)
                    yield return new RootState(kvp.Key, kvp.Value.ActiveCount, kvp.Value.ExternalRefCount, kvp.Value.LastTouchedFrame);
        }

        // ========================================================================
        // 内部工具
        // ========================================================================

        private TraceSnapshot<T> CreateSnapshot(in TraceContextRecord record)
        {
            var childCount = 0;
            if (_childrenByParent.TryGetValue(record.ContextId, out var children) && children != null)
                childCount = children.Count;
            _metadataStore.TryGetMetadata(record.RootId, out var metadata);
            return new TraceSnapshot<T>(
                contextId: record.ContextId,
                rootId: record.RootId,
                parentId: record.ParentId,
                kind: record.Kind,
                endedFrame: record.EndedFrame,
                endReason: record.EndReason,
                childCount: childCount,
                metadata: metadata);
        }

        private (long id, string display) ExtractOrigin(object origin)
        {
            if (origin == null) return (0, null);
            if (_contextSource.TryExtractTraceId(origin, out var id, out var display))
                return (id, display);
            return (origin.GetHashCode(), origin.ToString());
        }

        // ========================================================================
        // 元数据扩展点（子类实现）
        // ========================================================================

        /// <summary>
        /// 构建根节点元数据（抽象方法，子类必须实现）
        /// </summary>
        protected abstract T CreateMetadata(
            long rootId, int kind,
            long sourceActorId, long targetActorId,
            long originId, string originDisplay,
            long targetId, string targetDisplay,
            int configId);

        /// <summary>
        /// 从元数据中获取源角色 ID
        /// </summary>
        protected virtual long GetSourceActorId(T metadata) => 0;

        /// <summary>
        /// 从元数据中获取目标角色 ID
        /// </summary>
        protected virtual long GetTargetActorId(T metadata) => 0;

        /// <summary>
        /// 从元数据中获取溯源源标识
        /// </summary>
        protected virtual long GetOriginSourceId(T metadata) => 0;

        /// <summary>
        /// 从元数据中获取溯源源显示名
        /// </summary>
        protected virtual string GetOriginSourceDisplay(T metadata) => null;

        /// <summary>
        /// 从元数据中获取溯源目标标识
        /// </summary>
        protected virtual long GetOriginTargetId(T metadata) => 0;

        /// <summary>
        /// 从元数据中获取溯源目标显示名
        /// </summary>
        protected virtual string GetOriginTargetDisplay(T metadata) => null;
    }
}
