namespace AbilityKit.Core.Common.Record.Core
{
    public interface ISeekStrategy
    {
        bool TrySeek(in SeekRequest req);
    }
}
