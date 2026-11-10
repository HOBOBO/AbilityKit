using System;
using System.Collections.Generic;
using System.Globalization;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Demo.Moba.Services;

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

        public bool TryCreateParameterSnapshot(int level, int skillId, out MobaSkillLevelParameterSnapshot snapshot, out string error)
        {
            snapshot = MobaSkillLevelParameterSnapshot.Empty;
            error = null;
            if (level <= 0 || Levels == null || level > Levels.Count)
            {
                error = $"Skill level is outside the configured table. skillId={skillId}, levelTableId={Id}, level={level}, levelCount={Levels?.Count ?? 0}.";
                return false;
            }

            var row = Levels[level - 1];
            var names = ParamNames ?? Array.Empty<string>();
            var values = row?.Params ?? Array.Empty<float>();
            if (names.Count == 0)
            {
                if (values.Length != 0)
                {
                    error = $"Skill level row has values but no declared parameter names. skillId={skillId}, levelTableId={Id}, level={level}, valueCount={values.Length}.";
                    return false;
                }
                return true;
            }

            if (values.Length < names.Count)
            {
                error = $"Skill level row is missing named parameter values. skillId={skillId}, levelTableId={Id}, level={level}, parameterCount={names.Count}, valueCount={values.Length}.";
                return false;
            }
            if (values.Length != names.Count)
            {
                error = $"Skill level row parameter values do not match declared names. skillId={skillId}, levelTableId={Id}, level={level}, parameterCount={names.Count}, valueCount={values.Length}.";
                return false;
            }

            var entries = new MobaSkillLevelParameterValue[names.Count];
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < names.Count; i++)
            {
                var name = names[i];
                if (string.IsNullOrWhiteSpace(name))
                {
                    error = $"Skill level table declares an empty parameter name. skillId={skillId}, levelTableId={Id}, level={level}, parameterIndex={i}.";
                    return false;
                }
                if (!seen.Add(name))
                {
                    error = $"Skill level table declares a duplicate parameter name. skillId={skillId}, levelTableId={Id}, level={level}, parameter={name}.";
                    return false;
                }

                var value = values[i];
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    error = $"Skill level parameter is not finite. skillId={skillId}, levelTableId={Id}, level={level}, parameter={name}, value={value.ToString(CultureInfo.InvariantCulture)}.";
                    return false;
                }
                entries[i] = new MobaSkillLevelParameterValue(name, value);
            }

            snapshot = new MobaSkillLevelParameterSnapshot(entries);
            return true;
        }
    }
}
