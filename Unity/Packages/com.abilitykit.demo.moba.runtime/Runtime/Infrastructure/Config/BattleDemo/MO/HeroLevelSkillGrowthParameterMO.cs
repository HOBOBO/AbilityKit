using System;
using AbilityKit.Demo.Moba.Share.Config;

namespace AbilityKit.Demo.Moba.Config.BattleDemo.MO
{
    public sealed class HeroLevelSkillGrowthParameterMO
    {
        public int Id { get; }
        public int SkillId { get; }
        public int ChampionLevel { get; }
        public float Damage { get; }
        public float Shield { get; }
        public float DurationMs { get; }
        public float SummonHealth { get; }
        public float SummonDamage { get; }
        public float SummonDurationMs { get; }

        public HeroLevelSkillGrowthParameterMO(HeroLevelSkillGrowthParameterDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            Id = dto.Id;
            SkillId = dto.SkillId;
            ChampionLevel = dto.ChampionLevel;
            Damage = dto.Damage;
            Shield = dto.Shield;
            DurationMs = dto.DurationMs;
            SummonHealth = dto.SummonHealth;
            SummonDamage = dto.SummonDamage;
            SummonDurationMs = dto.SummonDurationMs;
        }

        public bool TryGetValue(string column, out float value)
        {
            value = 0f;
            if (string.IsNullOrWhiteSpace(column)) return false;
            switch (column.Trim().ToLowerInvariant())
            {
                case "damage": value = Damage; return true;
                case "shield": value = Shield; return true;
                case "durationms": value = DurationMs; return true;
                case "summonhealth": value = SummonHealth; return true;
                case "summondamage": value = SummonDamage; return true;
                case "summondurationms": value = SummonDurationMs; return true;
                default: return false;
            }
        }
    }
}
