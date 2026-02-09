using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Eventing;

namespace AbilityKit.Ability.Impl.Moba.EffectSource
{
    public sealed partial class EffectSourceRegistry
    {
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

        public bool HasSkillRootMeta(long rootId)
        {
            if (!Enabled) return false;
            if (rootId <= 0) return false;

            if (!TryGetRootInt(rootId, RootIntKeys.EffectSourceKind, out var kind) || kind != (int)EffectSourceKind.SkillCast)
            {
                return false;
            }

            return TryGetRootInt(rootId, RootIntKeys.SkillId, out var _);
        }

        public bool TryGetSkillRootMeta(long rootId, out SkillRootMeta meta)
        {
            meta = default;
            if (!Enabled) return false;
            if (rootId <= 0) return false;

            if (!TryGetRootInt(rootId, RootIntKeys.EffectSourceKind, out var kind) || kind != (int)EffectSourceKind.SkillCast)
            {
                return false;
            }

            if (!TryGetRootInt(rootId, RootIntKeys.SkillId, out var skillId)) return false;

            TryGetRootInt(rootId, RootIntKeys.SkillSlot, out var slot);
            TryGetRootInt(rootId, RootIntKeys.SkillLevel, out var level);
            TryGetRootInt(rootId, RootIntKeys.SkillSequence, out var seq);
            TryGetRootInt(rootId, RootIntKeys.SkillCasterActorId, out var caster);
            TryGetRootInt(rootId, RootIntKeys.SkillTargetActorId, out var target);

            meta = new SkillRootMeta(skillId, slot, level, seq, caster, target);
            return true;
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
    }
}
