using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO
{
    public sealed class CharacterMO
    {
        public int Id { get; }
        public string Name { get; }
        public int ModelId { get; }
        public int AttributeTemplateId { get; }
        public IReadOnlyList<int> SkillIds { get; }

        public CharacterMO(CharacterDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            Id = dto.Id;
            Name = dto.Name;
            ModelId = dto.ModelId;
            AttributeTemplateId = dto.AttributeTemplateId;
            SkillIds = dto.SkillIds ?? Array.Empty<int>();
        }
    }
}
