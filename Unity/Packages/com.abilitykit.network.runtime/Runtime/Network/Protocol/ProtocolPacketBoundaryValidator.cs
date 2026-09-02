#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Protocol;
using AbilityKit.Protocol.Catalog;
using AbilityKit.Network.Runtime;

namespace AbilityKit.Network.Protocol
{
    public enum ProtocolPacketBoundaryFailureKind
    {
        None = 0,
        UnknownMessage = 1,
        AmbiguousMessage = 2,
        UnsupportedSchemaVersion = 3,
        PayloadTooLarge = 4,
        MalformedPayloadLength = 5,
        NegotiationPending = 6
    }

    /// <summary>
    /// Applies catalog-owned limits to inbound packets before they reach business handlers. This
    /// validator intentionally does not decode payloads: decoding remains the responsibility of
    /// the registered message handler, while size/version checks happen exactly once at the
    /// transport boundary.
    /// </summary>
    public sealed class ProtocolPacketBoundaryValidator
    {
        private readonly ProtocolCatalogRegistry _catalogs;
        private readonly string _catalogId;
        private readonly int? _schemaVersion;
        private readonly IReadOnlyDictionary<string, int>? _selectedSchemaVersions;
        private readonly ProtocolCatalogNegotiationSession? _negotiationSession;
        private readonly HashSet<string> _bootstrapMessageIds;

        public ProtocolPacketBoundaryValidator(
            ProtocolCatalogRegistry catalogs,
            string catalogId,
            int? schemaVersion = null,
            bool rejectUnknownMessages = false,
            Action<ProtocolPacketBoundaryFailureKind, NetworkPacketHeader>? failure = null,
            IReadOnlyDictionary<string, int>? selectedSchemaVersions = null,
            ProtocolCatalogNegotiationSession? negotiationSession = null,
            bool requireNegotiated = false,
            IReadOnlyCollection<string>? bootstrapMessageIds = null)
        {
            _catalogs = catalogs ?? throw new ArgumentNullException(nameof(catalogs));
            if (string.IsNullOrWhiteSpace(catalogId))
                throw new ArgumentException("Catalog id is required.", nameof(catalogId));
            _catalogId = catalogId;
            _schemaVersion = schemaVersion;
            _selectedSchemaVersions = selectedSchemaVersions;
            _negotiationSession = negotiationSession;
            _bootstrapMessageIds = new HashSet<string>(StringComparer.Ordinal);
            if (bootstrapMessageIds != null)
            {
                foreach (var messageId in bootstrapMessageIds)
                {
                    if (!string.IsNullOrWhiteSpace(messageId))
                        _bootstrapMessageIds.Add(messageId);
                }
            }
            RequireNegotiated = requireNegotiated;
            RejectUnknownMessages = rejectUnknownMessages;
            Failure = failure;
        }

        public bool RejectUnknownMessages { get; }
        public bool RequireNegotiated { get; }
        public Action<ProtocolPacketBoundaryFailureKind, NetworkPacketHeader>? Failure { get; }
        public ProtocolCatalogNegotiationSession? NegotiationSession => _negotiationSession;

        public void BeginConnection(int connectionGeneration) =>
            _negotiationSession?.Reset(connectionGeneration);

        public ProtocolCatalogNegotiationResult ApplyRemoteCatalog(
            ProtocolCatalogDefinition remoteCatalog)
        {
            if (_negotiationSession == null)
                throw new InvalidOperationException("No catalog negotiation session is configured.");
            return _negotiationSession.ApplyRemoteCatalog(remoteCatalog);
        }

        public bool Validate(NetworkPacketHeader header, ArraySegment<byte> payload)
        {
            if (header.PayloadLength != (uint)Math.Max(0, payload.Count))
                return Reject(ProtocolPacketBoundaryFailureKind.MalformedPayloadLength, header, true);

            var kind = NetworkPacketRouter.ResolveKind(header.Flags);
            var direction = kind == NetworkPacketDispatchKind.Request
                ? ProtocolDirection.ClientToServer
                : ProtocolDirection.ServerToClient;
            var protocolKind = kind == NetworkPacketDispatchKind.Request
                ? ProtocolPacketKind.Request
                : kind == NetworkPacketDispatchKind.Response
                    ? ProtocolPacketKind.Response
                    : kind == NetworkPacketDispatchKind.ServerPush
                        ? ProtocolPacketKind.Push
                        : (ProtocolPacketKind?)null;

            if (!protocolKind.HasValue)
                return Reject(ProtocolPacketBoundaryFailureKind.UnknownMessage, header, RejectUnknownMessages);

            var candidates = _catalogs.FindMessages(_catalogId, header.OpCode, direction, protocolKind);
            if (candidates.Count == 0)
                return Reject(ProtocolPacketBoundaryFailureKind.UnknownMessage, header, RejectUnknownMessages);
            if (candidates.Count != 1)
                return Reject(ProtocolPacketBoundaryFailureKind.AmbiguousMessage, header, true);

            var definition = candidates[0];
            if (RequireNegotiated &&
                (_negotiationSession == null || !_negotiationSession.IsNegotiated) &&
                !_bootstrapMessageIds.Contains(definition.Id))
                return Reject(ProtocolPacketBoundaryFailureKind.NegotiationPending, header, true);
            var schemaVersion = _schemaVersion;
            if (_negotiationSession?.Result != null &&
                _negotiationSession.Result.TryGetSchemaVersion(definition.Id, out var negotiatedVersion))
                schemaVersion = negotiatedVersion;
            if (_selectedSchemaVersions != null &&
                _selectedSchemaVersions.TryGetValue(definition.Id, out var selectedVersion))
                schemaVersion = selectedVersion;
            if (schemaVersion.HasValue &&
                (schemaVersion.Value < definition.MinimumSchemaVersion ||
                 schemaVersion.Value > definition.MaximumSchemaVersion))
                return Reject(ProtocolPacketBoundaryFailureKind.UnsupportedSchemaVersion, header, true);
            if (payload.Count > definition.MaximumPayloadBytes)
                return Reject(ProtocolPacketBoundaryFailureKind.PayloadTooLarge, header, true);

            return true;
        }

        private bool Reject(
            ProtocolPacketBoundaryFailureKind kind,
            NetworkPacketHeader header,
            bool reject)
        {
            if (reject) Failure?.Invoke(kind, header);
            return !reject;
        }
    }
}
