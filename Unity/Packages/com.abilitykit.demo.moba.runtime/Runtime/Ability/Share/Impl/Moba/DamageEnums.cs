namespace AbilityKit.Ability.Share.Impl.Moba
{
    /// <summary>
    /// 伤害类型枚举
    /// </summary>
    public enum DamageType : byte
    {
        None = 0,
        Physical = 1,
        Magic = 2,
        True = 4,
    }

    /// <summary>
    /// 暴击类型枚举
    /// </summary>
    public enum CritType : byte
    {
        None = 0,
        Critical = 1,
    }

    /// <summary>
    /// 伤害原因类型枚举
    /// </summary>
    public enum DamageReasonKind : byte
    {
        None = 0,
        Skill = 1,
        BasicAttack = 2,
        Buff = 3,
        Item = 4,
        Environment = 5,
    }

    /// <summary>
    /// 伤害公式类型枚举
    /// </summary>
    public enum DamageFormulaKind : byte
    {
        None = 0,
        Standard = 1,
    }
}