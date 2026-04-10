using System;
using System.Collections.Generic;

namespace AbilityKit.Core.Common.Record.Lockstep
{
    [Serializable]
    public sealed class LockstepInputRecordFile
    {
        public LockstepInputRecordMeta Meta;
        public List<LockstepInputRecordFrame> Inputs;
        public List<LockstepStateHashRecordFrame> StateHashes;
        public List<LockstepSnapshotRecordFrame> Snapshots;
        public List<LockstepRecordChunkIndex> Index;
    }

    [Serializable]
    public sealed class LockstepInputRecordMeta
    {
        public string WorldId;
        public string WorldType;
        public int TickRate;
        public int RandomSeed;
        public string PlayerId;
        public long StartedAtUnixMs;
    }

    [Serializable]
    public sealed class LockstepInputRecordFrame
    {
        public int Frame;
        public string PlayerId;
        public int OpCode;
        public string PayloadBase64;
    }

    [Serializable]
    public sealed class LockstepStateHashRecordFrame
    {
        public int Frame;
        public int Version;
        public uint Hash;
    }

    [Serializable]
    public sealed class LockstepSnapshotRecordFrame
    {
        public int Frame;
        public int OpCode;
        public string PayloadBase64;
    }

    [Serializable]
    public sealed class LockstepRecordChunkIndex
    {
        public int StartFrame;
        public int EndFrame;

        public int InputStart;
        public int InputEnd;

        public int StateHashStart;
        public int StateHashEnd;

        public int SnapshotStart;
        public int SnapshotEnd;
    }
}
