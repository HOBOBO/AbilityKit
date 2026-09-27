using System;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    [GenerateMobaPlanActionSchema(AbilityKit.Demo.Moba.Systems.TriggeringConstants.Actions.AdvanceGameplayCounter)]
    public readonly struct AdvanceGameplayCounterArgs
    {
        public readonly int KeyId;
        public readonly int ScopePayloadFieldId;
        public readonly double Threshold;
        public readonly double Delta;
        public readonly double ResetValue;
        public readonly int TriggerId;

        public AdvanceGameplayCounterArgs(
            [MobaPlanActionArg(MobaPlanActionArgKind.Int, 0d, true, "key_id", "keyid", "id", DisplayName = "key_id")] int keyId,
            [MobaPlanActionArg(MobaPlanActionArgKind.Int, 0d, true, "scope_payload_field_id", "scopePayloadFieldId", "scope_field_id", "field_id", DisplayName = "scope_payload_field_id")] int scopePayloadFieldId,
            [MobaPlanActionArg(MobaPlanActionArgKind.Float, 0d, true, "threshold", "limit", "count")] double threshold,
            [MobaPlanActionArg(MobaPlanActionArgKind.Float, 1d, false, "delta", "step", "amount")] double delta,
            [MobaPlanActionArg(MobaPlanActionArgKind.Float, 0d, false, "reset_value", "resetValue", "reset")] double resetValue,
            [MobaPlanActionArg(MobaPlanActionArgKind.Int, 0d, true, "trigger_id", "triggerId", "on_threshold_trigger_id", "onThresholdTriggerId", DisplayName = "trigger_id")] int triggerId)
        {
            KeyId = keyId;
            ScopePayloadFieldId = scopePayloadFieldId;
            Threshold = threshold;
            Delta = delta;
            ResetValue = resetValue;
            TriggerId = triggerId;
        }

        public int ResolveScopedKey(int scopeValue)
        {
            unchecked
            {
                return (KeyId * 100000) + scopeValue;
            }
        }
    }
}
