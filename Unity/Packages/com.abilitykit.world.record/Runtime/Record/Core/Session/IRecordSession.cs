using System;

namespace AbilityKit.Core.Common.Record.Core
{
    public interface IRecordSession : IDisposable
    {
        RecordProfile Profile { get; }

        RecordContainer Container { get; }

        bool TryGetWriter(RecordTrackId trackId, out IEventTrackWriter writer);

        bool TryGetReader(RecordTrackId trackId, out IEventTrackReader reader);

        byte[] Serialize();

        bool TryLoad(byte[] data);
    }
}
