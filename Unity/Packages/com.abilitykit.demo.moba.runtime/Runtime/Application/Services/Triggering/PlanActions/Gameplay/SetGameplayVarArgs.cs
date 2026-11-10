using AbilityKit.Demo.Moba.Gameplay;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public readonly struct SetGameplayVarArgs
    {
        public readonly int KeyId;
        public readonly double Value;
        public readonly int ScopePayloadFieldId;

        public SetGameplayVarArgs(int keyId, double value, int scopePayloadFieldId = 0)
        {
            KeyId = keyId;
            Value = value;
            ScopePayloadFieldId = scopePayloadFieldId;
        }

        public int ResolveKey(int scopeId)
        {
            return ScopePayloadFieldId == 0
                ? KeyId
                : MobaGameplayVariableKeys.ResolveScopedKey(KeyId, scopeId);
        }
    }
}
