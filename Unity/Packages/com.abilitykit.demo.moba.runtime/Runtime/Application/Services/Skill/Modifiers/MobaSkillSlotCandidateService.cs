using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;

namespace AbilityKit.Demo.Moba.Services
{
    public readonly struct MobaResolvedSlotSkill
    {
        public MobaResolvedSlotSkill(int slot, int baseSkillId, int effectiveSkillId,
            int providerSourceId, int revision)
        {
            Slot = slot;
            BaseSkillId = baseSkillId;
            EffectiveSkillId = effectiveSkillId;
            ProviderSourceId = providerSourceId;
            Revision = revision;
        }

        public int Slot { get; }
        public int BaseSkillId { get; }
        public int EffectiveSkillId { get; }
        public int ProviderSourceId { get; }
        public int Revision { get; }
    }

    [WorldService(typeof(MobaSkillSlotCandidateService))]
    public sealed class MobaSkillSlotCandidateService : IService
    {
        private readonly Dictionary<int, List<Candidate>> _byOwner = new Dictionary<int, List<Candidate>>();
        private int _revision;

        internal void Add(int ownerActorId, int sourceId, int slot, int skillId, int priority, int order)
        {
            if (ownerActorId <= 0 || sourceId == 0 || slot <= 0 || skillId <= 0)
                throw new ArgumentOutOfRangeException(nameof(skillId));
            if (!_byOwner.TryGetValue(ownerActorId, out var candidates))
                _byOwner[ownerActorId] = candidates = new List<Candidate>(4);
            candidates.Add(new Candidate(sourceId, slot, skillId, priority, order));
            _revision++;
        }

        internal void ClearSource(int ownerActorId, int sourceId)
        {
            if (!_byOwner.TryGetValue(ownerActorId, out var candidates)) return;
            if (candidates.RemoveAll(item => item.SourceId == sourceId) == 0) return;
            if (candidates.Count == 0) _byOwner.Remove(ownerActorId);
            _revision++;
        }

        [MobaModifierConsumes(MobaSkillParamModifierKeys.Skill.Ids.SkillId,
            MobaParameterStage.CastPrepare, MobaModifierSampleTiming.DerivedView)]
        public MobaResolvedSlotSkill Resolve(int ownerActorId, int slot, int baseSkillId)
        {
            if (ownerActorId <= 0 || slot <= 0 || baseSkillId <= 0)
                return new MobaResolvedSlotSkill(slot, baseSkillId, baseSkillId, 0, _revision);
            Candidate? best = null;
            if (_byOwner.TryGetValue(ownerActorId, out var candidates))
            {
                foreach (var candidate in candidates)
                {
                    if (candidate.Slot != slot) continue;
                    if (!best.HasValue || IsBetter(candidate, best.Value)) best = candidate;
                }
            }
            return best.HasValue
                ? new MobaResolvedSlotSkill(slot, baseSkillId, best.Value.SkillId,
                    best.Value.SourceId, _revision)
                : new MobaResolvedSlotSkill(slot, baseSkillId, baseSkillId, 0, _revision);
        }

        private static bool IsBetter(in Candidate candidate, in Candidate current)
        {
            if (candidate.Priority != current.Priority) return candidate.Priority > current.Priority;
            if (candidate.Order != current.Order) return candidate.Order < current.Order;
            return candidate.SourceId < current.SourceId;
        }

        public void Dispose() => _byOwner.Clear();

        private readonly struct Candidate
        {
            public Candidate(int sourceId, int slot, int skillId, int priority, int order)
            {
                SourceId = sourceId;
                Slot = slot;
                SkillId = skillId;
                Priority = priority;
                Order = order;
            }

            public int SourceId { get; }
            public int Slot { get; }
            public int SkillId { get; }
            public int Priority { get; }
            public int Order { get; }
        }
    }
}
