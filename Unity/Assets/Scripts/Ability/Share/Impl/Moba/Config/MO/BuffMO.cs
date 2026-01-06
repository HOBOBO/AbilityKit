using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO
{
    public sealed class BuffMO
    {
        public int Id { get; }
        public string Name { get; }
        public int DurationMs { get; }
        public IReadOnlyList<int> Tags { get; }

        public BuffMO(global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BuffDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            Id = dto.Id;
            Name = dto.Name;
            DurationMs = dto.DurationMs;
            Tags = dto.Tags ?? Array.Empty<int>();
        }
    }
}
