namespace AbilityKit.Demo.Moba
{
    /// <summary>
    /// 浼ゅ绫诲瀷鏋氫妇
    /// </summary>
    public enum DamageType : byte
    {
        None = 0,
        Physical = 1,
        Magic = 2,
        True = 4,
    }

    /// <summary>
    /// 鏆村嚮绫诲瀷鏋氫妇
    /// </summary>
    public enum CritType : byte
    {
        None = 0,
        Critical = 1,
    }

    /// <summary>
    /// 浼ゅ鍘熷洜绫诲瀷鏋氫妇
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
    /// 浼ゅ鍏紡绫诲瀷鏋氫妇
    /// </summary>
    public enum DamageFormulaKind : byte
    {
        None = 0,
        Standard = 1,
    }
}
