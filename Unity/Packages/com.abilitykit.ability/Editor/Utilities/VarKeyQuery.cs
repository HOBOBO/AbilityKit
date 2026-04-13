using AbilityKit.Ability.Config;

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
            ExpectedKind = ArgValueKind.None;
        }

        public VarKeyQuery(
            bool includeLocal,
            bool includeGlobal,
            Triggering.VarScope? scope,
            VarKeyUsage usage,
            ArgValueKind expectedKind
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

        public Triggering.VarScope? Scope { get; }
        public VarKeyUsage Usage { get; }
        public ArgValueKind ExpectedKind { get; }
    }
}
