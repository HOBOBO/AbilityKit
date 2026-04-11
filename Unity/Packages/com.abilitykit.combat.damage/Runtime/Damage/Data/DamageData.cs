using System;

namespace AbilityKit.Combat
{
    /// <summary>
    /// 伤害请求
    /// 表示一次伤害计算的输入数据
    /// </summary>
    public struct DamageRequest
    {
        /// <summary>
        /// 伤害来源（技能、Buff等）
        /// </summary>
        public object Source;

        /// <summary>
        /// 攻击者
        /// </summary>
        public object Attacker;

        /// <summary>
        /// 目标
        /// </summary>
        public object Target;

        /// <summary>
        /// 基础伤害值
        /// </summary>
        public float BaseValue;

        /// <summary>
        /// 伤害类型
        /// </summary>
        public DamageType DamageType;

        /// <summary>
        /// 伤害标志
        /// </summary>
        public DamageFlags Flags;

        /// <summary>
        /// 伤害来源类型
        /// </summary>
        public DamageSourceType SourceType;

        /// <summary>
        /// 创建伤害请求
        /// </summary>
        public static DamageRequest Create(
            object source,
            object attacker,
            object target,
            float baseValue,
            DamageType damageType,
            DamageFlags flags = DamageFlags.None,
            DamageSourceType sourceType = DamageSourceType.Ability)
        {
            return new DamageRequest
            {
                Source = source,
                Attacker = attacker,
                Target = target,
                BaseValue = baseValue,
                DamageType = damageType,
                Flags = flags,
                SourceType = sourceType
            };
        }
    }

    /// <summary>
    /// 伤害计算结果
    /// </summary>
    public struct DamageResult
    {
        /// <summary>
        /// 原始请求
        /// </summary>
        public DamageRequest Request;

        /// <summary>
        /// 原始伤害值
        /// </summary>
        public float RawDamage;

        /// <summary>
        /// 应用护甲前的伤害值
        /// </summary>
        public float PreArmorDamage;

        /// <summary>
        /// 护甲减少的伤害值
        /// </summary>
        public float ArmorReduction;

        /// <summary>
        /// 抗性减少的伤害值
        /// </summary>
        public float ResistReduction;

        /// <summary>
        /// 伤害加成修正后的伤害值
        /// </summary>
        public float BonusDamage;

        /// <summary>
        /// 最终伤害值（应用所有修正后）
        /// </summary>
        public float FinalDamage;

        /// <summary>
        /// 暴击倍数
        /// </summary>
        public float CriticalMultiplier;

        /// <summary>
        /// 是否暴击
        /// </summary>
        public bool IsCritical => (Request.Flags & DamageFlags.Critical) != 0;

        /// <summary>
        /// 伤害溢出的值（超过目标生命值的部分）
        /// </summary>
        public float Overkill;

        /// <summary>
        /// 实际造成伤害（扣除护盾后）
        /// </summary>
        public float ActualDamage;

        /// <summary>
        /// 被护盾吸收的伤害
        /// </summary>
        public float ShieldDamage;

        /// <summary>
        /// 创建空结果
        /// </summary>
        public static DamageResult Empty => new DamageResult { FinalDamage = 0 };

        /// <summary>
        /// 创建结果（便捷方法）
        /// </summary>
        public static DamageResult Create(DamageRequest request)
        {
            return new DamageResult
            {
                Request = request,
                RawDamage = request.BaseValue,
                PreArmorDamage = request.BaseValue
            };
        }
    }
}
