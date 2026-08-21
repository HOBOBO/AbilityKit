#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Network.Runtime.Observability;
using AbilityKit.Protocol.Catalog;
using AbilityKit.Protocol.Generated;

namespace AbilityKit.Network.Sdk.Observability
{
    /// <summary>
    /// Shared multi-connection traffic source for editor tooling and diagnostic exporters.
    /// Project clients opt in by passing this monitor to NetworkSdkBuilder.ObserveTraffic.
    /// </summary>
    public sealed class NetworkTrafficMonitor : INetworkTrafficObserver
    {
        private static readonly Lazy<NetworkTrafficMonitor> Shared =
            new Lazy<NetworkTrafficMonitor>(CreateDefault, true);

        private readonly NetworkTrafficRingBuffer _buffer;
        private readonly NetworkTrafficInspector _inspector;

        public NetworkTrafficMonitor(
            int capacity,
            ProtocolCatalogRegistry catalogs,
            ProtocolPayloadDecoderRegistry decoders)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            Catalogs = catalogs ?? throw new ArgumentNullException(nameof(catalogs));
            Decoders = decoders ?? throw new ArgumentNullException(nameof(decoders));
            _buffer = new NetworkTrafficRingBuffer(capacity);
            _inspector = new NetworkTrafficInspector(Catalogs, Decoders);
        }

        /// <summary>Process-wide monitor used by the built-in Unity editor window.</summary>
        public static NetworkTrafficMonitor Default => Shared.Value;

        public ProtocolCatalogRegistry Catalogs { get; }
        public ProtocolPayloadDecoderRegistry Decoders { get; }
        public int Capacity => _buffer.Capacity;
        public int Count => _buffer.Count;
        public long DroppedCount => _buffer.DroppedCount;

        public void OnTraffic(NetworkTrafficEvent trafficEvent) => _buffer.OnTraffic(trafficEvent);

        public IReadOnlyList<NetworkTrafficEvent> Snapshot() => _buffer.Snapshot();

        public IReadOnlyList<NetworkTrafficInspectionRow> Inspect() => _inspector.Inspect(_buffer);

        public void Clear() => _buffer.Clear();

        private static NetworkTrafficMonitor CreateDefault() => new NetworkTrafficMonitor(
            8192,
            BuiltInProtocolCatalogs.CreateRegistry(),
            new ProtocolPayloadDecoderRegistry());
    }
}
