namespace AbilityKit.Ability.Editor
{
    internal enum VarKeyUsage
    {
        Assign = 0,
        Read = 1
    }

    internal readonly struct VarKeyQuery
    {
        public VarKeyQuery(bool includeLocal, bool includeGlobal)
        {
            IncludeLocal = includeLocal;
            IncludeGlobal = includeGlobal;

            Scope = null;
            Usage = VarKeyUsage.Read;
            ExpectedKind = AbilityKit.Configs.ArgValueKind.None;
        }

        public VarKeyQuery(
            bool includeLocal,
            bool includeGlobal,
            AbilityKit.Triggering.VarScope? scope,
            VarKeyUsage usage,
            AbilityKit.Configs.ArgValueKind expectedKind
        )
        {
            IncludeLocal = includeLocal;
            IncludeGlobal = includeGlobal;
            Scope = scope;
            Usage = usage;
            ExpectedKind = expectedKind;
        }

        public bool IncludeLocal { get; }
        public bool IncludeGlobal { get; }

        public AbilityKit.Triggering.VarScope? Scope { get; }
        public VarKeyUsage Usage { get; }
        public AbilityKit.Configs.ArgValueKind ExpectedKind { get; }
    }
}
