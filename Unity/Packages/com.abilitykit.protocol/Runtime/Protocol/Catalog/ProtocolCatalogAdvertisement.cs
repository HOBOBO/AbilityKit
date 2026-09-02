#nullable enable

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AbilityKit.Protocol.Catalog
{
    /// <summary>
    /// Transport-neutral catalog advertisement exchanged during the system handshake. It may
    /// contain several catalogs because a physical connection commonly serves room, battle and
    /// shared system traffic together.
    /// </summary>
    public sealed class ProtocolCatalogAdvertisement
    {
        public ProtocolCatalogAdvertisement(IReadOnlyList<ProtocolCatalogAdvertisementCatalog> catalogs)
        {
            if (catalogs == null) throw new ArgumentNullException(nameof(catalogs));
            Catalogs = Array.AsReadOnly(catalogs.ToArray());
        }

        public IReadOnlyList<ProtocolCatalogAdvertisementCatalog> Catalogs { get; }

        public static ProtocolCatalogAdvertisement FromCatalogs(
            IEnumerable<ProtocolCatalogDefinition> catalogs)
        {
            if (catalogs == null) throw new ArgumentNullException(nameof(catalogs));
            return new ProtocolCatalogAdvertisement(catalogs
                .Select(ProtocolCatalogAdvertisementCatalog.FromCatalog)
                .OrderBy(catalog => catalog.CatalogId, StringComparer.Ordinal)
                .ToArray());
        }
    }

    public sealed class ProtocolCatalogAdvertisementCatalog
    {
        public ProtocolCatalogAdvertisementCatalog(
            string catalogId,
            string projectId,
            string domain,
            int revision,
            string defaultCodec,
            IReadOnlyList<ProtocolCatalogAdvertisementMessage> messages)
        {
            CatalogId = catalogId ?? string.Empty;
            ProjectId = projectId ?? string.Empty;
            Domain = domain ?? string.Empty;
            Revision = revision;
            DefaultCodec = defaultCodec ?? string.Empty;
            if (messages == null) throw new ArgumentNullException(nameof(messages));
            Messages = Array.AsReadOnly(messages.ToArray());
        }

        public string CatalogId { get; }
        public string ProjectId { get; }
        public string Domain { get; }
        public int Revision { get; }
        public string DefaultCodec { get; }
        public IReadOnlyList<ProtocolCatalogAdvertisementMessage> Messages { get; }

        public ProtocolCatalogDefinition ToCatalogDefinition() =>
            new ProtocolCatalogDefinition(
                CatalogId,
                ProjectId,
                Domain,
                Revision,
                DefaultCodec,
                Messages.Select(message => message.ToMessageDefinition()).ToArray());

        internal static ProtocolCatalogAdvertisementCatalog FromCatalog(ProtocolCatalogDefinition catalog) =>
            new ProtocolCatalogAdvertisementCatalog(
                catalog.CatalogId,
                catalog.ProjectId,
                catalog.Domain,
                catalog.Revision,
                catalog.DefaultCodec,
                catalog.Messages.Select(ProtocolCatalogAdvertisementMessage.FromMessage).ToArray());
    }

    public sealed class ProtocolCatalogAdvertisementMessage
    {
        public ProtocolCatalogAdvertisementMessage(
            string id,
            uint opCode,
            ProtocolDirection direction,
            ProtocolPacketKind kind,
            string payloadType,
            string codec,
            ProtocolReliability reliability,
            int minimumSchemaVersion,
            int maximumSchemaVersion,
            int maximumPayloadBytes)
        {
            Id = id ?? string.Empty;
            OpCode = opCode;
            Direction = direction;
            Kind = kind;
            PayloadType = payloadType ?? string.Empty;
            Codec = codec ?? string.Empty;
            Reliability = reliability;
            MinimumSchemaVersion = minimumSchemaVersion;
            MaximumSchemaVersion = maximumSchemaVersion;
            MaximumPayloadBytes = maximumPayloadBytes;
        }

        public string Id { get; }
        public uint OpCode { get; }
        public ProtocolDirection Direction { get; }
        public ProtocolPacketKind Kind { get; }
        public string PayloadType { get; }
        public string Codec { get; }
        public ProtocolReliability Reliability { get; }
        public int MinimumSchemaVersion { get; }
        public int MaximumSchemaVersion { get; }
        public int MaximumPayloadBytes { get; }

        internal ProtocolMessageDefinition ToMessageDefinition() =>
            new ProtocolMessageDefinition(
                Id,
                OpCode,
                Direction,
                Kind,
                PayloadType,
                Codec,
                Reliability,
                minimumSchemaVersion: MinimumSchemaVersion,
                maximumSchemaVersion: MaximumSchemaVersion,
                maximumPayloadBytes: MaximumPayloadBytes);

        internal static ProtocolCatalogAdvertisementMessage FromMessage(ProtocolMessageDefinition message) =>
            new ProtocolCatalogAdvertisementMessage(
                message.Id,
                message.OpCode,
                message.Direction,
                message.Kind,
                message.PayloadType,
                message.Codec,
                message.Reliability,
                message.MinimumSchemaVersion,
                message.MaximumSchemaVersion,
                message.MaximumPayloadBytes);
    }

    public readonly struct ProtocolCatalogAdvertisementDecodeOptions
    {
        public ProtocolCatalogAdvertisementDecodeOptions(
            int maximumPayloadBytes = 1048576,
            int maximumCatalogs = 64,
            int maximumMessagesPerCatalog = 4096,
            int maximumStringBytes = 4096)
        {
            MaximumPayloadBytes = maximumPayloadBytes;
            MaximumCatalogs = maximumCatalogs;
            MaximumMessagesPerCatalog = maximumMessagesPerCatalog;
            MaximumStringBytes = maximumStringBytes;
        }

        public int MaximumPayloadBytes { get; }
        public int MaximumCatalogs { get; }
        public int MaximumMessagesPerCatalog { get; }
        public int MaximumStringBytes { get; }

        public static ProtocolCatalogAdvertisementDecodeOptions Default =>
            new ProtocolCatalogAdvertisementDecodeOptions();
    }

    /// <summary>Deterministic, bounded codec for the system catalog advertisement payload.</summary>
    public static class ProtocolCatalogAdvertisementCodec
    {
        private const uint Magic = 0x41434B41; // "AKCA" in little endian.
        private const ushort FormatVersion = 1;
        private const int HeaderBytes = 8;
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public static byte[] Encode(ProtocolCatalogAdvertisement advertisement)
        {
            if (advertisement == null) throw new ArgumentNullException(nameof(advertisement));
            var bytes = new List<byte>(Math.Min(1048576, HeaderBytes + advertisement.Catalogs.Count * 128));
            AppendUInt32(bytes, Magic);
            AppendUInt16(bytes, FormatVersion);
            AppendUInt16(bytes, CheckedCount(advertisement.Catalogs.Count, "catalog"));
            foreach (var catalog in advertisement.Catalogs)
            {
                AppendString(bytes, catalog.CatalogId);
                AppendString(bytes, catalog.ProjectId);
                AppendString(bytes, catalog.Domain);
                AppendInt32(bytes, catalog.Revision);
                AppendString(bytes, catalog.DefaultCodec);
                AppendUInt16(bytes, CheckedCount(catalog.Messages.Count, "message"));
                foreach (var message in catalog.Messages)
                {
                    AppendString(bytes, message.Id);
                    AppendUInt32(bytes, message.OpCode);
                    bytes.Add((byte)message.Direction);
                    bytes.Add((byte)message.Kind);
                    AppendString(bytes, message.PayloadType);
                    AppendString(bytes, message.Codec);
                    bytes.Add((byte)message.Reliability);
                    AppendInt32(bytes, message.MinimumSchemaVersion);
                    AppendInt32(bytes, message.MaximumSchemaVersion);
                    AppendInt32(bytes, message.MaximumPayloadBytes);
                }
            }
            return bytes.ToArray();
        }

        public static bool TryDecode(
            ReadOnlySpan<byte> payload,
            out ProtocolCatalogAdvertisement? advertisement,
            out string error,
            ProtocolCatalogAdvertisementDecodeOptions options = default)
        {
            options = Normalize(options);
            advertisement = null;
            error = string.Empty;
            if (payload.Length > options.MaximumPayloadBytes)
                return Fail($"Payload length {payload.Length} exceeds {options.MaximumPayloadBytes}.", out error);

            var reader = new Reader(payload, options);
            if (!reader.TryUInt32(out var magic) || magic != Magic)
                return Fail("Invalid catalog advertisement magic.", out error);
            if (!reader.TryUInt16(out var version) || version != FormatVersion)
                return Fail("Unsupported catalog advertisement format version.", out error);
            if (!reader.TryUInt16(out var catalogCount) || catalogCount > options.MaximumCatalogs)
                return Fail("Catalog count exceeds the configured bound.", out error);

            var catalogs = new List<ProtocolCatalogAdvertisementCatalog>(catalogCount);
            for (var i = 0; i < catalogCount; i++)
            {
                if (!reader.TryString(out var catalogId) || !reader.TryString(out var projectId) ||
                    !reader.TryString(out var domain) || !reader.TryInt32(out var revision) ||
                    !reader.TryString(out var defaultCodec) ||
                    !reader.TryUInt16(out var messageCount) ||
                    messageCount > options.MaximumMessagesPerCatalog)
                    return Fail("Truncated or oversized catalog advertisement.", out error);

                var messages = new List<ProtocolCatalogAdvertisementMessage>(messageCount);
                for (var j = 0; j < messageCount; j++)
                {
                    if (!reader.TryString(out var id) || !reader.TryUInt32(out var opCode) ||
                        !reader.TryByte(out var direction) || !reader.TryByte(out var kind) ||
                        !reader.TryString(out var payloadType) || !reader.TryString(out var codec) ||
                        !reader.TryByte(out var reliability) || !reader.TryInt32(out var minimum) ||
                        !reader.TryInt32(out var maximum) || !reader.TryInt32(out var budget) ||
                        !Enum.IsDefined(typeof(ProtocolDirection), (int)direction) ||
                        !Enum.IsDefined(typeof(ProtocolPacketKind), (int)kind) ||
                        !Enum.IsDefined(typeof(ProtocolReliability), (int)reliability))
                        return Fail("Invalid or truncated message advertisement.", out error);
                    messages.Add(new ProtocolCatalogAdvertisementMessage(
                        id, opCode, (ProtocolDirection)direction, (ProtocolPacketKind)kind,
                        payloadType, codec, (ProtocolReliability)reliability, minimum, maximum, budget));
                }

                catalogs.Add(new ProtocolCatalogAdvertisementCatalog(
                    catalogId, projectId, domain, revision, defaultCodec, messages));
            }

            if (!reader.IsAtEnd)
                return Fail("Trailing bytes are not allowed in a catalog advertisement.", out error);
            advertisement = new ProtocolCatalogAdvertisement(catalogs);
            return true;
        }

        private static ProtocolCatalogAdvertisementDecodeOptions Normalize(
            ProtocolCatalogAdvertisementDecodeOptions options)
        {
            var defaults = ProtocolCatalogAdvertisementDecodeOptions.Default;
            return new ProtocolCatalogAdvertisementDecodeOptions(
                options.MaximumPayloadBytes > 0 ? options.MaximumPayloadBytes : defaults.MaximumPayloadBytes,
                options.MaximumCatalogs > 0 ? options.MaximumCatalogs : defaults.MaximumCatalogs,
                options.MaximumMessagesPerCatalog > 0 ? options.MaximumMessagesPerCatalog : defaults.MaximumMessagesPerCatalog,
                options.MaximumStringBytes > 0 ? options.MaximumStringBytes : defaults.MaximumStringBytes);
        }

        private static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }

        private static ushort CheckedCount(int count, string label) =>
            count is < 0 or > ushort.MaxValue
                ? throw new InvalidOperationException($"Too many {label}s in catalog advertisement.")
                : (ushort)count;

        private static void AppendString(List<byte> bytes, string value)
        {
            var encoded = StrictUtf8.GetBytes(value ?? string.Empty);
            if (encoded.Length > ushort.MaxValue)
                throw new InvalidOperationException("Catalog advertisement string is too long.");
            AppendUInt16(bytes, (ushort)encoded.Length);
            bytes.AddRange(encoded);
        }

        private static void AppendUInt16(List<byte> bytes, ushort value)
        {
            Span<byte> buffer = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(buffer, value);
            bytes.AddRange(buffer.ToArray());
        }

        private static void AppendUInt32(List<byte> bytes, uint value)
        {
            Span<byte> buffer = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
            bytes.AddRange(buffer.ToArray());
        }

        private static void AppendInt32(List<byte> bytes, int value) => AppendUInt32(bytes, unchecked((uint)value));

        private ref struct Reader
        {
            private readonly ReadOnlySpan<byte> _payload;
            private readonly ProtocolCatalogAdvertisementDecodeOptions _options;
            private int _offset;

            public Reader(ReadOnlySpan<byte> payload, ProtocolCatalogAdvertisementDecodeOptions options)
            {
                _payload = payload;
                _options = options;
                _offset = 0;
            }

            public bool IsAtEnd => _offset == _payload.Length;

            public bool TryByte(out byte value)
            {
                if (_offset >= _payload.Length) { value = 0; return false; }
                value = _payload[_offset++];
                return true;
            }

            public bool TryUInt16(out ushort value)
            {
                if (_payload.Length - _offset < 2) { value = 0; return false; }
                value = BinaryPrimitives.ReadUInt16LittleEndian(_payload.Slice(_offset, 2));
                _offset += 2;
                return true;
            }

            public bool TryUInt32(out uint value)
            {
                if (_payload.Length - _offset < 4) { value = 0; return false; }
                value = BinaryPrimitives.ReadUInt32LittleEndian(_payload.Slice(_offset, 4));
                _offset += 4;
                return true;
            }

            public bool TryInt32(out int value)
            {
                if (!TryUInt32(out var raw)) { value = 0; return false; }
                value = unchecked((int)raw);
                return true;
            }

            public bool TryString(out string value)
            {
                value = string.Empty;
                if (!TryUInt16(out var length) || length > _options.MaximumStringBytes ||
                    _payload.Length - _offset < length)
                    return false;
                try
                {
                    value = StrictUtf8.GetString(_payload.Slice(_offset, length));
                    _offset += length;
                    return true;
                }
                catch (DecoderFallbackException)
                {
                    return false;
                }
            }
        }
    }
}
