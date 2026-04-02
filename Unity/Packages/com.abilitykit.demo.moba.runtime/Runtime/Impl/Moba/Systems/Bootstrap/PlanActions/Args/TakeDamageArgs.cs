namespace AbilityKit.Ability.Impl.Moba.Systems
{
    /// <summary>
    /// take_damage Action 的强类型参数
    /// </summary>
    public readonly struct TakeDamageArgs
    {
        /// <summary>
        /// 伤害倍率
        /// </summary>
        public readonly float Rate;

        /// <summary>
        /// 伤害原因参数（关联 DamageReasonKind）
        /// </summary>
        public readonly int ReasonParam;

        public TakeDamageArgs(float rate, int reasonParam)
        {
            Rate = rate;
            ReasonParam = reasonParam;
        }

        public static TakeDamageArgs Default => new TakeDamageArgs(1f, 0);
    }
}
