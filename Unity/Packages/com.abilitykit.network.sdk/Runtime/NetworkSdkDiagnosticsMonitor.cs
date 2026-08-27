#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Network.Sdk.Observability;
using AbilityKit.Protocol.Catalog;

namespace AbilityKit.Network.Sdk.Diagnostics
{
    /// <summary>
    /// Process-wide composition root for SDK clients that should appear in the built-in
    /// diagnostics window. Consumers acquire clients through <see cref="Hub"/> while editor
    /// tooling reads immutable snapshots through <see cref="Snapshot"/>.
    /// </summary>
    public sealed class NetworkSdkDiagnosticsMonitor
    {
        private static readonly Lazy<NetworkSdkDiagnosticsMonitor> Shared =
            new Lazy<NetworkSdkDiagnosticsMonitor>(CreateDefault, true);

        private readonly NetworkSdkDiagnosticsAggregator _aggregator;

        public NetworkSdkDiagnosticsMonitor(
            NetworkSdkClientHub hub,
            ProtocolCatalogRegistry catalogs)
        {
            Hub = hub ?? throw new ArgumentNullException(nameof(hub));
            Catalogs = catalogs ?? throw new ArgumentNullException(nameof(catalogs));
            _aggregator = new NetworkSdkDiagnosticsAggregator(Hub, Catalogs);
        }

        /// <summary>Process-wide source consumed by the built-in Unity editor window.</summary>
        public static NetworkSdkDiagnosticsMonitor Default => Shared.Value;

        public NetworkSdkClientHub Hub { get; }

        public ProtocolCatalogRegistry Catalogs { get; }

        public IReadOnlyList<NetworkClientDiagnosticsSnapshot> Snapshot() =>
            _aggregator.Snapshot();

        private static NetworkSdkDiagnosticsMonitor CreateDefault() =>
            new NetworkSdkDiagnosticsMonitor(
                new NetworkSdkClientHub(),
                NetworkTrafficMonitor.Default.Catalogs);
    }
}
