namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>Platform-neutral authoritative state hash snapshot.</summary>
    public readonly struct StateHashData
    {
        public bool HasValue { get; }
        public int Version { get; }
        public int FrameIndex { get; }
        public uint StateHash { get; }

        public StateHashData(int version, int frameIndex, uint stateHash)
        {
            Version = version;
            FrameIndex = frameIndex;
            StateHash = stateHash;
            HasValue = true;
        }

        public StateHashData(int frameIndex, uint stateHash)
            : this(0, frameIndex, stateHash)
        {
        }

        public static readonly StateHashData Default = default;
    }
}
