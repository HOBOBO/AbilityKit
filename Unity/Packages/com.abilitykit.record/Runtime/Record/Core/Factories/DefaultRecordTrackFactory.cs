using System;

namespace AbilityKit.Core.Common.Record.Core
{
    public sealed class DefaultRecordTrackFactory : IRecordTrackWriterFactory, IRecordTrackReaderFactory
    {
        public bool TryCreateWriter(RecordContainer container, RecordTrackId trackId, RecordProfile profile, out IEventTrackWriter writer)
        {
            writer = null;
            if (container == null) return false;
            if (container.Tracks == null) return false;

            if (!container.Tracks.TryGetValue(trackId, out var track) || track == null)
            {
                if (profile == null) return false;

                track = CreateDefaultTrack(trackId, profile);
                if (track == null) return false;
                container.Tracks[trackId] = track;
            }

            if (track.Events == null) track.Events = new EventTrack();
            writer = track.Events;
            return true;
        }

        public bool TryCreateReader(RecordContainer container, RecordTrackId trackId, out IEventTrackReader reader)
        {
            reader = null;
            if (container == null) return false;
            if (container.Tracks == null) return false;

            if (!container.Tracks.TryGetValue(trackId, out var track) || track == null) return false;
            if (track.Events == null) return false;

            reader = track.Events;
            return true;
        }

        private static RecordTrack CreateDefaultTrack(RecordTrackId trackId, RecordProfile profile)
        {
            // By convention we only auto-create the three default tracks.
            var inputsId = RecordTrackId.FromName(RecordTrackNames.Inputs);
            var hashId = RecordTrackId.FromName(RecordTrackNames.StateHash);
            var snapsId = RecordTrackId.FromName(RecordTrackNames.Snapshots);

            if (trackId == inputsId)
            {
                if (!profile.EnableInputs) return null;
                return new RecordTrack { Id = trackId, Version = 1, Schema = RecordTrackNames.Inputs, Events = new EventTrack() };
            }

            if (trackId == hashId)
            {
                if (!profile.EnableStateHash) return null;
                return new RecordTrack { Id = trackId, Version = 1, Schema = RecordTrackNames.StateHash, Events = new EventTrack() };
            }

            if (trackId == snapsId)
            {
                if (!profile.EnableSnapshots) return null;
                return new RecordTrack { Id = trackId, Version = 1, Schema = RecordTrackNames.Snapshots, Events = new EventTrack() };
            }

            return null;
        }
    }
}
