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

    /// <summary>
    /// Marker for providers that restore object existence and structural relationships. After all
    /// providers pass preflight, the coordinator imports these providers before field-state providers.
    /// </summary>
    public interface IRollbackStructureRestoreProvider : IRollbackStateProvider
    {
    }
}
