using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Impl.Moba.EffectSource
{
    public sealed partial class EffectSourceRegistry
    {
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
    }
}
