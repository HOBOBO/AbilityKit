#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Network.Runtime;
using AbilityKit.Protocol;
using AbilityKit.Protocol.Catalog;

namespace AbilityKit.Network.Sdk.Diagnostics
{
    public enum NetworkRouteCatalogMappingStatus
    {
        Unresolved = 0,
        Mapped = 1,
        Ambiguous = 2
    }

    public readonly struct NetworkRouteCatalogCandidateSnapshot
    {
        internal NetworkRouteCatalogCandidateSnapshot(
            ProtocolCatalogDefinition catalog,
            ProtocolMessageDefinition message)
        {
            CatalogId = catalog.CatalogId;
            CatalogProjectId = catalog.ProjectId;
            Domain = catalog.Domain;
            MessageId = message.Id;
            PayloadType = message.PayloadType;
            Codec = message.Codec;
            Direction = message.Direction;
            Kind = message.Kind;
            CaptureSampleRate = message.CaptureSampleRate;
        }

        public string CatalogId { get; }
        public string CatalogProjectId { get; }
        public string Domain { get; }
        public string MessageId { get; }
        public string PayloadType { get; }
        public string Codec { get; }
        public ProtocolDirection Direction { get; }
        public ProtocolPacketKind Kind { get; }
        public double CaptureSampleRate { get; }
    }

    public readonly struct NetworkRouteDiagnosticsSnapshot
    {
        internal NetworkRouteDiagnosticsSnapshot(
            NetworkPacketRouteSnapshot route,
            ProtocolDirection? direction,
            ProtocolPacketKind? packetKind,
            IReadOnlyList<NetworkRouteCatalogCandidateSnapshot> candidates)
        {
            Route = route;
            Direction = direction;
            PacketKind = packetKind;
            Candidates = candidates;
            MappingStatus = candidates.Count == 0
                ? NetworkRouteCatalogMappingStatus.Unresolved
                : candidates.Count == 1
                    ? NetworkRouteCatalogMappingStatus.Mapped
                    : NetworkRouteCatalogMappingStatus.Ambiguous;
        }

        public NetworkPacketRouteSnapshot Route { get; }
        public ProtocolDirection? Direction { get; }
        public ProtocolPacketKind? PacketKind { get; }
        public NetworkRouteCatalogMappingStatus MappingStatus { get; }
        public IReadOnlyList<NetworkRouteCatalogCandidateSnapshot> Candidates { get; }
    }

    public readonly struct NetworkClientDiagnosticsSnapshot
    {
        internal NetworkClientDiagnosticsSnapshot(
            NetworkSdkClientKey key,
            int leaseCount,
            NetworkConnectionDiagnosticsSnapshot? connection,
            IReadOnlyList<NetworkRouteDiagnosticsSnapshot> routes)
        {
            Key = key;
            LeaseCount = leaseCount;
            Connection = connection;
            Routes = routes;
        }

        public NetworkSdkClientKey Key { get; }
        public int LeaseCount { get; }
        public bool SupportsDiagnostics => Connection.HasValue;
        public NetworkConnectionDiagnosticsSnapshot? Connection { get; }
        public IReadOnlyList<NetworkRouteDiagnosticsSnapshot> Routes { get; }
    }

    /// <summary>
    /// Aggregates stable SDK client identities, runtime connection state, router counters and
    /// conservative Protocol Catalog candidates for editor tooling and diagnostic exporters.
    /// </summary>
    public sealed class NetworkSdkDiagnosticsAggregator
    {
        public const string SharedCatalogProjectId = "abilitykit.shared";

        private readonly NetworkSdkClientHub _hub;
        private readonly ProtocolCatalogRegistry _catalogs;

        public NetworkSdkDiagnosticsAggregator(
            NetworkSdkClientHub hub,
            ProtocolCatalogRegistry catalogs)
        {
            _hub = hub ?? throw new ArgumentNullException(nameof(hub));
            _catalogs = catalogs ?? throw new ArgumentNullException(nameof(catalogs));
        }

        public IReadOnlyList<NetworkClientDiagnosticsSnapshot> Snapshot()
        {
            var clients = _hub.Snapshot();
            var catalogs = _catalogs.Snapshot();
            var snapshots = new NetworkClientDiagnosticsSnapshot[clients.Count];
            for (var i = 0; i < clients.Count; i++)
            {
                var client = clients[i];
                NetworkConnectionDiagnosticsSnapshot? connection = null;
                try
                {
                    if (client.Client.TryGetDiagnosticsSnapshot(out var current))
                        connection = current;
                }
                catch (ObjectDisposedException)
                {
                    // A client can be removed immediately after the Hub snapshot was copied.
                }

                var routes = connection.HasValue
                    ? MapRoutes(client.Key, connection.Value.PacketRouter, catalogs)
                    : Array.Empty<NetworkRouteDiagnosticsSnapshot>();
                snapshots[i] = new NetworkClientDiagnosticsSnapshot(
                    client.Key,
                    client.LeaseCount,
                    connection,
                    routes);
            }

            return snapshots;
        }

        private static IReadOnlyList<NetworkRouteDiagnosticsSnapshot> MapRoutes(
            NetworkSdkClientKey key,
            NetworkPacketRouterSnapshot? router,
            IReadOnlyList<ProtocolCatalogDefinition> catalogs)
        {
            if (!router.HasValue || router.Value.Routes == null)
                return Array.Empty<NetworkRouteDiagnosticsSnapshot>();

            var source = router.Value.Routes;
            var routes = new NetworkRouteDiagnosticsSnapshot[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                var route = source[i];
                ResolveProtocolIdentity(route.Kind, out var direction, out var packetKind);
                var candidates = FindCandidates(
                    key.ProjectId,
                    route.OpCode,
                    direction,
                    packetKind,
                    catalogs);
                routes[i] = new NetworkRouteDiagnosticsSnapshot(
                    route,
                    direction,
                    packetKind,
                    candidates);
            }

            return routes;
        }

        private static IReadOnlyList<NetworkRouteCatalogCandidateSnapshot> FindCandidates(
            string projectId,
            uint opCode,
            ProtocolDirection? direction,
            ProtocolPacketKind? packetKind,
            IReadOnlyList<ProtocolCatalogDefinition> catalogs)
        {
            if (!direction.HasValue || !packetKind.HasValue)
                return Array.Empty<NetworkRouteCatalogCandidateSnapshot>();

            var candidates = new List<NetworkRouteCatalogCandidateSnapshot>();
            for (var catalogIndex = 0; catalogIndex < catalogs.Count; catalogIndex++)
            {
                var catalog = catalogs[catalogIndex];
                if (!string.Equals(catalog.ProjectId, projectId, StringComparison.Ordinal) &&
                    !string.Equals(catalog.ProjectId, SharedCatalogProjectId, StringComparison.Ordinal))
                {
                    continue;
                }

                for (var messageIndex = 0; messageIndex < catalog.Messages.Count; messageIndex++)
                {
                    var message = catalog.Messages[messageIndex];
                    if (message.OpCode != opCode ||
                        message.Kind != packetKind.Value ||
                        (message.Direction != direction.Value &&
                         message.Direction != ProtocolDirection.Bidirectional))
                    {
                        continue;
                    }

                    candidates.Add(new NetworkRouteCatalogCandidateSnapshot(catalog, message));
                }
            }

            return candidates.ToArray();
        }

        private static void ResolveProtocolIdentity(
            NetworkPacketDispatchKind kind,
            out ProtocolDirection? direction,
            out ProtocolPacketKind? packetKind)
        {
            switch (kind)
            {
                case NetworkPacketDispatchKind.Request:
                    direction = ProtocolDirection.ClientToServer;
                    packetKind = ProtocolPacketKind.Request;
                    return;
                case NetworkPacketDispatchKind.Response:
                    direction = ProtocolDirection.ServerToClient;
                    packetKind = ProtocolPacketKind.Response;
                    return;
                case NetworkPacketDispatchKind.ServerPush:
                    direction = ProtocolDirection.ServerToClient;
                    packetKind = ProtocolPacketKind.Push;
                    return;
                default:
                    direction = null;
                    packetKind = null;
                    return;
            }
        }
    }
}
