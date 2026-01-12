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
        public int EffectId { get; }
        public BuffStackingPolicy StackingPolicy { get; }
        public BuffRefreshPolicy RefreshPolicy { get; }
        public int MaxStacks { get; }
        public IReadOnlyList<int> Tags { get; }

        public BuffMO(global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BuffDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            Id = dto.Id;
            Name = dto.Name;
            DurationMs = dto.DurationMs;
            EffectId = dto.EffectId;
            StackingPolicy = (BuffStackingPolicy)dto.StackingPolicy;
            RefreshPolicy = (BuffRefreshPolicy)dto.RefreshPolicy;
            MaxStacks = dto.MaxStacks;
            Tags = dto.Tags ?? Array.Empty<int>();
        }
    }
}
