using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba;

namespace AbilityKit.Demo.Moba.Config.BattleDemo.MO
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

        public BuffMO(BuffDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            Id = dto.Id;
            Name = dto.Name;
            DurationMs = dto.DurationMs;

            OngoingEffectId = dto.OngoingEffectId;

            OnAddEffects = dto.OnAddEffects ?? Array.Empty<int>();
            OnRemoveEffects = dto.OnRemoveEffects ?? Array.Empty<int>();
            OnIntervalEffects = dto.OnIntervalEffects ?? Array.Empty<int>();
            IntervalMs = dto.IntervalMs;
            StackingPolicy = (BuffStackingPolicy)dto.StackingPolicy;
            RefreshPolicy = (BuffRefreshPolicy)dto.RefreshPolicy;
            MaxStacks = dto.MaxStacks;
            TriggerIds = dto.TriggerIds ?? Array.Empty<int>();
            Tags = dto.Tags ?? Array.Empty<int>();
        }
    }
}
