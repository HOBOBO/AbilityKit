using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core;

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    /// <summary>
    /// give_damage Action 的强类型参数
    /// </summary>
    public readonly struct GiveDamageArgs
    {
        /// <summary>
        /// 伤害值
        /// </summary>
        public readonly float DamageValue;

        /// <summary>
        /// 伤害原因参数（关联 DamageReasonKind）
        /// </summary>
        public readonly int ReasonParam;

        /// <summary>
        /// 伤害类型（物理/魔法/真实）
        /// </summary>
        public readonly DamageType DamageType;

        public GiveDamageArgs(float damageValue, int reasonParam, DamageType damageType = DamageType.Physical)
        {
            DamageValue = damageValue;
            ReasonParam = reasonParam;
            DamageType = damageType;
        }

        public static GiveDamageArgs Default => new GiveDamageArgs(0f, 0, DamageType.Physical);
    }
}
