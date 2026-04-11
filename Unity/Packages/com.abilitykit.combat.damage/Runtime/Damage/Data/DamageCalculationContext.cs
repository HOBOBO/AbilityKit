using System;
using AbilityKit.Dataflow;

namespace AbilityKit.Combat
{
    /// <summary>
    /// 伤害计算上下文
    /// 继承自 DataflowContext，提供伤害计算专用字段
    /// </summary>
    public class DamageCalculationContext : DataflowContext
    {
        /// <summary>
        /// 伤害请求
        /// </summary>
        public DamageRequest Request { get; set; }

        /// <summary>
        /// 伤害计算结果
        /// </summary>
        public DamageResult Result { get; set; }

        /// <summary>
        /// 目标护甲值
        /// </summary>
        public float TargetArmor { get; set; }

        /// <summary>
        /// 目标魔抗值
        /// </summary>
        public float TargetMagicResist { get; set; }

        /// <summary>
        /// 目标最大生命值
        /// </summary>
        public float TargetMaxHealth { get; set; }

        /// <summary>
        /// 目标当前生命值
        /// </summary>
        public float TargetCurrentHealth { get; set; }

        /// <summary>
        /// 攻击者物理攻击力
        /// </summary>
        public float AttackerPhysicalDamage { get; set; }

        /// <summary>
        /// 攻击者魔法攻击力
        /// </summary>
        public float AttackerMagicDamage { get; set; }

        /// <summary>
        /// 重置上下文状态
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            Request = default;
            Result = default;
            TargetArmor = 0;
            TargetMagicResist = 0;
            TargetMaxHealth = 0;
            TargetCurrentHealth = 0;
            AttackerPhysicalDamage = 0;
            AttackerMagicDamage = 0;
        }
    }
}
