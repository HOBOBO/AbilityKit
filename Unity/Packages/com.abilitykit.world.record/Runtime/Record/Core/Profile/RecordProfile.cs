using System;

namespace AbilityKit.Ability.Share.Common.Record.Core
{
    [Serializable]
    public sealed class RecordProfile
    {
        public bool EnableInputs = true;
        public bool EnableStateHash = true;
        public int StateHashIntervalFrames = 10;

        public bool EnableSnapshots = true;

        public int IndexChunkFrames = 300;
    }
}
