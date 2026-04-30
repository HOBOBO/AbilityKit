namespace AbilityKit.Ability.StateSync
{
    public interface IRollbackable
    {
        long EntityId { get; }
        int SnapshotKey { get; }
        IRollbackState CreateRollbackState();
        void RestoreFromRollbackState(IRollbackState state);
    }
}
