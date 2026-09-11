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
            int maximumPayloadBytes,
            string? responseId = null,
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
            MinimumSchemaVersion = minimumSchemaVersion;
            MaximumSchemaVersion = maximumSchemaVersion;
            MaximumPayloadBytes = maximumPayloadBytes;
            ResponseId = responseId ?? string.Empty;
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
        public int MinimumSchemaVersion { get; }
        public int MaximumSchemaVersion { get; }
        public int MaximumPayloadBytes { get; }
        public string ResponseId { get; }
        public double CaptureSampleRate { get; }
        public IReadOnlyList<string> SensitiveFields { get; }

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
                maximumPayloadBytes: MaximumPayloadBytes,
                responseId: ResponseId,
                captureSampleRate: CaptureSampleRate,
                sensitiveFields: SensitiveFields);

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
                message.MaximumPayloadBytes,
                message.ResponseId,
                message.CaptureSampleRate,
                message.SensitiveFields);
    }

    public readonly struct ProtocolCatalogAdvertisementDecodeOptions
    {
        public const int DefaultMaximumPayloadBytes = 1048576;
        public const int DefaultMaximumCatalogs = 64;
        public const int DefaultMaximumMessagesPerCatalog = 4096;
        public const int DefaultMaximumStringBytes = 4096;
        public const int DefaultMaximumSensitiveFieldsPerMessage = 64;

        public ProtocolCatalogAdvertisementDecodeOptions(
            int maximumPayloadBytes = DefaultMaximumPayloadBytes,
            int maximumCatalogs = DefaultMaximumCatalogs,
            int maximumMessagesPerCatalog = DefaultMaximumMessagesPerCatalog,
            int maximumStringBytes = DefaultMaximumStringBytes,
            int maximumSensitiveFieldsPerMessage = DefaultMaximumSensitiveFieldsPerMessage)
        {
            MaximumPayloadBytes = maximumPayloadBytes;
            MaximumCatalogs = maximumCatalogs;
            MaximumMessagesPerCatalog = maximumMessagesPerCatalog;
            MaximumStringBytes = maximumStringBytes;
            MaximumSensitiveFieldsPerMessage = maximumSensitiveFieldsPerMessage;
        }

        public int MaximumPayloadBytes { get; }
        public int MaximumCatalogs { get; }
        public int MaximumMessagesPerCatalog { get; }
        public int MaximumStringBytes { get; }
        public int MaximumSensitiveFieldsPerMessage { get; }

        /// <summary>
        /// Decode bounds used when the caller passes no options.
        /// The constructor arguments must be spelled out: for a struct, `new T()`
        /// binds to the implicit parameterless constructor and zeroes every field
        /// rather than calling the all-optional-parameter constructor above. The
        /// zeroed form is not a valid bound - it rejects every non-empty payload
        /// and every catalog - so Default would silently disable the codec.
        /// </summary>
        public static ProtocolCatalogAdvertisementDecodeOptions Default { get; } =
            new ProtocolCatalogAdvertisementDecodeOptions(
                DefaultMaximumPayloadBytes,
                DefaultMaximumCatalogs,
                DefaultMaximumMessagesPerCatalog,
                DefaultMaximumStringBytes,
                DefaultMaximumSensitiveFieldsPerMessage);
    }

    /// <summary>Deterministic, bounded codec for the system catalog advertisement payload.</summary>
    public static class ProtocolCatalogAdvertisementCodec
    {
        private const uint Magic = 0x41434B41; // "AKCA" in little endian.

        /// <summary>
        /// Version written by <see cref="Encode"/>. Version 2 added the per-message
        /// response id, capture sample rate and sensitive field list; version 1 payloads
        /// are still decoded, with those fields defaulted.
        /// </summary>
        private const ushort CurrentFormatVersion = 2;
        private const ushort MinimumSupportedFormatVersion = 1;

        private const int HeaderBytes = 8;
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public static byte[] Encode(ProtocolCatalogAdvertisement advertisement)
        {
            if (advertisement == null) throw new ArgumentNullException(nameof(advertisement));
            var bytes = new List<byte>(Math.Min(1048576, HeaderBytes + advertisement.Catalogs.Count * 128));
            AppendUInt32(bytes, Magic);
            AppendUInt16(bytes, CurrentFormatVersion);
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
                    AppendString(bytes, message.ResponseId);
                    AppendDouble(bytes, message.CaptureSampleRate);
                    AppendUInt16(bytes, CheckedCount(message.SensitiveFields.Count, "sensitive field"));
                    foreach (var sensitiveField in message.SensitiveFields)
                    {
                        AppendString(bytes, sensitiveField);
                    }
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
            if (!reader.TryUInt16(out var version) ||
                version < MinimumSupportedFormatVersion || version > CurrentFormatVersion)
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

                    var responseId = string.Empty;
                    var captureSampleRate = 1d;
                    IReadOnlyList<string> sensitiveFields = Array.Empty<string>();
                    if (version >= 2)
                    {
                        if (!reader.TryString(out responseId) || !reader.TryDouble(out captureSampleRate) ||
                            double.IsNaN(captureSampleRate) || double.IsInfinity(captureSampleRate))
                            return Fail("Invalid or truncated message advertisement.", out error);
                        if (!reader.TryUInt16(out var sensitiveFieldCount) ||
                            sensitiveFieldCount > options.MaximumSensitiveFieldsPerMessage)
                            return Fail("Truncated or oversized catalog advertisement.", out error);
                        if (sensitiveFieldCount > 0)
                        {
                            var fields = new List<string>(sensitiveFieldCount);
                            for (var k = 0; k < sensitiveFieldCount; k++)
                            {
                                if (!reader.TryString(out var sensitiveField))
                                    return Fail("Invalid or truncated message advertisement.", out error);
                                fields.Add(sensitiveField);
                            }
                            sensitiveFields = fields;
                        }
                    }

                    messages.Add(new ProtocolCatalogAdvertisementMessage(
                        id, opCode, (ProtocolDirection)direction, (ProtocolPacketKind)kind,
                        payloadType, codec, (ProtocolReliability)reliability, minimum, maximum, budget,
                        responseId, captureSampleRate, sensitiveFields));
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
                options.MaximumStringBytes > 0 ? options.MaximumStringBytes : defaults.MaximumStringBytes,
                options.MaximumSensitiveFieldsPerMessage > 0
                    ? options.MaximumSensitiveFieldsPerMessage
                    : defaults.MaximumSensitiveFieldsPerMessage);
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

        // BinaryPrimitives.WriteDoubleLittleEndian is .NET 5+; Unity targets
        // netstandard2.1 and does not have it. Writing the IEEE-754 bit pattern
        // as a little-endian Int64 produces the exact same eight bytes.
        private static void AppendDouble(List<byte> bytes, double value)
        {
            Span<byte> buffer = stackalloc byte[8];
            BinaryPrimitives.WriteInt64LittleEndian(buffer, BitConverter.DoubleToInt64Bits(value));
            bytes.AddRange(buffer.ToArray());
        }

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

            public bool TryDouble(out double value)
            {
                if (_payload.Length - _offset < 8) { value = 0d; return false; }
                value = BitConverter.Int64BitsToDouble(
                    BinaryPrimitives.ReadInt64LittleEndian(_payload.Slice(_offset, 8)));
                _offset += 8;
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
