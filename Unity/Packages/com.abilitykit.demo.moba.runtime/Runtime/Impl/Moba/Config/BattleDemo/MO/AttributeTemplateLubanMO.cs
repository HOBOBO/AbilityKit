using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo.MO
{
    /// <summary>
    /// 基于 Luban 二进制配置的属性模板 MO
    /// </summary>
    public sealed class AttributeTemplateLubanMO
    {
        /// <summary>
        /// 模板编号
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// 升级战斗属性方案编号
        /// </summary>
        public int UpgradeCode { get; }

        /// <summary>
        /// 主动技能列表
        /// </summary>
        public IReadOnlyList<int> ActiveSkills { get; }

        /// <summary>
        /// 被动技能列表
        /// </summary>
        public IReadOnlyList<int> PassiveSkills { get; }

        /// <summary>
        /// 生命值
        /// </summary>
        public int Hp { get; }

        /// <summary>
        /// 最大生命值
        /// </summary>
        public int MaxHp { get; }

        /// <summary>
        /// 额外生命值
        /// </summary>
        public int ExtraHp { get; }

        /// <summary>
        /// 物理攻击
        /// </summary>
        public int PhysicsAttack { get; }

        /// <summary>
        /// 法术攻击
        /// </summary>
        public int MagicAttack { get; }

        /// <summary>
        /// 额外物理攻击
        /// </summary>
        public int ExtraPhysicsAttack { get; }

        /// <summary>
        /// 额外法术攻击
        /// </summary>
        public int ExtraMagicAttack { get; }

        /// <summary>
        /// 物理防御
        /// </summary>
        public int PhysicsDefense { get; }

        /// <summary>
        /// 法术防御
        /// </summary>
        public int MagicDefense { get; }

        /// <summary>
        /// 法力值
        /// </summary>
        public int Mana { get; }

        /// <summary>
        /// 最大法力值
        /// </summary>
        public int MaxMana { get; }

        /// <summary>
        /// 暴击率
        /// </summary>
        public int CriticalR { get; }

        /// <summary>
        /// 攻速倍率
        /// </summary>
        public int AttackSpeedR { get; }

        /// <summary>
        /// 冷却缩减
        /// </summary>
        public int CooldownReduceR { get; }

        /// <summary>
        /// 物理穿透
        /// </summary>
        public int PhysicsPenetrationR { get; }

        /// <summary>
        /// 法术穿透
        /// </summary>
        public int MagicPenetrationR { get; }

        /// <summary>
        /// 移动速度
        /// </summary>
        public int MoveSpeed { get; }

        /// <summary>
        /// 物理吸血
        /// </summary>
        public int PhysicsBloodsuckingR { get; }

        /// <summary>
        /// 法术吸血
        /// </summary>
        public int MagicBloodsuckingR { get; }

        /// <summary>
        /// 攻击范围
        /// </summary>
        public int AttackRange { get; }

        /// <summary>
        /// 每秒回血
        /// </summary>
        public int PerSecondBloodR { get; }

        /// <summary>
        /// 每秒回蓝
        /// </summary>
        public int PerSecondManaR { get; }

        /// <summary>
        /// 韧性
        /// </summary>
        public int ResilienceR { get; }

        public AttributeTemplateLubanMO(global::cfg.DRAttributeTemplates dr)
        {
            if (dr == null) throw new ArgumentNullException(nameof(dr));
            Id = dr.Code;
            UpgradeCode = dr.UpgradeCode;
            ActiveSkills = dr.ActiveSkills ?? new List<int>();
            PassiveSkills = dr.PassiveSkills ?? new List<int>();
            Hp = dr.Hp;
            MaxHp = dr.MaxHp;
            ExtraHp = dr.ExtraHp;
            PhysicsAttack = dr.PhysicsAttack;
            MagicAttack = dr.MagicAttack;
            ExtraPhysicsAttack = dr.ExtraPhysicsAttack;
            ExtraMagicAttack = dr.ExtraMagicAttack;
            PhysicsDefense = dr.PhysicsDefense;
            MagicDefense = dr.MagicDefense;
            Mana = dr.Mana;
            MaxMana = dr.MaxMana;
            CriticalR = dr.CriticalR;
            AttackSpeedR = dr.AttackSpeedR;
            CooldownReduceR = dr.CooldownReduceR;
            PhysicsPenetrationR = dr.PhysicsPenetrationR;
            MagicPenetrationR = dr.MagicPenetrationR;
            MoveSpeed = dr.MoveSpeed;
            PhysicsBloodsuckingR = dr.PhysicsBloodsuckingR;
            MagicBloodsuckingR = dr.MagicBloodsuckingR;
            AttackRange = dr.AttackRange;
            PerSecondBloodR = dr.PerSecondBloodR;
            PerSecondManaR = dr.PerSecondManaR;
            ResilienceR = dr.ResilienceR;
        }
    }
}
