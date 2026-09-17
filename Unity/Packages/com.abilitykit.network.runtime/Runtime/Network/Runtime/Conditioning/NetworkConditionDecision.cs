namespace AbilityKit.Network.Runtime.Conditioning
{
    public enum NetworkConditionDropReason
    {
        None,
        RandomLoss,
        QueueOverflow
    }

    /// <summary>Packet-level outcome before downstream delivery; timestamps use the injected clock.</summary>
    public readonly struct NetworkConditionDecision
    {
        public NetworkConditionDecision(long observedAtMs, long deliverAtMs, bool inbound,
            uint opCode, uint sequence, NetworkConditionDropReason dropReason, bool reordered)
        {
            ObservedAtMs = observedAtMs;
            DeliverAtMs = deliverAtMs;
            Inbound = inbound;
            OpCode = opCode;
            Sequence = sequence;
            DropReason = dropReason;
            Reordered = reordered;
        }

        public long ObservedAtMs { get; }
        public long DeliverAtMs { get; }
        public bool Inbound { get; }
        public uint OpCode { get; }
        public uint Sequence { get; }
        public NetworkConditionDropReason DropReason { get; }
        public bool Dropped => DropReason != NetworkConditionDropReason.None;
        public bool Reordered { get; }
    }
}
