using System;
using System.Collections.Generic;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Impl.Moba.EffectSource
{
    public sealed class EffectSourceRegistry : IService
    {
        public static bool Enabled { get; set; } = false;

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

        public int ContextCount => _contexts.Count;
        public int RootCount => _roots.Count;

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
            if (originSource != null) r.OriginSource = originSource;
            if (originTarget != null) r.OriginTarget = originTarget;
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

        public bool End(long contextId, int frame, EffectSourceEndReason reason)
        {
            if (!Enabled) return false;
            if (contextId <= 0) return false;
            if (!_contexts.TryGetValue(contextId, out var r)) return false;
            if (r.EndedFrame > 0) return false;

            r.EndedFrame = frame;
            r.EndReason = reason;

            TouchRoot(r.RootId, frame);
            if (_roots.TryGetValue(r.RootId, out var root))
            {
                root.ActiveCount--;
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

        private long NextId()
        {
            return _nextId++;
        }

        public void Dispose()
        {
            _contexts.Clear();
            _roots.Clear();
            _childrenByParent.Clear();
        }
    }
}
