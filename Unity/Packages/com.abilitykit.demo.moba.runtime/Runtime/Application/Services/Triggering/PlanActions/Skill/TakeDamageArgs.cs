namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    using AbilityKit.Demo.Moba;
    /// <summary>
    /// take_damage Action 的强类型参数。
    /// </summary>
    [GenerateMobaPlanActionSchema(AbilityKit.Demo.Moba.Systems.TriggeringConstants.Actions.TakeDamage)]
    public readonly struct TakeDamageArgs
    {
        /// <summary>
        /// 伤害倍率。
        /// </summary>
        public readonly float Rate;

        /// <summary>
        /// 伤害原因参数，对应 DamageReasonKind。
        /// </summary>
        public readonly int ReasonParam;

        public TakeDamageArgs(
            [MobaPlanActionArg(MobaPlanActionArgKind.Float, 1d, false, "rate", "damage_rate", "damagerate", Min = 0d)] float rate,
            [MobaPlanActionArg(MobaPlanActionArgKind.Int, 0d, false, "reason_param", "reasonparam")] int reasonParam)
        {
            Rate = rate;
            ReasonParam = reasonParam;
        }

        public static TakeDamageArgs Default => new TakeDamageArgs(1f, 0);
    }
}
