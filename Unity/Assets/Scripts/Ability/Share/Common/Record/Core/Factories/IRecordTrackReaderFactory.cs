namespace AbilityKit.Ability.Share.Common.Record.Core
{
    public interface IRecordTrackReaderFactory
    {
        bool TryCreateReader(RecordContainer container, RecordTrackId trackId, out IEventTrackReader reader);
    }
}
