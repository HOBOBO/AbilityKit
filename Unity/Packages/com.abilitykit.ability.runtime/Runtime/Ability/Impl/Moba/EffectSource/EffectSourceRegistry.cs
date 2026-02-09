using System;
using System.Collections.Generic;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.World.Services;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Eventing;

namespace AbilityKit.Ability.Impl.Moba.EffectSource
{
    public sealed partial class EffectSourceRegistry : IService
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

        public static class RootIntKeys
        {
            public static readonly IntKey EffectSourceKind = new IntKey(StableStringId.Get("effectsource.kind"));

            public static readonly IntKey SkillId = new IntKey(StableStringId.Get("skill.id"));
            public static readonly IntKey SkillSlot = new IntKey(StableStringId.Get("skill.slot"));
            public static readonly IntKey SkillLevel = new IntKey(StableStringId.Get("skill.level"));
            public static readonly IntKey SkillSequence = new IntKey(StableStringId.Get("skill.sequence"));

            public static readonly IntKey SkillCasterActorId = new IntKey(StableStringId.Get("skill.caster.actorId"));
            public static readonly IntKey SkillTargetActorId = new IntKey(StableStringId.Get("skill.target.actorId"));
        }

        public readonly struct SkillRootMeta
        {
            public readonly int SkillId;
            public readonly int SkillSlot;
            public readonly int SkillLevel;
            public readonly int Sequence;
            public readonly int CasterActorId;
            public readonly int TargetActorId;

            public SkillRootMeta(int skillId, int skillSlot, int skillLevel, int sequence, int casterActorId, int targetActorId)
            {
                SkillId = skillId;
                SkillSlot = skillSlot;
                SkillLevel = skillLevel;
                Sequence = sequence;
                CasterActorId = casterActorId;
                TargetActorId = targetActorId;
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
            catch (Exception ex)
            {
                AbilityKit.Ability.Share.Common.Log.Log.Exception(ex, "[EffectSourceRegistry] EffectSourceLiveRegistry.Register failed");
            }
#endif
        }

        public int ContextCount => _contexts.Count;
        public int RootCount => _roots.Count;

        public int LastFrame { get; private set; }

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
            catch (Exception ex)
            {
                AbilityKit.Ability.Share.Common.Log.Log.Exception(ex, "[EffectSourceRegistry] EffectSourceLiveRegistry.Unregister failed");
            }
#endif
            _contexts.Clear();
            _roots.Clear();
            _childrenByParent.Clear();
            _rootBlackboards.Clear();
        }
    }
}
