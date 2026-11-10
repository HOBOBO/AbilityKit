namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    [GenerateMobaPlanActionSchema(AbilityKit.Demo.Moba.Systems.TriggeringConstants.Actions.SetHfsmParameter)]
    public readonly struct SetHfsmParameterArgs
    {
        public readonly int ParameterId;
        public readonly double Value;
        public readonly int Priority;
        public readonly int SourceId;
        public readonly bool Toggle;
        public readonly double AlternateValue;

        public SetHfsmParameterArgs(
            [MobaPlanActionArg(MobaPlanActionArgKind.Int, 0d, true, "parameter_id", "parameterId", "id", DisplayName = "parameter_id", Min = 1d)] int parameterId,
            [MobaPlanActionArg(MobaPlanActionArgKind.Float, 0d, true, "value", "amount")] double value,
            [MobaPlanActionArg(MobaPlanActionArgKind.Int, 0d, false, "priority")] int priority = 0,
            [MobaPlanActionArg(MobaPlanActionArgKind.Int, 0d, false, "source_id", DisplayName = "source_id", Min = 0d)] int sourceId = 0,
            [MobaPlanActionArg(MobaPlanActionArgKind.Bool, 0d, false, "toggle", DisplayName = "toggle")] bool toggle = false,
            [MobaPlanActionArg(MobaPlanActionArgKind.Float, 1d, false, "alternate_value", "alternateValue", DisplayName = "alternate_value")] double alternateValue = 1d)
        {
            ParameterId = parameterId;
            Value = value;
            Priority = priority;
            SourceId = sourceId;
            Toggle = toggle;
            AlternateValue = alternateValue;
        }
    }
}
