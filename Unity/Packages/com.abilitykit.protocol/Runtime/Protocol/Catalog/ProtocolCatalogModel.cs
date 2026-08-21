#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Protocol.Catalog
{
    public enum ProtocolPacketKind
    {
        Request = 0,
        Response = 1,
        Push = 2,
        Event = 3
    }

    public enum ProtocolReliability
    {
        Reliable = 0,
        Realtime = 1
    }

    public readonly struct ProtocolMessageKey : IEquatable<ProtocolMessageKey>
    {
        public ProtocolMessageKey(
            string catalogId,
            uint opCode,
            ProtocolDirection direction,
            ProtocolPacketKind kind)
        {
            if (string.IsNullOrWhiteSpace(catalogId))
                throw new ArgumentException("Catalog id is required.", nameof(catalogId));

            CatalogId = catalogId;
            OpCode = opCode;
            Direction = direction;
            Kind = kind;
        }

        public string CatalogId { get; }
        public uint OpCode { get; }
        public ProtocolDirection Direction { get; }
        public ProtocolPacketKind Kind { get; }

        public bool Equals(ProtocolMessageKey other) =>
            string.Equals(CatalogId, other.CatalogId, StringComparison.Ordinal) &&
            OpCode == other.OpCode &&
            Direction == other.Direction &&
            Kind == other.Kind;

        public override bool Equals(object? obj) =>
            obj is ProtocolMessageKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = StringComparer.Ordinal.GetHashCode(CatalogId ?? string.Empty);
                hash = (hash * 397) ^ (int)OpCode;
                hash = (hash * 397) ^ (int)Direction;
                hash = (hash * 397) ^ (int)Kind;
                return hash;
            }
        }

        public override string ToString() =>
            $"{CatalogId}:{Direction}:{Kind}:{OpCode}";

        public static bool operator ==(ProtocolMessageKey left, ProtocolMessageKey right) =>
            left.Equals(right);

        public static bool operator !=(ProtocolMessageKey left, ProtocolMessageKey right) =>
            !left.Equals(right);
    }

    public sealed class ProtocolMessageDefinition
    {
        public ProtocolMessageDefinition(
            string id,
            uint opCode,
            ProtocolDirection direction,
            ProtocolPacketKind kind,
            string payloadType,
            string codec,
            ProtocolReliability reliability = ProtocolReliability.Reliable,
            string? responseId = null,
            int minimumSchemaVersion = 1,
            int maximumSchemaVersion = 1,
            int maximumPayloadBytes = 1048576,
            double captureSampleRate = 1d,
            IReadOnlyList<string>? sensitiveFields = null)
        {
            Id = id ?? string.Empty;
            OpCode = opCode;
            Direction = direction;
            Kind = kind;
            PayloadType = payloadType ?? string.Empty;
            Codec = codec ?? string.Empty;
            Reliability = reliability;
            ResponseId = responseId ?? string.Empty;
            MinimumSchemaVersion = minimumSchemaVersion;
            MaximumSchemaVersion = maximumSchemaVersion;
            MaximumPayloadBytes = maximumPayloadBytes;
            CaptureSampleRate = captureSampleRate;
            SensitiveFields = sensitiveFields ?? Array.Empty<string>();
        }

        public string Id { get; }
        public uint OpCode { get; }
        public ProtocolDirection Direction { get; }
        public ProtocolPacketKind Kind { get; }
        public string PayloadType { get; }
        public string Codec { get; }
        public ProtocolReliability Reliability { get; }
        public string ResponseId { get; }
        public int MinimumSchemaVersion { get; }
        public int MaximumSchemaVersion { get; }
        public int MaximumPayloadBytes { get; }
        public double CaptureSampleRate { get; }
        public IReadOnlyList<string> SensitiveFields { get; }

        public ProtocolMessageKey CreateKey(string catalogId) =>
            new ProtocolMessageKey(catalogId, OpCode, Direction, Kind);
    }

    public sealed class ProtocolCatalogDefinition
    {
        public ProtocolCatalogDefinition(
            string catalogId,
            string projectId,
            string domain,
            int revision,
            string defaultCodec,
            IReadOnlyList<ProtocolMessageDefinition> messages)
        {
            CatalogId = catalogId ?? string.Empty;
            ProjectId = projectId ?? string.Empty;
            Domain = domain ?? string.Empty;
            Revision = revision;
            DefaultCodec = defaultCodec ?? string.Empty;
            Messages = messages ?? Array.Empty<ProtocolMessageDefinition>();
        }

        public string CatalogId { get; }
        public string ProjectId { get; }
        public string Domain { get; }
        public int Revision { get; }
        public string DefaultCodec { get; }
        public IReadOnlyList<ProtocolMessageDefinition> Messages { get; }
    }
}
