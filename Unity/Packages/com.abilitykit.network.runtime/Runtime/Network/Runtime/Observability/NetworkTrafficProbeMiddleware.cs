#nullable enable

using System;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;

namespace AbilityKit.Network.Runtime.Observability
{
    /// <summary>Mirrors bounded packet metadata to an observer and always forwards the packet.</summary>
    public sealed class NetworkTrafficProbeMiddleware : INetworkMiddleware
    {
        private readonly NetworkTrafficConnectionContext _connection;
        private readonly INetworkTrafficObserver _observer;
        private readonly int _maximumPayloadPreviewBytes;
        private readonly NetworkTrafficCaptureFilter? _filter;
        private readonly Func<DateTimeOffset> _utcNowProvider;
        private readonly Action<Exception>? _observerErrorHandler;

        public NetworkTrafficProbeMiddleware(
            NetworkTrafficConnectionContext connection,
            INetworkTrafficObserver observer,
            int maximumPayloadPreviewBytes = 0,
            NetworkTrafficCaptureFilter? filter = null,
            Func<DateTimeOffset>? utcNowProvider = null,
            Action<Exception>? observerErrorHandler = null)
        {
            if (maximumPayloadPreviewBytes < 0)
                throw new ArgumentOutOfRangeException(nameof(maximumPayloadPreviewBytes));

            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _observer = observer ?? throw new ArgumentNullException(nameof(observer));
            _maximumPayloadPreviewBytes = maximumPayloadPreviewBytes;
            _filter = filter;
            _utcNowProvider = utcNowProvider ?? (() => DateTimeOffset.UtcNow);
            _observerErrorHandler = observerErrorHandler;
        }

        public void OnInbound(
            ISessionContext context,
            NetworkPacketHeader header,
            ArraySegment<byte> payload,
            Action<NetworkPacketHeader, ArraySegment<byte>> next)
        {
            Observe(NetworkTrafficDirection.Inbound, header, payload);
            next(header, payload);
        }

        public void OnOutbound(
            ISessionContext context,
            NetworkPacketHeader header,
            ArraySegment<byte> payload,
            Action<NetworkPacketHeader, ArraySegment<byte>> next)
        {
            Observe(NetworkTrafficDirection.Outbound, header, payload);
            next(header, payload);
        }

        private void Observe(
            NetworkTrafficDirection direction,
            NetworkPacketHeader header,
            ArraySegment<byte> payload)
        {
            try
            {
                if (_filter != null && !_filter(direction, header))
                    return;

                var payloadLength = payload.Array == null ? 0 : payload.Count;
                var previewLength = Math.Min(payloadLength, _maximumPayloadPreviewBytes);
                var preview = previewLength == 0 ? Array.Empty<byte>() : new byte[previewLength];
                if (previewLength > 0)
                    Buffer.BlockCopy(payload.Array!, payload.Offset, preview, 0, previewLength);

                _observer.OnTraffic(new NetworkTrafficEvent(
                    _connection,
                    _utcNowProvider(),
                    direction,
                    header,
                    payloadLength,
                    preview));
            }
            catch (Exception exception)
            {
                try
                {
                    _observerErrorHandler?.Invoke(exception);
                }
                catch
                {
                    // Diagnostics must never interrupt packet forwarding.
                }
            }
        }
    }
}
