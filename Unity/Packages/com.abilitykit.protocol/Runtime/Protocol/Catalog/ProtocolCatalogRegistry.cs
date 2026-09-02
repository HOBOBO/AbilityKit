#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Protocol.Catalog
{
    public sealed class ProtocolCatalogRegistry
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, ProtocolCatalogDefinition> _catalogs =
            new Dictionary<string, ProtocolCatalogDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<ProtocolMessageKey, ProtocolMessageDefinition> _messagesByKey =
            new Dictionary<ProtocolMessageKey, ProtocolMessageDefinition>();
        private readonly Dictionary<string, ProtocolMessageDefinition> _messagesById =
            new Dictionary<string, ProtocolMessageDefinition>(StringComparer.Ordinal);

        public void Register(ProtocolCatalogDefinition catalog)
        {
            var validation = ProtocolCatalogValidator.Validate(catalog);
            if (!validation.IsValid)
                throw new ArgumentException(validation.Diagnostics[0].ToString(), nameof(catalog));

            lock (_gate)
            {
                if (_catalogs.ContainsKey(catalog.CatalogId))
                    throw new InvalidOperationException($"Protocol catalog '{catalog.CatalogId}' is already registered.");

                for (var i = 0; i < catalog.Messages.Count; i++)
                {
                    var message = catalog.Messages[i];
                    var qualifiedId = Qualify(catalog.CatalogId, message.Id);
                    var key = message.CreateKey(catalog.CatalogId);
                    if (_messagesById.ContainsKey(qualifiedId) || _messagesByKey.ContainsKey(key))
                        throw new InvalidOperationException($"Protocol message '{qualifiedId}' conflicts with an existing registration.");
                }

                _catalogs.Add(catalog.CatalogId, catalog);
                for (var i = 0; i < catalog.Messages.Count; i++)
                {
                    var message = catalog.Messages[i];
                    _messagesById.Add(Qualify(catalog.CatalogId, message.Id), message);
                    _messagesByKey.Add(message.CreateKey(catalog.CatalogId), message);
                }
            }
        }

        public bool TryGetCatalog(string catalogId, out ProtocolCatalogDefinition? catalog)
        {
            lock (_gate)
                return _catalogs.TryGetValue(catalogId ?? string.Empty, out catalog);
        }

        public IReadOnlyList<ProtocolCatalogDefinition> Snapshot()
        {
            lock (_gate)
            {
                var catalogs = new ProtocolCatalogDefinition[_catalogs.Count];
                _catalogs.Values.CopyTo(catalogs, 0);
                return catalogs;
            }
        }

        public bool TryGetMessage(
            string catalogId,
            string messageId,
            out ProtocolMessageDefinition? message)
        {
            lock (_gate)
                return _messagesById.TryGetValue(Qualify(catalogId, messageId), out message);
        }

        /// <summary>
        /// Selects the highest schema version supported by a registered message and a peer's
        /// advertised range. Call this during connection/session negotiation before decoding.
        /// </summary>
        public bool TryNegotiateSchemaVersion(
            string catalogId,
            string messageId,
            int peerMinimumSchemaVersion,
            int peerMaximumSchemaVersion,
            out int selectedSchemaVersion)
        {
            if (!TryGetMessage(catalogId, messageId, out var message) || message == null)
            {
                selectedSchemaVersion = 0;
                return false;
            }

            return ProtocolSchemaVersionNegotiator.TrySelect(
                message,
                peerMinimumSchemaVersion,
                peerMaximumSchemaVersion,
                out selectedSchemaVersion);
        }

        public bool TryNegotiateCatalog(
            ProtocolCatalogDefinition remote,
            out ProtocolCatalogNegotiationResult? result)
        {
            if (remote == null)
            {
                result = null;
                return false;
            }

            lock (_gate)
            {
                if (!_catalogs.TryGetValue(remote.CatalogId, out var local))
                {
                    result = null;
                    return false;
                }

                result = ProtocolCatalogNegotiator.Negotiate(local, remote);
                return result.IsCompatible;
            }
        }

        /// <summary>
        /// Negotiates a multi-catalog advertisement for a single physical connection. Catalogs
        /// unknown to this registry are treated as optional; every shared catalog must pass the
        /// normal identity and schema-window checks.
        /// </summary>
        public bool TryNegotiateAdvertisement(
            ProtocolCatalogAdvertisement remoteAdvertisement,
            out ProtocolCatalogAdvertisementNegotiationResult? result)
        {
            if (remoteAdvertisement == null)
            {
                result = null;
                return false;
            }

            lock (_gate)
            {
                var localCatalogs = new ProtocolCatalogDefinition[_catalogs.Count];
                _catalogs.Values.CopyTo(localCatalogs, 0);
                result = ProtocolCatalogAdvertisementNegotiator.Negotiate(
                    localCatalogs,
                    remoteAdvertisement);
                return result.IsCompatible;
            }
        }

        public bool TryGetMessage(
            in ProtocolMessageKey key,
            out ProtocolMessageDefinition? message)
        {
            lock (_gate)
                return _messagesByKey.TryGetValue(key, out message);
        }

        /// <summary>
        /// Finds all definitions that can describe a packet with the supplied transport
        /// identity. A catalog may intentionally contain more than one packet kind for one
        /// opcode, so callers should treat more than one result as ambiguous unless they have
        /// an additional packet-kind signal from the transport header.
        /// </summary>
        public IReadOnlyList<ProtocolMessageDefinition> FindMessages(
            string catalogId,
            uint opCode,
            ProtocolDirection direction,
            ProtocolPacketKind? kind = null)
        {
            var matches = new List<ProtocolMessageDefinition>();
            lock (_gate)
            {
                foreach (var catalog in _catalogs.Values)
                {
                    if (!string.Equals(catalog.CatalogId, catalogId, StringComparison.Ordinal))
                        continue;

                    foreach (var message in catalog.Messages)
                    {
                        if (message.OpCode != opCode ||
                            (message.Direction != direction &&
                             message.Direction != ProtocolDirection.Bidirectional) ||
                            (kind.HasValue && message.Kind != kind.Value))
                            continue;

                        matches.Add(message);
                    }
                }
            }

            return matches.ToArray();
        }

        private static string Qualify(string catalogId, string messageId) =>
            $"{catalogId ?? string.Empty}/{messageId ?? string.Empty}";
    }
}
