using System;

namespace AbilityKit.Demo.Moba.Share.Config
{
    /// <summary>Effect values for one skill at one champion level.</summary>
    [Serializable]
    public sealed class HeroLevelSkillGrowthParameterDTO
    {
        public int Id;
        public int SkillId;
        public int ChampionLevel;
        public float Damage;
        public float Shield;
        public float DurationMs;
        public float SummonHealth;
        public float SummonDamage;
        public float SummonDurationMs;
    }
}
