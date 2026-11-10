using System;
using AbilityKit.Demo.Moba.Gameplay;

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
        public readonly int HfsmParameterId;
        public readonly bool ClearHfsmSourceOnThreshold;
        public readonly bool PreserveThreshold;
        public readonly double HfsmValueScale;
        public readonly double HfsmValueOffset;

        public AdvanceGameplayCounterArgs(
            [MobaPlanActionArg(MobaPlanActionArgKind.Int, 0d, true, "key_id", "keyid", "id", DisplayName = "key_id")] int keyId,
            [MobaPlanActionArg(MobaPlanActionArgKind.Int, 0d, true, "scope_payload_field_id", "scopePayloadFieldId", "scope_field_id", "field_id", DisplayName = "scope_payload_field_id")] int scopePayloadFieldId,
            [MobaPlanActionArg(MobaPlanActionArgKind.Float, 0d, true, "threshold", "limit", "count")] double threshold,
            [MobaPlanActionArg(MobaPlanActionArgKind.Float, 1d, false, "delta", "step", "amount")] double delta,
            [MobaPlanActionArg(MobaPlanActionArgKind.Float, 0d, false, "reset_value", "resetValue", "reset")] double resetValue,
            [MobaPlanActionArg(MobaPlanActionArgKind.Int, 0d, true, "trigger_id", "triggerId", "on_threshold_trigger_id", "onThresholdTriggerId", DisplayName = "trigger_id")] int triggerId,
            [MobaPlanActionArg(MobaPlanActionArgKind.Int, 0d, false, "hfsm_parameter_id", "parameter_id", DisplayName = "hfsm_parameter_id")] int hfsmParameterId = 0,
            [MobaPlanActionArg(MobaPlanActionArgKind.Bool, 0d, false, "clear_hfsm_source_on_threshold", "clearHfsmSourceOnThreshold", DisplayName = "clear_hfsm_source_on_threshold")] bool clearHfsmSourceOnThreshold = false,
            [MobaPlanActionArg(MobaPlanActionArgKind.Bool, 0d, false, "preserve_threshold", "preserveThreshold", DisplayName = "preserve_threshold")] bool preserveThreshold = false,
            [MobaPlanActionArg(MobaPlanActionArgKind.Float, 1d, false, "hfsm_value_scale")] double hfsmValueScale = 1d,
            [MobaPlanActionArg(MobaPlanActionArgKind.Float, 0d, false, "hfsm_value_offset")] double hfsmValueOffset = 0d)
        {
            KeyId = keyId;
            ScopePayloadFieldId = scopePayloadFieldId;
            Threshold = threshold;
            Delta = delta;
            ResetValue = resetValue;
            TriggerId = triggerId;
            HfsmParameterId = hfsmParameterId;
            ClearHfsmSourceOnThreshold = clearHfsmSourceOnThreshold;
            PreserveThreshold = preserveThreshold;
            if (double.IsNaN(hfsmValueScale) || double.IsInfinity(hfsmValueScale) ||
                double.IsNaN(hfsmValueOffset) || double.IsInfinity(hfsmValueOffset))
                throw new ArgumentOutOfRangeException(nameof(hfsmValueScale));
            HfsmValueScale = hfsmValueScale;
            HfsmValueOffset = hfsmValueOffset;
        }

        public int ResolveScopedKey(int scopeValue)
        {
            return MobaGameplayVariableKeys.ResolveScopedKey(KeyId, scopeValue);
        }
    }
}
