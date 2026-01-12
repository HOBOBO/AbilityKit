using System;
using System.Collections.Generic;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO
{
    public sealed class SkillFlowMO
    {
        public int Id { get; }
        public string Name { get; }
        public IReadOnlyList<SkillPhaseDTO> Phases { get; }

        public SkillFlowMO(SkillFlowDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            Id = dto.Id;
            Name = dto.Name;
            Phases = dto.Phases ?? Array.Empty<SkillPhaseDTO>();
        }
    }
}
