using System;
using System.Collections.Generic;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.CO;

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
        public IReadOnlyList<int> Tags { get; }

        public SkillMO(ISkillCO co)
        {
            if (co == null) throw new ArgumentNullException(nameof(co));
            Id = co.Key;
            Name = co.Name;
            CooldownMs = co.CooldownMs;
            Range = co.Range;
            IconId = co.IconId;
            Category = co.Category;

            var tags = new List<int>();
            var span = co.Tags;
            for (var i = 0; i < span.Length; i++) tags.Add(span[i]);
            Tags = tags;
        }

        public SkillMO(SkillDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            Id = dto.Id;
            Name = dto.Name;
            CooldownMs = dto.CooldownMs;
            Range = dto.Range;
            IconId = dto.IconId;
            Category = dto.Category;
            Tags = dto.Tags ?? Array.Empty<int>();
        }
    }
}
