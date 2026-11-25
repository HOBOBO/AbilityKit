using System;
using System.Collections;
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
        internal readonly Dictionary<long, ChildIndex> _childrenByParent;
        internal readonly ITraceLeafDataStore _leafDataStore;
        internal readonly ITraceContextSource _contextSource;
        internal long _nextId;
        private long _revision;
        private long _predictionFloor = 1;

        internal sealed class ChildIndex : List<long>
        {
            private IReadOnlyList<long> _view;

            public IReadOnlyList<long> View => _view ??= new ChildIndexView(this);
        }

        private sealed class ChildIndexView : IReadOnlyList<long>
        {
            private readonly ChildIndex _index;

            public ChildIndexView(ChildIndex index) => _index = index;
            public int Count => _index.Count;
            public long this[int index] => _index[index];
            public IEnumerator<long> GetEnumerator() => _index.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        public event Action<TraceRegistryEvent> RegistryEvent;

        /// <summary>
        /// Optional diagnostic callback for failures raised by registry observers.
        /// Observer failures never alter committed trace state or stop other observers.
        /// </summary>
        public Action<TraceRegistryEvent, Exception> ObserverException { get; set; }

        protected TraceTreeRegistryBase(
            ITraceContextSource contextSource,
            ITraceLeafDataStore leafDataStore)
        {
            _contexts = new Dictionary<long, TraceContextRecord>();
            _roots = new Dictionary<long, TraceRootRecord>();
            _childrenByParent = new Dictionary<long, ChildIndex>();
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
            var contextId = ResolveCreationId(null);
            _nextId = contextId + 1L;
            return contextId;
        }

        /// <summary>
        /// 校验外部 ID，并按需推进本地分配游标；不创建节点。
        /// </summary>
        protected void ReserveExternalId(long contextId, bool advanceAllocationFloor = true)
        {
            ValidateCreationId(contextId);
            if (advanceAllocationFloor && contextId >= _nextId) _nextId = contextId + 1L;
        }

        private protected long ResolveCreationId(long? requestedId)
        {
            if (requestedId.HasValue)
            {
                ValidateCreationId(requestedId.Value);
                return requestedId.Value;
            }

            var nextId = _nextId;
            while (nextId < long.MaxValue && _contexts.ContainsKey(nextId)) nextId++;
            if (nextId == long.MaxValue)
                throw new InvalidOperationException("Trace context ID space is exhausted.");
            return nextId;
        }

        private void ValidateCreationId(long contextId)
        {
            if (contextId <= 0L || contextId == long.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(contextId));
            if (_contexts.ContainsKey(contextId))
                throw new InvalidOperationException($"Trace context {contextId} already exists.");
        }

        private protected long CommitCreatedNode<TMetadata>(in TraceContextRecord record, TMetadata metadata,
            ITraceMetadataStore<TMetadata> metadataStore, bool advanceAllocationFloor)
            where TMetadata : TraceMetadata
        {
            ValidateCreationId(record.ContextId);
            var isRoot = record.ParentId == 0L;
            var root = default(TraceRootRecord);
            ChildIndex siblings = null;
            if (!isRoot && (!_contexts.TryGetValue(record.ParentId, out var parent) || parent.RootId != record.RootId ||
                !_roots.TryGetValue(record.RootId, out root) || !_childrenByParent.TryGetValue(record.ParentId, out siblings)))
                throw new InvalidOperationException($"Trace parent {record.ParentId} is no longer available for node creation.");

            metadataStore.SetMetadata(record.ContextId, metadata);
            _contexts.Add(record.ContextId, record);
            _childrenByParent.Add(record.ContextId, new ChildIndex());
            if (isRoot)
                _roots.Add(record.RootId, new TraceRootRecord(1, 0, record.CreatedFrame));
            else
            {
                siblings.Add(record.ContextId);
                _roots[record.RootId] = root.WithActiveCount(root.ActiveCount + 1).WithLastTouchedFrame(record.CreatedFrame);
            }

            if (advanceAllocationFloor && record.ContextId >= _nextId) _nextId = record.ContextId + 1L;
            Publish(new TraceRegistryEvent(isRoot ? TraceRegistryEventKind.RootCreated : TraceRegistryEventKind.ChildCreated,
                record.ContextId, record.RootId, record.ParentId, record.Kind, record.CreatedFrame));
            return record.ContextId;
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

        /// <summary>
        /// 返回直接子节点的只读实时视图（按创建顺序）；叶节点的视图为空。
        /// 不得在修改注册表时枚举。清理或移除父节点后应重新查询，不再使用旧视图。
        /// </summary>
        public bool TryGetChildren(long parentId, out IReadOnlyList<long> children)
        {
            if (_childrenByParent.TryGetValue(parentId, out var list))
            {
                children = list.View;
                return true;
            }

            children = Array.Empty<long>();
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

        /// <summary>
        /// Revokes externally allocated observation nodes without applying this registry's
        /// allocator boundary. Descendants are included to keep the observation tree valid.
        /// </summary>
        protected int RetractExternalNodes(IReadOnlyCollection<long> contextIds, long eventBoundary)
        {
            if (contextIds == null) throw new ArgumentNullException(nameof(contextIds));
            if (contextIds.Count == 0) return 0;

            var removedIds = new HashSet<long>();
            var pending = new Stack<long>();
            foreach (var contextId in contextIds)
                if (_contexts.ContainsKey(contextId) && removedIds.Add(contextId))
                    pending.Push(contextId);

            while (pending.Count > 0)
            {
                var parentId = pending.Pop();
                if (!_childrenByParent.TryGetValue(parentId, out var children)) continue;
                for (var i = 0; i < children.Count; i++)
                {
                    var childId = children[i];
                    if (removedIds.Add(childId)) pending.Push(childId);
                }
            }

            var affectedParents = new HashSet<long>();
            foreach (var contextId in removedIds)
            {
                if (!_contexts.TryGetValue(contextId, out var record)) continue;
                if (record.ParentId != 0L) affectedParents.Add(record.ParentId);
                if (!record.IsEnded && _roots.TryGetValue(record.RootId, out var root))
                    _roots[record.RootId] = root.WithActiveCount(root.ActiveCount - 1)
                        .WithLastTouchedFrame(GetCurrentFrame());
                _contexts.Remove(contextId);
                _childrenByParent.Remove(contextId);
                if (record.ContextId == record.RootId) _roots.Remove(contextId);
                OnPredictionNodeRemoved(contextId);
                _leafDataStore.Clear(contextId);
            }

            foreach (var parentId in affectedParents)
                if (_childrenByParent.TryGetValue(parentId, out var children))
                    children.RemoveAll(removedIds.Contains);

            if (removedIds.Count > 0)
                Publish(new TraceRegistryEvent(
                    TraceRegistryEventKind.PredictionRetracted,
                    eventBoundary,
                    0,
                    0,
                    0,
                    GetCurrentFrame()));
            return removedIds.Count;
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
        public virtual void Dispose()
        {
            TraceRegistryDirectory.Unregister(this);
            Clear();
            RegistryEvent = null;
            ObserverException = null;
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
        /// 释放一个根引用。最后一个引用在树已结束时释放，会刷新回收保留窗口。
        /// 不存在的根或已无引用的根不产生变更。
        /// </summary>
        public void ReleaseRoot(long rootId)
        {
            if (_roots.TryGetValue(rootId, out var record) && record.ExternalRefCount > 0)
            {
                var frame = GetCurrentFrame();
                var updated = record.WithExternalRefCount(record.ExternalRefCount - 1);
                if (updated.ActiveCount == 0 && updated.ExternalRefCount == 0)
                    updated = updated.WithLastTouchedFrame(Math.Max(record.LastTouchedFrame, frame));
                _roots[rootId] = updated;
                Publish(new TraceRegistryEvent(TraceRegistryEventKind.RootReleased, rootId, rootId, 0, 0, frame));
            }
        }

        /// <summary>
        /// 结束指定节点
        /// </summary>
        public bool End(long contextId, int reason = 0)
        {
            return EndAtFrame(contextId, reason, GetCurrentFrame());
        }

        /// <summary>Ends a node using the frame supplied by its lifecycle owner.</summary>
        public bool EndAtFrame(long contextId, int reason, int frame)
        {
            if (!_contexts.TryGetValue(contextId, out var record) || record.IsEnded)
                return false;

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
                    .WithLastTouchedFrame(Math.Max(rootRec.LastTouchedFrame, frame));

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
            var observers = RegistryEvent;
            if (observers == null) return;

            foreach (Action<TraceRegistryEvent> observer in observers.GetInvocationList())
            {
                try
                {
                    observer(registryEvent);
                }
                catch (Exception ex)
                {
                    try
                    {
                        ObserverException?.Invoke(registryEvent, ex);
                    }
                    catch
                    {
                        // Observation diagnostics must not alter committed trace state.
                    }
                }
            }
        }

        protected virtual object GetMetadataObject(long contextId) => null;

        private protected int GetChildCount(long contextId) =>
            _childrenByParent.TryGetValue(contextId, out var children) ? children.Count : 0;

        private protected void CollectStats(
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

        private protected void RemoveRootTree<TMetadata>(long rootId, ITraceMetadataStore<TMetadata> metadataStore)
            where TMetadata : TraceMetadata
        {
            var pending = new Stack<long>();
            var records = new List<TraceContextRecord>();
            pending.Push(rootId);
            while (pending.Count > 0)
            {
                var contextId = pending.Pop();
                records.Add(_contexts[contextId]);
                if (_childrenByParent.TryGetValue(contextId, out var children))
                    foreach (var childId in children)
                        pending.Push(childId);
            }

            _roots.Remove(rootId);
            for (var index = records.Count - 1; index >= 0; index--)
            {
                var contextId = records[index].ContextId;
                _contexts.Remove(contextId);
                _childrenByParent.Remove(contextId);
                metadataStore.Clear(contextId);
                _leafDataStore.Clear(contextId);
            }

            /*
             * 整树删除后按子节点先于父节点的顺序通知，供诊断方清除逐节点数据。
             */
            var frame = GetCurrentFrame();
            for (var index = records.Count - 1; index >= 0; index--)
            {
                var record = records[index];
                Publish(new TraceRegistryEvent(TraceRegistryEventKind.RootPurged,
                    record.ContextId, rootId, record.ParentId, record.Kind, frame));
            }
        }

        private TraceNodeSnapshot CreateNodeSnapshot(in TraceContextRecord record)
        {
            return new TraceNodeSnapshot(
                contextId: record.ContextId,
                rootId: record.RootId,
                parentId: record.ParentId,
                kind: record.Kind,
                createdFrame: record.CreatedFrame,
                endedFrame: record.EndedFrame,
                endReason: record.EndReason,
                childCount: GetChildCount(record.ContextId),
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
            return CreateRootCore(null, kind, sourceActorId, targetActorId, originSource, originTarget, configId, null, true);
        }

        /// <summary>
        /// 使用外部执行上下文的 ID 创建根节点。可选择创建成功后是否推进本地 ID 游标。
        /// </summary>
        public long CreateRootWithId(
            long contextId,
            int kind,
            long sourceActorId = 0,
            long targetActorId = 0,
            object originSource = null,
            object originTarget = null,
            int configId = 0,
            int createdFrame = -1,
            bool advanceAllocationFloor = true)
        {
            return CreateRootCore(contextId, kind, sourceActorId, targetActorId, originSource, originTarget,
                configId, createdFrame < 0 ? (int?)null : createdFrame, advanceAllocationFloor);
        }

        private long CreateRootCore(long? requestedId, int kind, long sourceActorId, long targetActorId,
            object originSource, object originTarget, int configId, int? createdFrame, bool advanceAllocationFloor)
        {
            var contextId = ResolveCreationId(requestedId);
            var (originId, originDisplay) = ExtractOrigin(originSource);
            var (targetId, targetDisplay) = ExtractOrigin(originTarget);
            var record = new TraceContextRecord(
                contextId, contextId, 0L, kind, createdFrame ?? Frame, 0, 0, false);
            var metadata = CreateMetadata(
                contextId, kind, sourceActorId, targetActorId,
                originId, originDisplay, targetId, targetDisplay, configId);
            return CommitCreatedNode(in record, metadata, _metadataStore, advanceAllocationFloor);
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
            return CreateChildCore(null, parentContextId, kind, sourceActorId, targetActorId,
                originSource, originTarget, configId, null, true);
        }

        /// <summary>
        /// 使用外部执行上下文的 ID 创建子节点。可选择创建成功后是否推进本地 ID 游标。
        /// </summary>
        public long CreateChildWithId(
            long contextId,
            long parentContextId,
            int kind,
            long sourceActorId = 0,
            long targetActorId = 0,
            object originSource = null,
            object originTarget = null,
            int configId = 0,
            int createdFrame = -1,
            bool advanceAllocationFloor = true)
        {
            return CreateChildCore(contextId, parentContextId, kind, sourceActorId, targetActorId,
                originSource, originTarget, configId, createdFrame < 0 ? (int?)null : createdFrame, advanceAllocationFloor);
        }

        private long CreateChildCore(long? requestedId, long parentContextId, int kind, long sourceActorId,
            long targetActorId, object originSource, object originTarget, int configId, int? createdFrame,
            bool advanceAllocationFloor)
        {
            if (!_contexts.TryGetValue(parentContextId, out var parentRecord))
                throw new ArgumentException($"Parent context {parentContextId} not found", nameof(parentContextId));

            var contextId = ResolveCreationId(requestedId);
            var rootId = parentRecord.RootId;
            T rootMetadata = null;
            var inherit = (originSource == null || originTarget == null || sourceActorId == 0L || targetActorId == 0L)
                && _metadataStore.TryGetMetadata(rootId, out rootMetadata);
            var (originId, originDisplay) = originSource != null ? ExtractOrigin(originSource)
                : inherit ? (GetOriginSourceId(rootMetadata), GetOriginSourceDisplay(rootMetadata)) : (0L, null);
            var (targetId, targetDisplay) = originTarget != null ? ExtractOrigin(originTarget)
                : inherit ? (GetOriginTargetId(rootMetadata), GetOriginTargetDisplay(rootMetadata)) : (0L, null);
            if (inherit && sourceActorId == 0L) sourceActorId = GetSourceActorId(rootMetadata);
            if (inherit && targetActorId == 0L) targetActorId = GetTargetActorId(rootMetadata);

            var record = new TraceContextRecord(
                contextId, rootId, parentContextId, kind, createdFrame ?? Frame, 0, 0, false);
            /*
             * 保留两类公开入口原有的元数据参数；结构记录始终使用实际所属根。
             */
            var metadata = CreateMetadata(requestedId.HasValue ? rootId : contextId, kind, sourceActorId, targetActorId,
                originId, originDisplay, targetId, targetDisplay, configId);
            return CommitCreatedNode(in record, metadata, _metadataStore, advanceAllocationFloor);
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
        /// 回收已结束且无人持有的整棵树。保留窗口以根最近一次变更帧为基准，
        /// 使用与当前注册表相同的帧时钟；非正值表示不等待。返回实际回收的根数。
        /// </summary>
        public int Purge(int currentFrame, int keepEndedFrames = 0)
        {
            var purgedCount = 0;
            var toRemove = new List<long>();

            foreach (var rootId in _roots.Keys)
                if (CanPurgeRoot(rootId, currentFrame, keepEndedFrames))
                    toRemove.Add(rootId);

            foreach (var rootId in toRemove)
            {
                /*
                 * 订阅者可能已重新持有后续的候选根，因此删除前重新检查。
                 */
                if (!CanPurgeRoot(rootId, currentFrame, keepEndedFrames)) continue;
                RemoveRootTree(rootId, _metadataStore);
                purgedCount++;
            }
            return purgedCount;
        }

        /// <summary>
        /// 兼容入口：立即尝试回收已结束且无人持有的整棵树，不等待保留窗口。
        /// 需要获知是否实际回收时，使用 <see cref="TryPurgeRoot"/>。
        /// </summary>
        public void PurgeRoot(long rootId)
        {
            TryPurgeRoot(rootId);
        }

        /// <summary>
        /// 尝试立即回收已结束且无人持有的整棵树。不存在的 ID、子节点 ID、
        /// 活动或仍被持有的根均返回 false，不改变状态，也不发布事件。
        /// </summary>
        public bool TryPurgeRoot(long rootId)
        {
            if (!CanPurgeRoot(rootId, currentFrame: 0, keepEndedFrames: 0)) return false;
            RemoveRootTree(rootId, _metadataStore);
            return true;
        }

        private bool CanPurgeRoot(long rootId, int currentFrame, int keepEndedFrames)
        {
            return _roots.TryGetValue(rootId, out var root) && root.ActiveCount == 0 && root.ExternalRefCount == 0
                && _contexts.TryGetValue(rootId, out var node) && node.IsEnded
                && (keepEndedFrames <= 0 || (long)currentFrame - root.LastTouchedFrame >= keepEndedFrames);
        }

        // ========================================================================
        // 内部工具
        // ========================================================================

        private TraceSnapshot<T> CreateSnapshot(in TraceContextRecord record)
        {
            _metadataStore.TryGetMetadata(record.ContextId, out var metadata);
            return new TraceSnapshot<T>(
                contextId: record.ContextId,
                rootId: record.RootId,
                parentId: record.ParentId,
                kind: record.Kind,
                createdFrame: record.CreatedFrame,
                endedFrame: record.EndedFrame,
                endReason: record.EndReason,
                childCount: GetChildCount(record.ContextId),
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
        /// 在节点提交前创建业务元数据，不得重入注册表写操作。
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
