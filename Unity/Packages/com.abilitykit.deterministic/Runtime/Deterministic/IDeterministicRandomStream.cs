namespace AbilityKit.Deterministic
{
    /// <summary>
    /// Canonical deterministic random stream contract shared by simulation services, triggers and
    /// behavior trees. Implementations must preserve the exact state across capture/restore.
    /// </summary>
    public interface IDeterministicRandomStream
    {
        ulong Seed { get; }
        ulong Sequence { get; }

        void SetSeed(ulong seed);
        ulong NextUInt64();
        int NextInt32(int minInclusive, int maxExclusive);
        float NextFloat01();

        void CaptureState(out ulong state0, out ulong state1, out ulong sequence);
        void RestoreState(ulong state0, ulong state1, ulong sequence);
    }
}
