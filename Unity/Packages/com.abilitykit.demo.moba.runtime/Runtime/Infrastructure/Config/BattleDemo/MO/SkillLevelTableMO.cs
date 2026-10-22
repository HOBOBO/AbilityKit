using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Share.Config;

namespace AbilityKit.Demo.Moba.Config.BattleDemo.MO
{
    public sealed class SkillLevelTableMO
    {
        public int Id { get; }
        public IReadOnlyList<SkillLevelDTO> Levels { get; }
        public IReadOnlyList<string> ParamNames { get; }

        public SkillLevelTableMO(SkillLevelTableDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            Id = dto.Id;
            Levels = dto.Levels ?? Array.Empty<SkillLevelDTO>();
            ParamNames = dto.ParamNames ?? Array.Empty<string>();
        }

        public SkillLevelDTO GetLevel(int level)
        {
            if (Levels == null || Levels.Count == 0) throw new InvalidOperationException($"SkillLevelTable has no levels. id={Id}");
            if (level <= 0) throw new ArgumentOutOfRangeException(nameof(level));
            var idx = level - 1;
            if ((uint)idx >= (uint)Levels.Count) throw new ArgumentOutOfRangeException(nameof(level));
            return Levels[idx];
        }

        public bool TryGetParameter(int level, string name, out float value)
        {
            value = 0f;
            if (string.IsNullOrWhiteSpace(name) || ParamNames == null) return false;
            for (var i = 0; i < ParamNames.Count; i++)
            {
                if (!string.Equals(ParamNames[i], name, StringComparison.OrdinalIgnoreCase)) continue;
                var row = GetLevel(level);
                if (row?.Params == null || i >= row.Params.Length) return false;
                value = row.Params[i];
                return true;
            }
            return false;
        }
    }
}
