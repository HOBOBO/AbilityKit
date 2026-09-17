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
        public readonly int CreatedFrame;
        public readonly int EndedFrame;
        public readonly int EndReason;
        public readonly bool IsEnded;

        public TraceContextRecord(
            long contextId,
            long rootId,
            long parentId,
            int kind,
            int createdFrame,
            int endedFrame,
            int endReason,
            bool isEnded)
        {
            ContextId = contextId;
            RootId = rootId;
            ParentId = parentId;
            Kind = kind;
            CreatedFrame = createdFrame;
            EndedFrame = endedFrame;
            EndReason = endReason;
            IsEnded = isEnded;
        }
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
        private long _revision;
        private long _predictionFloor = 1;

        private static readonly List<long> EmptyChildrenList = new List<long>();

        public event Action<TraceRegistryEvent> RegistryEvent;

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
            TraceRegistryDirectory.Register(this);
        }

        /// <summary>
        /// 获取当前帧号（子类可覆盖）
        /// </summary>
        protected virtual int Frame => 0;

        /// <summary>
        /// 内部获取当前帧号（供扩展方法使用）
        /// </summary>
        internal int GetCurrentFrame() => Frame;

        /// <summary>
        /// 生成新的唯一 ID
        /// </summary>
        protected long NewId()
        {
            if (_nextId == long.MaxValue)
                throw new InvalidOperationException("Trace context ID space is exhausted.");
            return _nextId++;
        }

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
        /// 获取查询可见 Trace 数据的单调版本。
        /// </summary>
        public long Revision => _revision;

        public long NextContextId => _nextId;

        /// <summary>
        /// 获取根节点 ID 列表
        /// </summary>
        public IEnumerable<long> RootIds => _roots.Keys;

        public virtual string GetKindName(int kind) => null;

        public IEnumerable<RootState> GetRootStates(bool activeOnly = false)
        {
            foreach (var kvp in _roots)
            {
                if (activeOnly && kvp.Value.ActiveCount <= 0)
                    continue;
                yield return new RootState(kvp.Key, kvp.Value.ActiveCount, kvp.Value.ExternalRefCount, kvp.Value.LastTouchedFrame);
            }
        }

        public IEnumerable<RootState> GetActiveRoots()
        {
            foreach (var state in GetRootStates(true))
                yield return state;
        }

        public IEnumerable<RootState> GetEndedRoots()
        {
            foreach (var kvp in _roots)
                if (kvp.Value.ActiveCount == 0)
                    yield return new RootState(kvp.Key, kvp.Value.ActiveCount, kvp.Value.ExternalRefCount, kvp.Value.LastTouchedFrame);
        }

        public bool TryGetNodeSnapshot(long contextId, out TraceNodeSnapshot snapshot)
        {
            if (_contexts.TryGetValue(contextId, out var record))
            {
                snapshot = CreateNodeSnapshot(record);
                return true;
            }

            snapshot = default;
            return false;
        }

        public IEnumerable<TraceNodeSnapshot> GetNodeSnapshotsByRoot(long rootId)
        {
            foreach (var kvp in _contexts)
                if (kvp.Value.RootId == rootId)
                    yield return CreateNodeSnapshot(kvp.Value);
        }

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

        public bool Contains(long contextId) => _contexts.ContainsKey(contextId);

        public void ValidatePredictionRetraction(long nextContextId)
        {
            if (nextContextId < _predictionFloor || nextContextId > _nextId)
                throw new InvalidOperationException($"Invalid Trace prediction boundary {nextContextId}.");
        }

        /// <summary>Revokes nodes allocated after a local snapshot, without reusing their identities.</summary>
        public int RetractPrediction(long nextContextId)
        {
            ValidatePredictionRetraction(nextContextId);
            var affectedParents = new HashSet<long>();
            var removedCount = 0;
            for (var id = _nextId - 1L; id >= nextContextId; id--)
            {
                if (!_contexts.TryGetValue(id, out var record)) continue;
                if (record.ParentId != 0L) affectedParents.Add(record.ParentId);
                if (!record.IsEnded && _roots.TryGetValue(record.RootId, out var root))
                    _roots[record.RootId] = root.WithActiveCount(root.ActiveCount - 1).WithLastTouchedFrame(GetCurrentFrame());
                _contexts.Remove(id);
                _childrenByParent.Remove(id);
                if (record.ContextId == record.RootId) _roots.Remove(id);
                OnPredictionNodeRemoved(id);
                _leafDataStore.Clear(id);
                removedCount++;
            }
            foreach (var parentId in affectedParents)
                if (_childrenByParent.TryGetValue(parentId, out var children))
                    children.RemoveAll(id => id >= nextContextId);
            if (removedCount > 0)
                Publish(new TraceRegistryEvent(TraceRegistryEventKind.PredictionRetracted, nextContextId, 0, 0, 0, GetCurrentFrame()));
            return removedCount;
        }

        protected virtual void OnPredictionNodeRemoved(long contextId) { }

        /// <summary>Validates a local lifecycle restore without rebuilding missing identities or metadata.</summary>
        public void ValidateLifecycleRestore(IReadOnlyList<TraceNodeSnapshot> nodes)
        {
            if (nodes == null) throw new ArgumentNullException(nameof(nodes));
            var ids = new HashSet<long>();
            foreach (var node in nodes)
            {
                if (!ids.Add(node.ContextId) || !_roots.ContainsKey(node.RootId) || !_contexts.TryGetValue(node.ContextId, out var current) ||
                    current.RootId != node.RootId || current.ParentId != node.ParentId ||
                    current.Kind != node.Kind || current.CreatedFrame != node.CreatedFrame ||
                    (!node.IsEnded && (node.EndedFrame != 0 || node.EndReason != 0)))
                    throw new InvalidOperationException($"Trace lifecycle identity {node.ContextId} is missing or invalid.");
            }
        }

        /// <summary>Restores captured node lifecycle only; current external retain handles remain authoritative.</summary>
        public void RestoreLifecycle(IReadOnlyList<TraceNodeSnapshot> nodes)
        {
            ValidateLifecycleRestore(nodes);
            var activeDeltas = new Dictionary<long, int>();
            foreach (var node in nodes)
            {
                var current = _contexts[node.ContextId];
                activeDeltas.TryGetValue(node.RootId, out var delta);
                activeDeltas[node.RootId] = delta + (node.IsEnded ? 0 : 1) - (current.IsEnded ? 0 : 1);
                _contexts[node.ContextId] = new TraceContextRecord(node.ContextId, node.RootId, node.ParentId,
                    node.Kind, node.CreatedFrame, node.EndedFrame, node.EndReason, node.IsEnded);
            }
            foreach (var change in activeDeltas)
            {
                var root = _roots[change.Key];
                _roots[change.Key] = root.WithActiveCount(root.ActiveCount + change.Value).WithLastTouchedFrame(GetCurrentFrame());
            }
            if (nodes.Count > 0)
                Publish(new TraceRegistryEvent(TraceRegistryEventKind.LifecycleRestored, 0, 0, 0, 0, GetCurrentFrame()));
        }

        public bool IsLeaf(long contextId)
        {
            if (!_contexts.ContainsKey(contextId))
                return false;
            if (_childrenByParent.TryGetValue(contextId, out var children) && children?.Count > 0)
                return false;
            return true;
        }

        public void SetLeafData(long contextId, object data)
        {
            if (!IsLeaf(contextId))
                throw new InvalidOperationException($"Context {contextId} is not a leaf node.");
            _leafDataStore.Set(contextId, data);
        }

        public bool TryGetLeafData(long contextId, out object data)
        {
            if (!IsLeaf(contextId)) { data = null; return false; }
            return _leafDataStore.TryGet(contextId, out data);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            TraceRegistryDirectory.Unregister(this);
            Clear();
        }

        /// <summary>
        /// 清理所有数据
        /// </summary>
        public void Clear()
        {
            OnClearing();
            _contexts.Clear();
            _roots.Clear();
            _childrenByParent.Clear();
            _predictionFloor = _nextId;
            OnClear();
            Publish(new TraceRegistryEvent(TraceRegistryEventKind.RegistryCleared, 0, 0, 0, 0, GetCurrentFrame()));
        }

        /// <summary>
        /// 清理时触发（子类可覆盖）
        /// </summary>
        protected virtual void OnClear() { }

        /// <summary>Clears owned mappings while node identities are still available.</summary>
        protected virtual void OnClearing()
        {
            foreach (var id in _contexts.Keys)
                _leafDataStore.Clear(id);
        }

        /// <summary>
        /// 保留根节点（增加外部引用计数）
        /// </summary>
        public void RetainRoot(long rootId)
        {
            if (_roots.TryGetValue(rootId, out var record))
            {
                _roots[rootId] = record.WithExternalRefCount(record.ExternalRefCount + 1);
                Publish(new TraceRegistryEvent(TraceRegistryEventKind.RootRetained, rootId, rootId, 0, 0, GetCurrentFrame()));
            }
        }

        /// <summary>
        /// 释放根节点（减少外部引用计数）
        /// </summary>
        public void ReleaseRoot(long rootId)
        {
            if (_roots.TryGetValue(rootId, out var record))
            {
                _roots[rootId] = record.WithExternalRefCount(Math.Max(0, record.ExternalRefCount - 1));
                Publish(new TraceRegistryEvent(TraceRegistryEventKind.RootReleased, rootId, rootId, 0, 0, GetCurrentFrame()));
            }
        }

        /// <summary>
        /// 结束指定节点
        /// </summary>
        public bool End(long contextId, int reason = 0)
        {
            if (!_contexts.TryGetValue(contextId, out var record) || record.IsEnded)
                return false;

            var frame = GetCurrentFrame();
            _contexts[contextId] = new TraceContextRecord(
                contextId: record.ContextId,
                rootId: record.RootId,
                parentId: record.ParentId,
                kind: record.Kind,
                createdFrame: record.CreatedFrame,
                endedFrame: frame,
                endReason: reason,
                isEnded: true);

            if (_roots.TryGetValue(record.RootId, out var rootRec))
                _roots[record.RootId] = rootRec.WithActiveCount(rootRec.ActiveCount - 1)
                    .WithLastTouchedFrame(frame);

            Publish(new TraceRegistryEvent(TraceRegistryEventKind.NodeEnded, contextId, record.RootId, record.ParentId, record.Kind, frame, reason));
            return true;
        }

        /// <summary>
        /// 结束根节点及其所有子节点
        /// </summary>
        public int EndRoot(long rootId, int reason = 0)
        {
            if (!_contexts.TryGetValue(rootId, out var record))
                return 0;

            var count = EndSubtree(rootId, reason);
            if (count > 0)
                Publish(new TraceRegistryEvent(TraceRegistryEventKind.RootEnded, rootId, rootId, 0, record.Kind, GetCurrentFrame(), reason));
            return count;
        }

        private int EndSubtree(long contextId, int reason)
        {
            var count = End(contextId, reason) ? 1 : 0;
            if (_childrenByParent.TryGetValue(contextId, out var children))
                foreach (var childId in children)
                    count += EndSubtree(childId, reason);
            return count;
        }

        protected void Publish(in TraceRegistryEvent registryEvent)
        {
            _revision++;
            RegistryEvent?.Invoke(registryEvent);
        }

        protected virtual object GetMetadataObject(long contextId) => null;
 
        private TraceNodeSnapshot CreateNodeSnapshot(in TraceContextRecord record)
        {
            var childCount = 0;
            if (_childrenByParent.TryGetValue(record.ContextId, out var children) && children != null)
                childCount = children.Count;
 
            return new TraceNodeSnapshot(
                contextId: record.ContextId,
                rootId: record.RootId,
                parentId: record.ParentId,
                kind: record.Kind,
                createdFrame: record.CreatedFrame,
                endedFrame: record.EndedFrame,
                endReason: record.EndReason,
                childCount: childCount,
                metadata: GetMetadataObject(record.ContextId),
                isEnded: record.IsEnded);
        }
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

        protected override void OnPredictionNodeRemoved(long contextId) => _metadataStore.Clear(contextId);

        protected override void OnClearing()
        {
            foreach (var id in _contexts.Keys)
                _metadataStore.Clear(id);
            base.OnClearing();
        }

        protected override object GetMetadataObject(long contextId)
        {
            return _metadataStore.TryGetMetadata(contextId, out var metadata) ? metadata : null;
        }

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

            var createdFrame = Frame;
            var record = new TraceContextRecord(
                contextId: contextId,
                rootId: contextId,
                parentId: 0,
                kind: kind,
                createdFrame: createdFrame,
                endedFrame: 0,
                endReason: 0,
                isEnded: false);

            _contexts[contextId] = record;
            _roots[contextId] = new TraceRootRecord(1, 0, createdFrame);
            _childrenByParent[contextId] = new List<long>();

            var metadata = CreateMetadata(
                contextId, kind, sourceActorId, targetActorId,
                originId, originDisplay, targetId, targetDisplay, configId);
            _metadataStore.SetMetadata(contextId, metadata);
            Publish(new TraceRegistryEvent(TraceRegistryEventKind.RootCreated, contextId, contextId, 0, kind, createdFrame));

            return contextId;
        }

        public long CreateRoot(in TraceOrigin origin)
        {
            return CreateRoot(
                origin.Kind,
                origin.SourceActorId,
                origin.TargetActorId,
                origin.OriginSource,
                origin.OriginTarget,
                origin.ConfigId);
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

            var createdFrame = Frame;
            var record = new TraceContextRecord(
                contextId: contextId,
                rootId: rootId,
                parentId: parentContextId,
                kind: kind,
                createdFrame: createdFrame,
                endedFrame: 0,
                endReason: 0,
                isEnded: false);

            _contexts[contextId] = record;
            _childrenByParent[contextId] = new List<long>();
 
            var metadata = CreateMetadata(
                contextId,
                kind,
                sourceActorId,
                targetActorId,
                originId,
                originDisplay,
                targetId,
                targetDisplay,
                configId);
            _metadataStore.SetMetadata(contextId, metadata);
 
            if (!_childrenByParent.TryGetValue(parentContextId, out var children))
            {
                children = new List<long>();
                _childrenByParent[parentContextId] = children;
            }
            children.Add(contextId);
 
            if (_roots.TryGetValue(rootId, out var rootRec))
            {
                _roots[rootId] = rootRec.WithActiveCount(rootRec.ActiveCount + 1)
                    .WithLastTouchedFrame(createdFrame);
            }
 
            Publish(new TraceRegistryEvent(TraceRegistryEventKind.ChildCreated, contextId, rootId, parentContextId, kind, createdFrame));
            return contextId;
        }

        public long CreateChild(in TraceOrigin origin)
        {
            return CreateChild(
                origin.ParentContextId,
                origin.Kind,
                origin.SourceActorId,
                origin.TargetActorId,
                origin.OriginSource,
                origin.OriginTarget,
                origin.ConfigId);
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

        public long BeginRoot(in TraceOrigin origin)
        {
            var rootId = CreateRoot(origin);
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

        public long BeginChild(in TraceOrigin origin)
        {
            var childId = CreateChild(origin);
            var parentRecord = _contexts[origin.ParentContextId];
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

        public long EnsureRoot(long contextId, in TraceOrigin origin)
        {
            return EnsureRoot(
                contextId,
                origin.Kind,
                origin.SourceActorId,
                origin.TargetActorId,
                origin.OriginSource,
                origin.OriginTarget,
                origin.ConfigId);
        }

        /// <summary>
        /// 获取节点快照
        /// </summary>
        public TraceSnapshot<T> TryGetSnapshot(long contextId)
        {
            if (!_contexts.TryGetValue(contextId, out var record))
                return default;
            return CreateSnapshot(record);
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
                chain.Add(CreateSnapshot(record));
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
                    yield return CreateSnapshot(kvp.Value);
        }

        /// <summary>
        /// 获取指定根节点下的所有节点
        /// </summary>
        public IEnumerable<TraceSnapshot<T>> GetNodesByRoot(long rootId)
        {
            foreach (var kvp in _contexts)
                if (kvp.Value.RootId == rootId)
                    yield return CreateSnapshot(kvp.Value);
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
            Publish(new TraceRegistryEvent(TraceRegistryEventKind.RootPurged, rootId, rootId, 0, 0, Frame));
        }

        // ========================================================================
        // 内部工具
        // ========================================================================

        private TraceSnapshot<T> CreateSnapshot(in TraceContextRecord record)
        {
            var childCount = 0;
            if (_childrenByParent.TryGetValue(record.ContextId, out var children) && children != null)
                childCount = children.Count;
            _metadataStore.TryGetMetadata(record.ContextId, out var metadata);
            return new TraceSnapshot<T>(
                contextId: record.ContextId,
                rootId: record.RootId,
                parentId: record.ParentId,
                kind: record.Kind,
                createdFrame: record.CreatedFrame,
                endedFrame: record.EndedFrame,
                endReason: record.EndReason,
                childCount: childCount,
                metadata: metadata,
                isEnded: record.IsEnded);
        }

        private (long id, string display) ExtractOrigin(object origin)
        {
            if (origin == null) return (0, null);
            if (origin is TraceEndpoint endpoint)
                return (endpoint.Id, endpoint.DisplayName);
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
