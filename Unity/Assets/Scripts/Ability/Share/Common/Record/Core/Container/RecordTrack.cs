using System;

namespace AbilityKit.Ability.Share.Common.Record.Core
{
    [Serializable]
    public sealed class RecordTrack
    {
        public RecordTrackId Id;

        public int Version;

        public string Schema;

        public EventTrack Events;
    }
}
