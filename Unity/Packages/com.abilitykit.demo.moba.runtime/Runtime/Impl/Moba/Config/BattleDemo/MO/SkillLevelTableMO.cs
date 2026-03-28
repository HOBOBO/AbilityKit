using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo.MO
{
    public sealed class SkillLevelTableMO
    {
        public int Id { get; }
        public IReadOnlyList<SkillLevelDTO> Levels { get; }

        public SkillLevelTableMO(SkillLevelTableDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            Id = dto.Id;
            Levels = dto.Levels ?? Array.Empty<SkillLevelDTO>();
        }

        public SkillLevelDTO GetLevel(int level)
        {
            if (Levels == null || Levels.Count == 0) throw new InvalidOperationException($"SkillLevelTable has no levels. id={Id}");
            if (level <= 0) throw new ArgumentOutOfRangeException(nameof(level));
            var idx = level - 1;
            if ((uint)idx >= (uint)Levels.Count) throw new ArgumentOutOfRangeException(nameof(level));
            return Levels[idx];
        }
    }
}
