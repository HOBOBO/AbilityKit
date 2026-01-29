using System;
using System.Collections.Generic;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.World.Services;
using AbilityKit.Triggering.Blackboard;

namespace AbilityKit.Ability.Impl.Moba.EffectSource
{
    public sealed class EffectSourceRegistry : IService
    {
        public static bool Enabled { get; set; } = false;
        public static bool StoreOriginObjects { get; set; } = false;

        public readonly struct IntKey
        {
            public readonly int Id;

            public IntKey(int id)
            {
                Id = id;
            }
        }

        public readonly struct EffectSourceScope : IDisposable
        {
            private readonly EffectSourceRegistry _registry;
            private readonly long _contextId;
            private readonly int _frame;
            private readonly EffectSourceEndReason _reason;

            public long ContextId => _contextId;

            public EffectSourceScope(EffectSourceRegistry registry, long contextId, int frame, EffectSourceEndReason reason)
            {
                _registry = registry;
                _contextId = contextId;
                _frame = frame;
                _reason = reason;
            }

            public void Dispose()
            {
                if (_registry == null) return;
                if (_contextId <= 0) return;
                _registry.End(_contextId, _frame, _reason);
            }
        }

        private sealed class ContextRecord
        {
            public long ContextId;
            public long RootId;
            public long ParentId;

            public EffectSourceKind Kind;
            public int ConfigId;
            public int SourceActorId;
            public int TargetActorId;

            public object OriginSource;
            public object OriginTarget;

            public int CreatedFrame;
            public int EndedFrame;
            public EffectSourceEndReason EndReason;
        }

        private sealed class RootRecord
        {
            public int ActiveCount;
            public int ExternalRefCount;
            public int LastTouchedFrame;
        }

        private long _nextId = 1;

        private readonly Dictionary<long, ContextRecord> _contexts = new Dictionary<long, ContextRecord>(256);
        private readonly Dictionary<long, RootRecord> _roots = new Dictionary<long, RootRecord>(64);
        private readonly Dictionary<long, List<long>> _childrenByParent = new Dictionary<long, List<long>>(256);
        private readonly Dictionary<long, DictionaryBlackboard> _rootBlackboards = new Dictionary<long, DictionaryBlackboard>(64);

        public EffectSourceRegistry()
        {
#if UNITY_EDITOR
            try
            {
                EffectSourceLiveRegistry.Register(this);
            }
            catch
            {
            }
#endif
        }

        public int ContextCount => _contexts.Count;
        public int RootCount => _roots.Count;

        public bool TryGetRootBlackboard(long rootId, out IBlackboard blackboard)
        {
            if (!Enabled)
            {
                blackboard = null;
                return false;
            }

            if (rootId <= 0)
            {
                blackboard = null;
                return false;
            }

            if (_rootBlackboards.TryGetValue(rootId, out var bb) && bb != null)
            {
                blackboard = bb;
                return true;
            }

            blackboard = null;
            return false;
        }

        public IBlackboard GetOrCreateRootBlackboard(long rootId, int capacity = 16)
        {
            if (!Enabled) return null;
            if (rootId <= 0) return null;

            if (_rootBlackboards.TryGetValue(rootId, out var bb) && bb != null)
            {
                return bb;
            }

            bb = new DictionaryBlackboard(capacity);
            _rootBlackboards[rootId] = bb;
            return bb;
        }

        public bool TryGetRootInt(long rootId, IntKey key, out int value)
        {
            value = default;
            if (!Enabled) return false;
            if (rootId <= 0) return false;
            if (!_rootBlackboards.TryGetValue(rootId, out var bb) || bb == null) return false;
            return bb.TryGetInt(key.Id, out value);
        }

        public int CopyRootIdsTo(List<long> list)
        {
            if (!Enabled) return 0;
            if (list == null) return 0;

            list.Clear();
            foreach (var kv in _roots)
            {
                list.Add(kv.Key);
            }
            return list.Count;
        }

        public bool TryCopyRootBlackboardInts(long rootId, List<KeyValuePair<int, int>> list)
        {
            if (!Enabled) return false;
            if (rootId <= 0) return false;
            if (list == null) return false;
            if (!_rootBlackboards.TryGetValue(rootId, out var bb) || bb == null) return false;
            bb.CopyIntsTo(list);
            return true;
        }

        public void SetRootInt(long rootId, IntKey key, int value)
        {
            if (!Enabled) return;
            if (rootId <= 0) return;
            var bb = GetOrCreateRootBlackboard(rootId);
            bb?.SetInt(key.Id, value);
        }

        public EffectSourceScope BeginRoot(
            EffectSourceKind kind,
            int configId,
            int sourceActorId,
            int targetActorId,
            int frame,
            EffectSourceEndReason endReason = EffectSourceEndReason.Completed,
            object originSource = null,
            object originTarget = null)
        {
            var id = CreateRoot(kind, configId, sourceActorId, targetActorId, frame, originSource, originTarget);
            return new EffectSourceScope(this, id, frame, endReason);
        }

        public EffectSourceScope BeginChild(
            long parentContextId,
            EffectSourceKind kind,
            int configId,
            int sourceActorId,
            int targetActorId,
            int frame,
            EffectSourceEndReason endReason = EffectSourceEndReason.Completed,
            object originSource = null,
            object originTarget = null)
        {
            var id = CreateChild(parentContextId, kind, configId, sourceActorId, targetActorId, frame, originSource, originTarget);
            return new EffectSourceScope(this, id, frame, endReason);
        }

        public long CreateRoot(
            EffectSourceKind kind,
            int configId,
            int sourceActorId,
            int targetActorId,
            int frame)
        {
            if (!Enabled) return 0;
            return CreateRoot(kind, configId, sourceActorId, targetActorId, frame, originSource: null, originTarget: null);
        }

        public long CreateRoot(
            EffectSourceKind kind,
            int configId,
            int sourceActorId,
            int targetActorId,
            int frame,
            object originSource,
            object originTarget)
        {
            if (!Enabled) return 0;
            if (kind == EffectSourceKind.None) return 0;
            var id = NextId();

            if (frame > LastFrame) LastFrame = frame;

            originSource = NormalizeOrigin(originSource, sourceActorId);
            originTarget = NormalizeOrigin(originTarget, targetActorId);

            var r = new ContextRecord
            {
                ContextId = id,
                RootId = id,
                ParentId = 0,
                Kind = kind,
                ConfigId = configId,
                SourceActorId = sourceActorId,
                TargetActorId = targetActorId,
                OriginSource = originSource ?? sourceActorId,
                OriginTarget = originTarget ?? targetActorId,
                CreatedFrame = frame,
                EndedFrame = 0,
                EndReason = EffectSourceEndReason.None,
            };

            _contexts[id] = r;
            _roots[id] = new RootRecord { ActiveCount = 1, ExternalRefCount = 0, LastTouchedFrame = frame };
            return id;
        }

        public bool TryGetOrigin(long contextId, out object originSource, out object originTarget)
        {
            if (!Enabled)
            {
                originSource = null;
                originTarget = null;
                return false;
            }

            if (_contexts.TryGetValue(contextId, out var r))
            {
                originSource = r.OriginSource;
                originTarget = r.OriginTarget;
                return true;
            }

            originSource = null;
            originTarget = null;
            return false;
        }

        public bool SetOrigin(long contextId, object originSource, object originTarget)
        {
            if (!Enabled) return false;
            if (contextId <= 0) return false;
            if (!_contexts.TryGetValue(contextId, out var r)) return false;
            if (originSource != null) r.OriginSource = NormalizeOrigin(originSource, r.SourceActorId);
            if (originTarget != null) r.OriginTarget = NormalizeOrigin(originTarget, r.TargetActorId);
            return true;
        }

        public long CreateChild(
            long parentContextId,
            EffectSourceKind kind,
            int configId,
            int sourceActorId,
            int targetActorId,
            int frame)
        {
            if (!Enabled) return 0;
            return CreateChild(parentContextId, kind, configId, sourceActorId, targetActorId, frame, originSource: null, originTarget: null);
        }

        public long CreateChild(
            long parentContextId,
            EffectSourceKind kind,
            int configId,
            int sourceActorId,
            int targetActorId,
            int frame,
            object originSource,
            object originTarget)
        {
            if (!Enabled) return 0;
            if (kind == EffectSourceKind.None) return 0;
            if (parentContextId <= 0) return 0;
            if (!_contexts.TryGetValue(parentContextId, out var parent)) return 0;

            if (frame > LastFrame) LastFrame = frame;

            originSource = NormalizeOrigin(originSource, sourceActorId);
            originTarget = NormalizeOrigin(originTarget, targetActorId);

            var id = NextId();
            var rootId = parent.RootId;

            var r = new ContextRecord
            {
                ContextId = id,
                RootId = rootId,
                ParentId = parentContextId,
                Kind = kind,
                ConfigId = configId,
                SourceActorId = sourceActorId,
                TargetActorId = targetActorId,
                OriginSource = originSource ?? parent.OriginSource ?? parent.SourceActorId,
                OriginTarget = originTarget ?? parent.OriginTarget ?? parent.TargetActorId,
                CreatedFrame = frame,
                EndedFrame = 0,
                EndReason = EffectSourceEndReason.None,
            };

            _contexts[id] = r;

            if (!_childrenByParent.TryGetValue(parentContextId, out var list))
            {
                list = new List<long>(2);
                _childrenByParent[parentContextId] = list;
            }
            list.Add(id);

            TouchRoot(rootId, frame);
            if (_roots.TryGetValue(rootId, out var root))
            {
                root.ActiveCount++;
            }
            else
            {
                _roots[rootId] = new RootRecord { ActiveCount = 1, ExternalRefCount = 0, LastTouchedFrame = frame };
            }

            return id;
        }

        public bool TryGetSnapshot(long contextId, out EffectSourceSnapshot snapshot)
        {
            if (!Enabled)
            {
                snapshot = default;
                return false;
            }

            if (_contexts.TryGetValue(contextId, out var r))
            {
                snapshot = new EffectSourceSnapshot(
                    contextId: r.ContextId,
                    rootId: r.RootId,
                    parentId: r.ParentId,
                    kind: r.Kind,
                    configId: r.ConfigId,
                    sourceActorId: r.SourceActorId,
                    targetActorId: r.TargetActorId,
                    createdFrame: r.CreatedFrame,
                    endedFrame: r.EndedFrame,
                    endReason: r.EndReason);
                return true;
            }

            snapshot = default;
            return false;
        }

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

        public bool TryGetRootState(long rootId, out RootState state)
        {
            if (!Enabled)
            {
                state = default;
                return false;
            }

            if (rootId <= 0)
            {
                state = default;
                return false;
            }

            if (_roots.TryGetValue(rootId, out var r) && r != null)
            {
                state = new RootState(rootId, r.ActiveCount, r.ExternalRefCount, r.LastTouchedFrame);
                return true;
            }

            state = default;
            return false;
        }

        public readonly struct RootStats
        {
            public readonly long RootId;
            public readonly int SubtreeNodeCount;
            public readonly int ActiveNodeCount;
            public readonly int OldestActiveCreatedFrame;

            public readonly int ActiveCount;
            public readonly int ExternalRefCount;
            public readonly int LastTouchedFrame;

            public RootStats(
                long rootId,
                int subtreeNodeCount,
                int activeNodeCount,
                int oldestActiveCreatedFrame,
                int activeCount,
                int externalRefCount,
                int lastTouchedFrame)
            {
                RootId = rootId;
                SubtreeNodeCount = subtreeNodeCount;
                ActiveNodeCount = activeNodeCount;
                OldestActiveCreatedFrame = oldestActiveCreatedFrame;
                ActiveCount = activeCount;
                ExternalRefCount = externalRefCount;
                LastTouchedFrame = lastTouchedFrame;
            }
        }

        public int LastFrame { get; private set; }

        public bool TryGetContextCreatedFrame(long contextId, out int createdFrame)
        {
            if (!Enabled)
            {
                createdFrame = default;
                return false;
            }

            if (contextId <= 0)
            {
                createdFrame = default;
                return false;
            }

            if (_contexts.TryGetValue(contextId, out var r) && r != null)
            {
                createdFrame = r.CreatedFrame;
                return true;
            }

            createdFrame = default;
            return false;
        }

        public bool TryGetRootStats(long rootId, out RootStats stats)
        {
            if (!Enabled)
            {
                stats = default;
                return false;
            }

            if (rootId <= 0)
            {
                stats = default;
                return false;
            }

            if (!_roots.TryGetValue(rootId, out var root) || root == null)
            {
                stats = default;
                return false;
            }

            var nodeCount = 0;
            var activeNodeCount = 0;
            var oldestActiveCreatedFrame = int.MaxValue;

            Walk(rootId);

            if (oldestActiveCreatedFrame == int.MaxValue) oldestActiveCreatedFrame = 0;

            stats = new RootStats(
                rootId: rootId,
                subtreeNodeCount: nodeCount,
                activeNodeCount: activeNodeCount,
                oldestActiveCreatedFrame: oldestActiveCreatedFrame,
                activeCount: root.ActiveCount,
                externalRefCount: root.ExternalRefCount,
                lastTouchedFrame: root.LastTouchedFrame);

            return true;

            void Walk(long id)
            {
                if (!_contexts.TryGetValue(id, out var r) || r == null) return;

                nodeCount++;
                if (r.EndedFrame == 0)
                {
                    activeNodeCount++;
                    if (r.CreatedFrame < oldestActiveCreatedFrame) oldestActiveCreatedFrame = r.CreatedFrame;
                }

                if (_childrenByParent.TryGetValue(id, out var children) && children != null)
                {
                    for (int i = 0; i < children.Count; i++)
                    {
                        Walk(children[i]);
                    }
                }
            }
        }

        public bool TryGetChildren(long parentContextId, out List<long> children)
        {
            if (!Enabled)
            {
                children = null;
                return false;
            }

            if (parentContextId <= 0)
            {
                children = null;
                return false;
            }

            if (_childrenByParent.TryGetValue(parentContextId, out var list) && list != null)
            {
                children = list;
                return true;
            }

            children = null;
            return false;
        }

        public bool TryBuildChain(long contextId, List<EffectSourceSnapshot> chain)
        {
            if (!Enabled) return false;
            if (contextId <= 0) return false;
            if (chain == null) return false;

            chain.Clear();

            var cur = contextId;
            var guard = 0;
            while (cur > 0 && guard++ < 1024)
            {
                if (!_contexts.TryGetValue(cur, out var r) || r == null) break;

                chain.Add(new EffectSourceSnapshot(
                    contextId: r.ContextId,
                    rootId: r.RootId,
                    parentId: r.ParentId,
                    kind: r.Kind,
                    configId: r.ConfigId,
                    sourceActorId: r.SourceActorId,
                    targetActorId: r.TargetActorId,
                    createdFrame: r.CreatedFrame,
                    endedFrame: r.EndedFrame,
                    endReason: r.EndReason));

                cur = r.ParentId;
            }

            return chain.Count > 0;
        }

        public bool End(long contextId, int frame, EffectSourceEndReason reason)
        {
            if (!Enabled) return false;
            if (contextId <= 0) return false;
            if (!_contexts.TryGetValue(contextId, out var r)) return false;
            if (r.EndedFrame > 0) return false;

            if (frame > LastFrame) LastFrame = frame;

            r.EndedFrame = frame;
            r.EndReason = reason;

            TouchRoot(r.RootId, frame);
            if (_roots.TryGetValue(r.RootId, out var root))
            {
                if (root.ActiveCount > 0)
                {
                    root.ActiveCount--;
                }
                else
                {
                    root.ActiveCount = 0;
                }
            }

            return true;
        }

        public void RetainRoot(long rootId, int frame)
        {
            if (!Enabled) return;
            if (rootId <= 0) return;
            if (!_roots.TryGetValue(rootId, out var root))
            {
                root = new RootRecord();
                _roots[rootId] = root;
            }

            root.ExternalRefCount++;
            root.LastTouchedFrame = frame;
        }

        public void ReleaseRoot(long rootId, int frame)
        {
            if (!Enabled) return;
            if (rootId <= 0) return;
            if (!_roots.TryGetValue(rootId, out var root)) return;

            if (root.ExternalRefCount > 0) root.ExternalRefCount--;
            root.LastTouchedFrame = frame;
        }

        public int Purge(int nowFrame, int keepEndedFrames = 600, int maxRoots = 4096)
        {
            if (!Enabled) return 0;
            var purged = 0;

            if (_roots.Count == 0) return 0;

            var toRemove = new List<long>();
            foreach (var kv in _roots)
            {
                var rootId = kv.Key;
                var root = kv.Value;

                if (root.ExternalRefCount > 0) continue;
                if (root.ActiveCount > 0) continue;

                var age = nowFrame - root.LastTouchedFrame;
                if (age < keepEndedFrames && _roots.Count <= maxRoots) continue;

                toRemove.Add(rootId);
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                purged += RemoveRoot(toRemove[i]);
            }

            return purged;
        }

        private int RemoveRoot(long rootId)
        {
            var removed = 0;

            if (!_contexts.TryGetValue(rootId, out var root))
            {
                _roots.Remove(rootId);
                return 0;
            }

            RemoveSubtree(rootId);
            _roots.Remove(rootId);
            _rootBlackboards.Remove(rootId);

            return removed;

            void RemoveSubtree(long id)
            {
                if (_childrenByParent.TryGetValue(id, out var children))
                {
                    for (int i = 0; i < children.Count; i++)
                    {
                        RemoveSubtree(children[i]);
                    }
                    _childrenByParent.Remove(id);
                }

                if (_contexts.Remove(id))
                {
                    removed++;
                }
            }
        }

        private void TouchRoot(long rootId, int frame)
        {
            if (_roots.TryGetValue(rootId, out var root))
            {
                root.LastTouchedFrame = frame;
            }
        }

        private static object NormalizeOrigin(object origin, int fallbackActorId)
        {
            if (origin == null) return fallbackActorId;

            if (origin is int || origin is long || origin is string)
            {
                return origin;
            }

            if (StoreOriginObjects)
            {
                return origin;
            }

            return fallbackActorId;
        }

        private long NextId()
        {
            return _nextId++;
        }

        public void Dispose()
        {
#if UNITY_EDITOR
            try
            {
                EffectSourceLiveRegistry.Unregister(this);
            }
            catch
            {
            }
#endif
            _contexts.Clear();
            _roots.Clear();
            _childrenByParent.Clear();
            _rootBlackboards.Clear();
        }
    }
}
