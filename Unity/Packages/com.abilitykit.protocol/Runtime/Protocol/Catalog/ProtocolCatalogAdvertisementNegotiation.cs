#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AbilityKit.Protocol.Catalog
{
    /// <summary>Aggregate result for negotiating every catalog shared by a connection.</summary>
    public sealed class ProtocolCatalogAdvertisementNegotiationResult
    {
        internal ProtocolCatalogAdvertisementNegotiationResult(
            bool compatible,
            IReadOnlyDictionary<string, ProtocolCatalogNegotiationResult> catalogs,
            IReadOnlyList<string> incompatibleCatalogIds)
        {
            IsCompatible = compatible;
            Catalogs = new ReadOnlyDictionary<string, ProtocolCatalogNegotiationResult>(
                new Dictionary<string, ProtocolCatalogNegotiationResult>(catalogs, StringComparer.Ordinal));
            IncompatibleCatalogIds = new ReadOnlyCollection<string>(incompatibleCatalogIds.ToArray());
        }

        public bool IsCompatible { get; }
        public IReadOnlyDictionary<string, ProtocolCatalogNegotiationResult> Catalogs { get; }
        public IReadOnlyList<string> IncompatibleCatalogIds { get; }

        public bool TryGetCatalogResult(
            string catalogId,
            out ProtocolCatalogNegotiationResult? result) =>
            Catalogs.TryGetValue(catalogId ?? string.Empty, out result);
    }

    public static class ProtocolCatalogAdvertisementNegotiator
    {
        /// <summary>
        /// Negotiates all remote catalogs that are known locally. Unknown remote catalogs are
        /// ignored to allow optional feature groups; a shared catalog must be compatible.
        /// </summary>
        public static ProtocolCatalogAdvertisementNegotiationResult Negotiate(
            IReadOnlyCollection<ProtocolCatalogDefinition> localCatalogs,
            ProtocolCatalogAdvertisement remoteAdvertisement)
        {
            if (localCatalogs == null) throw new ArgumentNullException(nameof(localCatalogs));
            if (remoteAdvertisement == null) throw new ArgumentNullException(nameof(remoteAdvertisement));

            var localById = localCatalogs.ToDictionary(catalog => catalog.CatalogId, StringComparer.Ordinal);
            var results = new Dictionary<string, ProtocolCatalogNegotiationResult>(StringComparer.Ordinal);
            var incompatible = new List<string>();
            foreach (var remote in remoteAdvertisement.Catalogs)
            {
                if (!localById.TryGetValue(remote.CatalogId, out var local))
                    continue;
                var result = ProtocolCatalogNegotiator.Negotiate(local, remote.ToCatalogDefinition());
                results[remote.CatalogId] = result;
                if (!result.IsCompatible)
                    incompatible.Add(remote.CatalogId);
            }

            return new ProtocolCatalogAdvertisementNegotiationResult(
                incompatible.Count == 0,
                results,
                incompatible);
        }
    }
}
