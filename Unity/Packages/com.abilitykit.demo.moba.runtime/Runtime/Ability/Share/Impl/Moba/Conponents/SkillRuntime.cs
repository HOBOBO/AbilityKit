namespace AbilityKit.Ability.Impl.Moba.Conponents
{
    public sealed class ActiveSkillRuntime
    {
        public int SkillId;
        public int Level;
        public long CooldownEndTimeMs;
    }

    public sealed class PassiveSkillRuntime
    {
        public int PassiveSkillId;
        public int Level;
        public long CooldownEndTimeMs;
    }
}
