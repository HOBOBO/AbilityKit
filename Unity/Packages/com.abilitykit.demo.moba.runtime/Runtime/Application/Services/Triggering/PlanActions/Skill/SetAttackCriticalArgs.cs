namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    /// <summary>Marks the current attack as a critical hit. The damage pipeline owns the multiplier.</summary>
    [GenerateMobaPlanActionSchema(AbilityKit.Demo.Moba.Systems.TriggeringConstants.Actions.SetAttackCritical)]
    public readonly struct SetAttackCriticalArgs
    {
        public readonly int ReasonKind;
        public readonly int ReasonParam;

        public SetAttackCriticalArgs(
            [MobaPlanActionArg(MobaPlanActionArgKind.Int, 0d, false, "reason_kind", "reasonKind")] int reasonKind = 0,
            [MobaPlanActionArg(MobaPlanActionArgKind.Int, 0d, false, "reason_param", "reasonParam")] int reasonParam = 0)
        {
            ReasonKind = reasonKind;
            ReasonParam = reasonParam;
        }
    }
}
