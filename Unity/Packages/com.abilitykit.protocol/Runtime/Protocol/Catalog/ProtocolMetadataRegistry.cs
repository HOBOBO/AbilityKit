#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Protocol.Catalog
{
    /// <summary>Stable generated metadata for one catalog message.</summary>
    public sealed class ProtocolMessageMetadata
    {
        public ProtocolMessageMetadata(
            string catalogId,
            string messageId,
            uint opCode,
            ProtocolDirection direction,
            ProtocolPacketKind kind,
            string payloadType,
            string codec,
            ProtocolReliability reliability,
            string? responseId,
            string source,
            int minimumSchemaVersion = 1,
            int maximumSchemaVersion = 1,
            int maximumPayloadBytes = 1048576,
            double captureSampleRate = 1d,
            IReadOnlyList<string>? sensitiveFields = null)
        {
            CatalogId = catalogId ?? string.Empty;
            MessageId = messageId ?? string.Empty;
            OpCode = opCode;
            Direction = direction;
            Kind = kind;
            PayloadType = payloadType ?? string.Empty;
            Codec = codec ?? string.Empty;
            Reliability = reliability;
            ResponseId = responseId ?? string.Empty;
            Source = source ?? string.Empty;
            MinimumSchemaVersion = minimumSchemaVersion;
            MaximumSchemaVersion = maximumSchemaVersion;
            MaximumPayloadBytes = maximumPayloadBytes;
            CaptureSampleRate = captureSampleRate;
            SensitiveFields = sensitiveFields ?? Array.Empty<string>();
        }

        public string CatalogId { get; }
        public string MessageId { get; }
        public uint OpCode { get; }
        public ProtocolDirection Direction { get; }
        public ProtocolPacketKind Kind { get; }
        public string PayloadType { get; }
        public string Codec { get; }
        public ProtocolReliability Reliability { get; }
        public string ResponseId { get; }
        public string Source { get; }
        public int MinimumSchemaVersion { get; }
        public int MaximumSchemaVersion { get; }
        public int MaximumPayloadBytes { get; }
        public double CaptureSampleRate { get; }
        public IReadOnlyList<string> SensitiveFields { get; }
        public string QualifiedId => $"{CatalogId}/{MessageId}";
    }

    /// <summary>
    /// Compatibility metadata view. New runtime composition should construct this view from the
    /// canonical <see cref="ProtocolCatalogRegistry"/>; the parameterless registration mode is
    /// retained for generated-code and binary compatibility.
    /// </summary>
    public sealed class ProtocolMetadataRegistry
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, ProtocolMessageMetadata> _byId =
            new Dictionary<string, ProtocolMessageMetadata>(StringComparer.Ordinal);
        private readonly List<ProtocolMessageMetadata> _all = new List<ProtocolMessageMetadata>();
        private readonly ProtocolCatalogRegistry? _catalogs;
        private readonly IReadOnlyDictionary<string, string>? _sources;

        public ProtocolMetadataRegistry()
        {
        }

        public ProtocolMetadataRegistry(
            ProtocolCatalogRegistry catalogs,
            IReadOnlyDictionary<string, string>? sources = null)
        {
            _catalogs = catalogs ?? throw new ArgumentNullException(nameof(catalogs));
            _sources = sources;
        }

        public bool IsCatalogBacked => _catalogs != null;

        public void Register(ProtocolMessageMetadata metadata)
        {
            if (metadata == null) throw new ArgumentNullException(nameof(metadata));
            if (_catalogs != null)
                throw new InvalidOperationException(
                    "Catalog-backed protocol metadata is read-only. Register the owning catalog in ProtocolCatalogRegistry instead.");
            if (string.IsNullOrWhiteSpace(metadata.CatalogId) || string.IsNullOrWhiteSpace(metadata.MessageId))
                throw new ArgumentException("CatalogId and MessageId are required.", nameof(metadata));

            lock (_gate)
            {
                if (_byId.ContainsKey(metadata.QualifiedId))
                    throw new InvalidOperationException($"Protocol metadata '{metadata.QualifiedId}' is already registered.");
                _byId.Add(metadata.QualifiedId, metadata);
                _all.Add(metadata);
            }
        }

        public bool TryGet(string catalogId, string messageId, out ProtocolMessageMetadata? metadata)
        {
            if (_catalogs != null)
            {
                if (_catalogs.TryGetMessage(catalogId, messageId, out var definition))
                {
                    metadata = Project(catalogId, definition!);
                    return true;
                }

                metadata = null;
                return false;
            }

            lock (_gate)
                return _byId.TryGetValue($"{catalogId ?? string.Empty}/{messageId ?? string.Empty}", out metadata);
        }

        public IReadOnlyList<ProtocolMessageMetadata> FindByOpCode(uint opCode)
        {
            if (_catalogs != null)
            {
                var projected = new List<ProtocolMessageMetadata>();
                var catalogs = _catalogs.Snapshot();
                for (var catalogIndex = 0; catalogIndex < catalogs.Count; catalogIndex++)
                {
                    var catalog = catalogs[catalogIndex];
                    for (var messageIndex = 0; messageIndex < catalog.Messages.Count; messageIndex++)
                    {
                        var message = catalog.Messages[messageIndex];
                        if (message.OpCode == opCode)
                            projected.Add(Project(catalog.CatalogId, message));
                    }
                }

                return projected.ToArray();
            }

            var result = new List<ProtocolMessageMetadata>();
            lock (_gate)
            {
                for (var i = 0; i < _all.Count; i++)
                    if (_all[i].OpCode == opCode) result.Add(_all[i]);
            }

            return result.ToArray();
        }

        public IReadOnlyList<ProtocolMessageMetadata> All
        {
            get
            {
                if (_catalogs != null)
                {
                    var projected = new List<ProtocolMessageMetadata>();
                    var catalogs = _catalogs.Snapshot();
                    for (var catalogIndex = 0; catalogIndex < catalogs.Count; catalogIndex++)
                    {
                        var catalog = catalogs[catalogIndex];
                        for (var messageIndex = 0; messageIndex < catalog.Messages.Count; messageIndex++)
                            projected.Add(Project(catalog.CatalogId, catalog.Messages[messageIndex]));
                    }

                    return projected.ToArray();
                }

                lock (_gate) return _all.ToArray();
            }
        }

        private ProtocolMessageMetadata Project(
            string catalogId,
            ProtocolMessageDefinition definition)
        {
            var qualifiedId = $"{catalogId ?? string.Empty}/{definition.Id}";
            var source = string.Empty;
            _sources?.TryGetValue(qualifiedId, out source);
            return new ProtocolMessageMetadata(
                catalogId,
                definition.Id,
                definition.OpCode,
                definition.Direction,
                definition.Kind,
                definition.PayloadType,
                definition.Codec,
                definition.Reliability,
                definition.ResponseId,
                source ?? string.Empty,
                definition.MinimumSchemaVersion,
                definition.MaximumSchemaVersion,
                definition.MaximumPayloadBytes,
                definition.CaptureSampleRate,
                definition.SensitiveFields);
        }
    }

    /// <summary>Runtime entry point used by generated metadata classes.</summary>
    public static class ProtocolStaticRegistry
    {
        public static ProtocolMetadataRegistry Create(IReadOnlyList<ProtocolMessageMetadata> metadata)
        {
            var registry = new ProtocolMetadataRegistry();
            RegisterAll(registry, metadata);
            return registry;
        }

        public static ProtocolMetadataRegistry Create(
            ProtocolCatalogRegistry catalogs,
            IReadOnlyDictionary<string, string>? sources = null) =>
            new ProtocolMetadataRegistry(catalogs, sources);

        public static void RegisterAll(
            ProtocolMetadataRegistry registry,
            IReadOnlyList<ProtocolMessageMetadata> metadata)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (metadata == null) throw new ArgumentNullException(nameof(metadata));
            for (var i = 0; i < metadata.Count; i++) registry.Register(metadata[i]);
        }
    }
}
