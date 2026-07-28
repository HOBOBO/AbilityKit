namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// MOBA 战斗溯源节点种类枚举
    /// </summary>
    public enum MobaTraceKind : byte
    {
        None = 0,

        // 技能溯源
        SkillCast = 1,
        SkillEffect = 2,
        SkillPhase = 3,

        // 效果溯源
        EffectExecution = 10,
        EffectAction = 11,

        // Buff 溯源
        BuffApply = 20,
        BuffTick = 21,
        BuffRemove = 22,

        // 投射物溯源
        ProjectileLaunch = 30,
        ProjectileHit = 31,

        // 区域溯源
        AreaSpawn = 40,
        AreaEnter = 41,
        AreaExit = 42,
        AreaExpire = 43,
        AreaStay = 44,

        // 召唤物溯源
        SummonSpawn = 50,
        SummonDeath = 51,

        // 单位生命周期溯源
        UnitSpawn = 60,
        UnitDespawn = 61,
        UnitDeath = 62,
        UnitRespawn = 63,

        // 伤害管线溯源
        DamageAttack = 70,
        DamageCalc = 71,
        DamageApply = 72,

        // 表现事件溯源
        PresentationPlay = 80,
        PresentationStop = 81,
    }

    /// <summary>
    /// MOBA 战斗溯源结束原因枚举
    /// </summary>
    public enum MobaTraceEndReason : byte
    {
        None = 0,

        // 通用结束原因
        Completed = 1,
        Interrupted = 2,
        Cancelled = 3,
        Failed = 4,

        // 效果结束原因
        EffectConditionNotMet = 10,
        EffectNoTarget = 11,

        // Buff 结束原因
        BuffExpired = 20,
        BuffDispelled = 21,
        BuffStacksExceeded = 22,

        // 投射物结束原因
        ProjectileExpired = 30,
        ProjectileObstructed = 31,

        // 区域结束原因
        AreaExpired = 40,
        AreaMaxEnterCount = 41,
    }
}
