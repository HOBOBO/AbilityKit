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
}