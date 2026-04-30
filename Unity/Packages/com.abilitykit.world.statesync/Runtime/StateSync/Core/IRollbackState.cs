namespace AbilityKit.Ability.StateSync
{
    public interface IRollbackState
    {
        int SnapshotKey { get; }
        byte[] Serialize();
        void Deserialize(byte[] data);
    }
}
