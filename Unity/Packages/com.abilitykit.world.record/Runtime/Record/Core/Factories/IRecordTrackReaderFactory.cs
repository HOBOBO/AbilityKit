namespace AbilityKit.Core.Common.Record.Core
{
    public interface IRecordTrackReaderFactory
    {
        bool TryCreateReader(RecordContainer container, RecordTrackId trackId, out IEventTrackReader reader);
    }
}
