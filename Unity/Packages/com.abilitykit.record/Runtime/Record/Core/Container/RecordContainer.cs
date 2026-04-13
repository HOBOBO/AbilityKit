using System;
using System.Collections.Generic;

namespace AbilityKit.Core.Common.Record.Core
{
    [Serializable]
    public sealed class RecordContainer
    {
        public RecordContainer()
        {
        }

        public Dictionary<string, object> Meta;

        public Dictionary<RecordTrackId, RecordTrack> Tracks;
    }
}
