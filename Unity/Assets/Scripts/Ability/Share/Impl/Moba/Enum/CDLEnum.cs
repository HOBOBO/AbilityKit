namespace AbilityKit.Ability.Impl.Moba
{
    // 实体主类型（用于逻辑层区分不同大类实体）
    public enum EntityMainType
    {
        // 未定义
        None = 0,

        // 单位（英雄/小兵/野怪/防御塔等）
        Unit = 1,

        // 投射物（子弹/飞行道具/技能抛射体）
        Projectile = 2,

        // 场景交互物（地形触发器/可拾取物/机关等）
        SceneObject = 3,

        // 召唤物（由英雄/技能创建的临时单位）
        Summon = 4,

        // 技能效果实体（例如持续AOE区域、陷阱区域等）
        Effect = 5
    }

    // 单位子类型（当主类型为 Unit 时，用于更细的分类）
    public enum UnitSubType
    {
        // 未定义
        None = 0,

        // 英雄
        Hero = 1,

        // 小兵
        Minion = 2,

        // 野怪
        Neutral = 3,

        // 首领/精英怪
        Boss = 4,

        // 防御塔
        Tower = 5,

        // 基地/水晶
        Base = 6
    }

    // 队伍/阵营类型
    public enum Team
    {
        None = 0,
        Team1 = 1,
        Team2 = 2,
        Neutral = 3,
    }

    // 技能槽位类型（普通攻击+多个技能槽）
    public enum SkillSlot
    {
        None = 0,
        BasicAttack = 1,
        Skill1 = 2,
        Skill2 = 3,
        Skill3 = 4,
        Skill4 = 5,
        Skill5 = 6,
    }

    // 技能类型（用于技能系统/冷却/施法流程区分）
    public enum SkillType
    {
        // 未定义
        None = 0,

        // 普通攻击
        NormalAttack = 1,

        // 主动技能（需要玩家触发）
        Active = 2,

        // 被动技能（常驻/触发型）
        Passive = 3,

        // 终极技能
        Ultimate = 4
    }

    public enum BattleAttributeType
    {
        // 未定义
        None = 0,

        // 当前生命
        HP = 1,

        // 最大生命
        MAX_HP = 2,

        // 额外生命（加成值）
        EXTRA_HP = 3,

        // 物理攻击
        PHYSICS_ATTACK = 4,

        // 法术攻击
        MAGIC_ATTACK = 5,

        // 额外物理攻击（加成值）
        EXTRA_PHYSICS_ATTACK = 6,

        // 额外法术攻击（加成值）
        EXTRA_MAGIC_ATTACK = 7,

        // 物理防御
        PHYSICS_DEFENSE = 8,

        // 法术防御
        MAGIC_DEFENSE = 9,

        // 当前法力
        MANA = 10,

        // 最大法力
        MAX_MANA = 11,

        // 暴击率
        CRITICAL_R = 12,

        // 攻速加成
        ATTACK_SPEED_R = 13,

        // 冷却缩减
        COOLDOWN_REDUCE_R = 14,

        // 物理穿透
        PHYSICS_PENETRATION_R = 15,

        // 法术穿透
        MAGIC_PENETRATION_R = 16,

        // 移动速度
        MOVE_SPEED = 17,

        // 物理吸血
        PHYSICS_BLOODSUCKING_R = 18,

        // 法术吸血
        MAGIC_BLOODSUCKING_R = 19,

        // 攻击范围
        ATTACK_RANGE = 20,

        // 每秒生命回复
        PER_SECOND_BLOOD_R = 21,

        // 每秒法力回复
        PER_SECOND_MANA_R = 22,
        /// <summary>
        /// 韧性
        /// </summary>
        RESILIENCE_R = 23,
    }

    public enum BuffStackingPolicy
    {
        None = 0,
        IgnoreIfExists = 1,
        Replace = 2,
        AddStack = 3,
        RefreshDuration = 4,
    }

    public enum BuffRefreshPolicy
    {
        None = 0,
        KeepRemaining = 1,
        ResetRemaining = 2,
        AddRemaining = 3,
    }

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
        Replaced
    }

    public enum EffectExecuteMode
    {
        InternalOnly = 0,
        PublishEventOnly = 1,
        InternalThenPublishEvent = 2,
    }
}