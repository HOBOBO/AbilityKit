namespace AbilityKit.Network.Host
{
    public readonly struct NetworkHostSessionSnapshot
    {
        public NetworkHostSessionSnapshot(
            string id,
            string remoteEndpoint,
            bool isConnected,
            bool isEstablished,
            long openedTimestamp,
            long lastActivityTimestamp,
            long timestampFrequency,
            int pendingRequests,
            long bytesReceived,
            long bytesSent,
            long packetsReceived,
            long packetsSent)
        {
            Id = id;
            RemoteEndpoint = remoteEndpoint;
            IsConnected = isConnected;
            IsEstablished = isEstablished;
            OpenedTimestamp = openedTimestamp;
            LastActivityTimestamp = lastActivityTimestamp;
            TimestampFrequency = timestampFrequency;
            PendingRequests = pendingRequests;
            BytesReceived = bytesReceived;
            BytesSent = bytesSent;
            PacketsReceived = packetsReceived;
            PacketsSent = packetsSent;
        }

        public string Id { get; }
        public string RemoteEndpoint { get; }
        public bool IsConnected { get; }
        public bool IsEstablished { get; }
        public long OpenedTimestamp { get; }
        public long LastActivityTimestamp { get; }
        public long TimestampFrequency { get; }
        public int PendingRequests { get; }
        public long BytesReceived { get; }
        public long BytesSent { get; }
        public long PacketsReceived { get; }
        public long PacketsSent { get; }
    }
}
