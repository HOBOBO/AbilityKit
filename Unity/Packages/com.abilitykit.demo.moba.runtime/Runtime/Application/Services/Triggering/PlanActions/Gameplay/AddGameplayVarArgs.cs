namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    [GenerateMobaPlanActionSchema(AbilityKit.Demo.Moba.Systems.TriggeringConstants.Actions.AddGameplayVar)]
    public readonly struct AddGameplayVarArgs
    {
        public readonly int KeyId;
        public readonly double Delta;

        public AddGameplayVarArgs(
            [MobaPlanActionArg(MobaPlanActionArgKind.Int, 0d, true, "key_id", "keyid", "id", DisplayName = "key_id", Min = 1d)] int keyId,
            [MobaPlanActionArg(MobaPlanActionArgKind.Float, 0d, false, "delta", "value", "amount")] double delta)
        {
            KeyId = keyId;
            Delta = delta;
        }
    }
}
