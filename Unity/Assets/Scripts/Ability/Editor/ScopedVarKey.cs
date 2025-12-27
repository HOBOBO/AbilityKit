using AbilityKit.Triggering;

namespace AbilityKit.Ability.Editor
{
    public readonly struct ScopedVarKey
    {
        public ScopedVarKey(VarScope scope, string key)
        {
            Scope = scope;
            Key = key;
        }

        public VarScope Scope { get; }
        public string Key { get; }
    }
}
