#nullable enable

using System;
using AbilityKit.Network.Protocol;

namespace AbilityKit.Network.Runtime.Observability
{
    public delegate INetworkTrafficObserver NetworkTrafficObserverFactory(
        NetworkTrafficConnectionContext context);

    public delegate bool NetworkTrafficCaptureFilter(
        NetworkTrafficDirection direction,
        NetworkPacketHeader header);

    public delegate NetworkTrafficCaptureFilter NetworkTrafficCaptureFilterFactory(
        NetworkTrafficConnectionContext context);

    /// <summary>Configures packet metadata capture without changing the wire pipeline.</summary>
    public sealed class NetworkTrafficCaptureOptions
    {
        public string ConnectionId = string.Empty;
        public string Role = string.Empty;
        public string CatalogId = string.Empty;
        public string TransportName = string.Empty;
        public int MaximumPayloadPreviewBytes;
        public NetworkTrafficObserverFactory? ObserverFactory;
        public NetworkTrafficCaptureFilter? Filter;
        public NetworkTrafficCaptureFilterFactory? FilterFactory;
        public Func<DateTimeOffset> UtcNowProvider = () => DateTimeOffset.UtcNow;
        public Action<Exception>? ObserverErrorHandler;

        internal void Validate()
        {
            if (MaximumPayloadPreviewBytes < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(MaximumPayloadPreviewBytes),
                    "Payload preview length cannot be negative.");
            if (ObserverFactory == null)
                throw new InvalidOperationException("A traffic observer factory is required when traffic capture is configured.");
            if (UtcNowProvider == null)
                throw new InvalidOperationException("A UTC clock provider is required when traffic capture is configured.");
        }
    }
}
