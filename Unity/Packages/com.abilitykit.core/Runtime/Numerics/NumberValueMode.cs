namespace AbilityKit.Core.Numerics
{
    internal static class NumericsDeprecation
    {
        public const string Message = "Gameplay modifier semantics are no longer a Core responsibility. Migrate to a domain-owned numeric pipeline; this API will be removed in the next major version.";
    }

    [System.Obsolete(NumericsDeprecation.Message)]
    public enum NumberValueMode
    {
        BaseOnly = 0,
        BaseAdd = 1,
        BaseAddMul = 2,
        OverrideOnly = 3
    }
}
