using System;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo.MO
{
    [Serializable]
    public sealed class SkillLevelTableDTO
    {
        public int Id;
        public SkillLevelDTO[] Levels;
    }

    [Serializable]
    public sealed class SkillLevelDTO
    {
        public int CooldownMs;
        public int Cost;
        public float[] Params;
    }
}
