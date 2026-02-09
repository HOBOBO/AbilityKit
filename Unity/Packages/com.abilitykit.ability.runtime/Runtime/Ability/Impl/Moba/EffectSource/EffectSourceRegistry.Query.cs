using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Impl.Moba.EffectSource
{
    public sealed partial class EffectSourceRegistry
    {
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
    }
}
