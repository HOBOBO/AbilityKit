using System;
using System.Collections.Generic;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.CO;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO
{
    public sealed class CharacterMO
    {
        public int Id { get; }
        public string Name { get; }
        public int ModelId { get; }
        public int AttributeTemplateId { get; }
        public IReadOnlyList<int> SkillIds { get; }

        public CharacterMO(ICharacterCO co)
        {
            if (co == null) throw new ArgumentNullException(nameof(co));
            Id = co.Key;
            Name = co.Name;
            ModelId = co.ModelId;
            AttributeTemplateId = co.AttributeTemplateId;
            SkillIds = co.SkillIds ?? Array.Empty<int>();
        }

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
