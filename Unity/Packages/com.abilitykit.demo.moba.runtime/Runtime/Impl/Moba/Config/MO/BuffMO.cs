using System;
using System.Collections.Generic;
using AbilityKit.Ability.Impl.Moba;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO
{
    public sealed class BuffMO
    {
        public int Id { get; }
        public string Name { get; }
        public int DurationMs { get; }

        public int OngoingEffectId { get; }

        public IReadOnlyList<int> OnAddEffects { get; }
        public IReadOnlyList<int> OnRemoveEffects { get; }
        public IReadOnlyList<int> OnIntervalEffects { get; }
        public int IntervalMs { get; }
        public BuffStackingPolicy StackingPolicy { get; }
        public BuffRefreshPolicy RefreshPolicy { get; }
        public int MaxStacks { get; }
        public IReadOnlyList<int> TriggerIds { get; }
        public IReadOnlyList<int> Tags { get; }

        public BuffMO(global::cfg.DRBuff dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            Id = dto.Id;
            Name = string.Empty;
            DurationMs = dto.DurationMs;

            OngoingEffectId = dto.OngoingEffectId;

            OnAddEffects = dto.OnAddEffects;
            OnRemoveEffects = dto.OnRemoveEffects;
            OnIntervalEffects = dto.OnIntervalEffects;
            IntervalMs = dto.IntervalMs;
            StackingPolicy = (BuffStackingPolicy)dto.StackingPolicy;
            RefreshPolicy = (BuffRefreshPolicy)dto.RefreshPolicy;
            MaxStacks = dto.MaxStacks;
            TriggerIds = dto.TriggerIds;
            Tags = dto.Tags;
        }
    }
}
