namespace AbilityKit.Ability.Share.Common.Record.Core
{
    public interface ISeekStrategy
    {
        bool TrySeek(in SeekRequest req);
    }
}
