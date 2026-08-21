#nullable enable

using System;
using AbilityKit.Network.Protocol;

namespace AbilityKit.Network.Runtime.Observability
{
    public enum NetworkTrafficDirection
    {
        Inbound = 0,
        Outbound = 1
    }

    /// <summary>Identifies one physical session created by a logical connection.</summary>
    public sealed class NetworkTrafficConnectionContext
    {
        public NetworkTrafficConnectionContext(
            string connectionId,
            int generation,
            string role,
            string catalogId,
            string endpoint,
            string transport)
        {
            ConnectionId = connectionId ?? string.Empty;
            Generation = generation;
            Role = role ?? string.Empty;
            CatalogId = catalogId ?? string.Empty;
            Endpoint = endpoint ?? string.Empty;
            Transport = transport ?? string.Empty;
        }

        public string ConnectionId { get; }
        public int Generation { get; }
        public string Role { get; }
        public string CatalogId { get; }
        public string Endpoint { get; }
        public string Transport { get; }
    }

    /// <summary>An immutable packet observation. PayloadPreview always owns its backing bytes.</summary>
    public sealed class NetworkTrafficEvent
    {
        internal NetworkTrafficEvent(
            NetworkTrafficConnectionContext connection,
            DateTimeOffset timestampUtc,
            NetworkTrafficDirection direction,
            NetworkPacketHeader header,
            int payloadLength,
            byte[] payloadPreview)
        {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            TimestampUtc = timestampUtc;
            Direction = direction;
            Flags = header.Flags;
            OpCode = header.OpCode;
            Sequence = header.Seq;
            PayloadLength = payloadLength;
            PayloadPreview = payloadPreview ?? Array.Empty<byte>();
            IsPayloadPreviewTruncated = PayloadPreview.Length < payloadLength;
        }

        public NetworkTrafficConnectionContext Connection { get; }
        public string ConnectionId => Connection.ConnectionId;
        public int Generation => Connection.Generation;
        public string Role => Connection.Role;
        public string CatalogId => Connection.CatalogId;
        public string Endpoint => Connection.Endpoint;
        public string Transport => Connection.Transport;
        public DateTimeOffset TimestampUtc { get; }
        public NetworkTrafficDirection Direction { get; }
        public NetworkPacketFlags Flags { get; }
        public uint OpCode { get; }
        public uint Sequence { get; }
        public int PayloadLength { get; }
        public ReadOnlyMemory<byte> PayloadPreview { get; }
        public bool IsPayloadPreviewTruncated { get; }
    }
}
