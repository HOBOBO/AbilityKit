using System;

namespace AbilityKit.Ability.Share.Common.Record.Core
{
    public sealed class RecordSession : IRecordSession
    {
        private readonly IRecordContainerSerializer _serializer;
        private readonly IRecordTrackWriterFactory _writerFactory;
        private readonly IRecordTrackReaderFactory _readerFactory;

        public RecordSession(
            RecordProfile profile,
            RecordContainer container,
            IRecordContainerSerializer serializer,
            IRecordTrackWriterFactory writerFactory,
            IRecordTrackReaderFactory readerFactory)
        {
            Profile = profile ?? new RecordProfile();
            Container = container ?? new RecordContainerBuilder(Profile).Build();

            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _writerFactory = writerFactory ?? throw new ArgumentNullException(nameof(writerFactory));
            _readerFactory = readerFactory ?? throw new ArgumentNullException(nameof(readerFactory));
        }

        public RecordProfile Profile { get; }

        public RecordContainer Container { get; }

        public bool TryGetWriter(RecordTrackId trackId, out IEventTrackWriter writer)
        {
            return _writerFactory.TryCreateWriter(Container, trackId, Profile, out writer);
        }

        public bool TryGetReader(RecordTrackId trackId, out IEventTrackReader reader)
        {
            return _readerFactory.TryCreateReader(Container, trackId, out reader);
        }

        public byte[] Serialize()
        {
            return _serializer.Serialize(Container);
        }

        public bool TryLoad(byte[] data)
        {
            var c = _serializer.Deserialize(data);
            if (c == null) return false;

            // Replace container contents. Keep the same session object so callers can hold references.
            Container.Meta = c.Meta;
            Container.Tracks = c.Tracks;
            return true;
        }

        public void Dispose()
        {
        }
    }
}
