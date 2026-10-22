using System;

namespace AbilityKit.Demo.Moba.Share.Config
{
    [Serializable]
    public sealed class SkillLevelTableDTO
    {
        public int Id;
        public SkillLevelDTO[] Levels;
        public string[] ParamNames;
    }

    [Serializable]
    public sealed class SkillLevelDTO
    {
        public int CooldownMs;
        public int Cost;
        public float[] Params;
    }
}
