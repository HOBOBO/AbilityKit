using System;

namespace AbilityKit.Combat
{
    /// <summary>
    /// 伤害类型
    /// </summary>
    [Flags]
    public enum DamageType
    {
        /// <summary>
        /// 物理伤害
        /// </summary>
        Physical = 1 << 0,

        /// <summary>
        /// 魔法伤害
        /// </summary>
        Magic = 1 << 1,

        /// <summary>
        /// 真实伤害（无视护甲和魔抗）
        /// </summary>
        True = 1 << 2,

        /// <summary>
        /// 混合伤害
        /// </summary>
        Mixed = Physical | Magic,
    }

    /// <summary>
    /// 伤害标志
    /// </summary>
    [Flags]
    public enum DamageFlags
    {
        /// <summary>
        /// 无标志
        /// </summary>
        None = 0,

        /// <summary>
        /// 暴击
        /// </summary>
        Critical = 1 << 0,

        /// <summary>
        /// 穿透护甲
        /// </summary>
        PenetrateArmor = 1 << 1,

        /// <summary>
        /// 穿透魔抗
        /// </summary>
        PenetrateMagicResist = 1 << 2,

        /// <summary>
        /// 忽略伤害减免
        /// </summary>
        IgnoreDamageReduction = 1 << 3,

        /// <summary>
        /// 忽略护盾
        /// </summary>
        IgnoreShield = 1 << 4,

        /// <summary>
        /// 生命偷取
        /// </summary>
        Lifesteal = 1 << 5,

        /// <summary>
        /// 魔法吸血
        /// </summary>
        SpellVamp = 1 << 6,

        /// <summary>
        /// 范围伤害
        /// </summary>
        AreaOfEffect = 1 << 7,

        /// <summary>
        /// DOT（持续伤害）
        /// </summary>
        DamageOverTime = 1 << 8,

        /// <summary>
        /// 反射伤害
        /// </summary>
        Reflected = 1 << 9,

        /// <summary>
        /// 真实打击（无法躲避）
        /// </summary>
        Unavoidable = 1 << 10,
    }

    /// <summary>
    /// 伤害来源类型
    /// </summary>
    public enum DamageSourceType
    {
        /// <summary>
        /// 来自技能
        /// </summary>
        Ability,

        /// <summary>
        /// 来自普通攻击
        /// </summary>
        Attack,

        /// <summary>
        /// 来自物品
        /// </summary>
        Item,

        /// <summary>
        /// 来自环境
        /// </summary>
        Environment,

        /// <summary>
        /// 来自Buff
        /// </summary>
        Buff,

        /// <summary>
        /// 未知来源
        /// </summary>
        Unknown,
    }
}
