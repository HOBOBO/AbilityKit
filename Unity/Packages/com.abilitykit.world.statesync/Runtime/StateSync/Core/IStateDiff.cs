namespace AbilityKit.Ability.StateSync
{
    public interface IStateDiff
    {
        int FromFrame { get; }
        int ToFrame { get; }
        long Timestamp { get; }
        byte[] CompressedData { get; }
        int UncompressedSize { get; }
        bool IsFullSnapshot { get; }
    }
}
