using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Impl.Moba.EffectSource
{
    public sealed partial class EffectSourceRegistry
    {
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

        public bool EnsureRoot(
            long contextId,
            EffectSourceKind kind,
            int configId,
            int sourceActorId,
            int targetActorId,
            int frame,
            object originSource = null,
            object originTarget = null)
        {
            if (!Enabled) return false;
            if (contextId <= 0) return false;
            if (kind == EffectSourceKind.None) return false;

            if (_contexts.ContainsKey(contextId))
            {
                TouchRoot(contextId, frame);
                return true;
            }

            if (frame > LastFrame) LastFrame = frame;

            originSource = NormalizeOrigin(originSource, sourceActorId);
            originTarget = NormalizeOrigin(originTarget, targetActorId);

            var r = new ContextRecord
            {
                ContextId = contextId,
                RootId = contextId,
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

            _contexts[contextId] = r;
            _roots[contextId] = new RootRecord { ActiveCount = 1, ExternalRefCount = 0, LastTouchedFrame = frame };

            if (contextId >= _nextId)
            {
                _nextId = contextId + 1;
            }

            return true;
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
    }
}
