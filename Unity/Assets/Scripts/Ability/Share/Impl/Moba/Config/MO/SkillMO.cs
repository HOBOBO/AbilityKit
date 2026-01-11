using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO
{
    public sealed class SkillMO
    {
        public int Id { get; }
        public string Name { get; }
        public int CooldownMs { get; }
        public int Range { get; }
        public int IconId { get; }
        public int Category { get; }
        public int LevelTableId { get; }
        public IReadOnlyList<SkillEffectDTO> Effects { get; }
        public IReadOnlyList<int> Tags { get; }

        public SkillMO(SkillDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            Id = dto.Id;
            Name = dto.Name;
            CooldownMs = dto.CooldownMs;
            Range = dto.Range;
            IconId = dto.IconId;
            Category = dto.Category;
            LevelTableId = dto.LevelTableId;
            Effects = dto.Effects ?? Array.Empty<SkillEffectDTO>();
            Tags = dto.Tags ?? Array.Empty<int>();
        }
    }
}
