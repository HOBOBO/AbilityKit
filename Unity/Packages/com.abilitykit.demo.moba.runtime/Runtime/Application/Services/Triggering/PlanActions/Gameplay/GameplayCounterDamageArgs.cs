using System;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public readonly struct ConsumeGameplayCounterArgs
    {
        public readonly int KeyId;
        public readonly int ScopePayloadFieldId;
        public readonly double Threshold;
        public readonly double ResetValue;
        public readonly DamageReasonKind ReasonKind;
        public readonly int ReasonParam;
        public readonly int TriggerId;
        public readonly bool PerSkillCast;

        public ConsumeGameplayCounterArgs(int keyId, int scopePayloadFieldId, double threshold,
            double resetValue, DamageReasonKind reasonKind, int reasonParam, int triggerId,
            bool perSkillCast = false)
        {
            KeyId = keyId;
            ScopePayloadFieldId = scopePayloadFieldId;
            Threshold = threshold;
            ResetValue = resetValue;
            ReasonKind = reasonKind;
            ReasonParam = reasonParam;
            TriggerId = triggerId;
            PerSkillCast = perSkillCast;
        }

        public int ResolveScopedKey(int scopeValue)
        {
            unchecked { return (KeyId * 100000) + scopeValue; }
        }
    }

    public readonly struct GameplayCounterDamageArgs
    {
        public readonly int KeyId;
        public readonly int ScopePayloadFieldId;
        public readonly double Threshold;
        public readonly double Value;
        public readonly int SourceId;
        public readonly DamageReasonKind ReasonKind;
        public readonly int ReasonParam;
        public readonly DamageNumberSlot NumberSlot;
        public readonly CombatNumberModifierOp Op;

        public GameplayCounterDamageArgs(int keyId, int scopePayloadFieldId, double threshold,
            double value, int sourceId, DamageReasonKind reasonKind, int reasonParam,
            DamageNumberSlot numberSlot, CombatNumberModifierOp op)
        {
            KeyId = keyId; ScopePayloadFieldId = scopePayloadFieldId; Threshold = threshold;
            Value = value; SourceId = sourceId; ReasonKind = reasonKind; ReasonParam = reasonParam;
            NumberSlot = numberSlot; Op = op;
        }

        public int ResolveScopedKey(int scopeValue)
        {
            unchecked { return (KeyId * 100000) + scopeValue; }
        }
    }
}
