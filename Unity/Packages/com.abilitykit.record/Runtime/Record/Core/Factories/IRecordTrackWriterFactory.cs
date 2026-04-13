namespace AbilityKit.Core.Common.Record.Core
{
    public interface IRecordTrackWriterFactory
    {
        bool TryCreateWriter(RecordContainer container, RecordTrackId trackId, RecordProfile profile, out IEventTrackWriter writer);
    }
}
