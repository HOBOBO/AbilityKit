namespace AbilityKit.Ability.Impl.Moba
{
    public enum EffectSourceKind
    {
        None = 0,
        SkillCast = 1,
        Buff = 2,
        Effect = 3,
        TriggerAction = 4,
        System = 5,
    }

    public enum EffectSourceEndReason
    {
        None = 0,
        Completed = 1,
        Cancelled = 2,
        Expired = 3,
        Dispelled = 4,
        Dead = 5,
        Replaced,
    }
}
