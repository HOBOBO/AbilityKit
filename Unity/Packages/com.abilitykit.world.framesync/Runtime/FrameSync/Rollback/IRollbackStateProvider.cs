using AbilityKit.Ability.FrameSync;

namespace AbilityKit.Ability.FrameSync.Rollback
{
    public interface IRollbackStateProvider
    {
        int Key { get; }

        byte[] Export(FrameIndex frame);

        void Import(FrameIndex frame, byte[] payload);
    }

    /// <summary>
    /// Optional validation hook executed for every provider before any provider imports state.
    /// Implementations must not mutate runtime state.
    /// </summary>
    public interface IRollbackStatePreflightProvider
    {
        void ValidateImport(FrameIndex frame, byte[] payload);
    }
}
