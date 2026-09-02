#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime.Observability;
using AbilityKit.Protocol;
using AbilityKit.Protocol.Catalog;

namespace AbilityKit.Network.Sdk.Observability
{
    /// <summary>Resolved view of one captured packet for editor, diagnostics, or export layers.</summary>
    public sealed class NetworkTrafficInspectionRow
    {
        internal NetworkTrafficInspectionRow(
            NetworkTrafficEvent traffic,
            IReadOnlyList<ProtocolMessageDefinition> candidates,
            ProtocolDecodeResult decode)
        {
            Traffic = traffic ?? throw new ArgumentNullException(nameof(traffic));
            Candidates = candidates ?? throw new ArgumentNullException(nameof(candidates));
            Decode = decode;
        }

        /// <summary>Original immutable transport observation.</summary>
        public NetworkTrafficEvent Traffic { get; }

        /// <summary>Catalog definitions matching the available transport identity.</summary>
        public IReadOnlyList<ProtocolMessageDefinition> Candidates { get; }

        /// <summary>The resolved message, or null when the packet is unknown or ambiguous.</summary>
        public ProtocolMessageDefinition? Message => Candidates.Count == 1 ? Candidates[0] : null;

        /// <summary>True when exactly one catalog message matches the packet.</summary>
        public bool IsKnown => Message != null;

        /// <summary>True when more than one catalog message matches the packet.</summary>
        public bool IsAmbiguous => Candidates.Count > 1;

        /// <summary>Decoded payload value or a contained diagnostic explaining why decoding failed.</summary>
        public ProtocolDecodeResult Decode { get; }
    }

    /// <summary>
    /// Joins transport observations with project-owned protocol metadata and decoders. This is
    /// intentionally a plain runtime service so Unity editor windows, headless tools, and local
    /// diagnostics can share the exact same matching and truncation behavior.
    /// </summary>
    public sealed class NetworkTrafficInspector
    {
        private readonly ProtocolCatalogRegistry _catalogs;
        private readonly ProtocolPayloadDecoderRegistry _decoders;

        /// <summary>Creates an inspector over the supplied catalog and decoder registries.</summary>
        public NetworkTrafficInspector(
            ProtocolCatalogRegistry catalogs,
            ProtocolPayloadDecoderRegistry decoders)
        {
            _catalogs = catalogs ?? throw new ArgumentNullException(nameof(catalogs));
            _decoders = decoders ?? throw new ArgumentNullException(nameof(decoders));
        }

        /// <summary>Creates a stable inspection snapshot from a bounded traffic collector.</summary>
        public IReadOnlyList<NetworkTrafficInspectionRow> Inspect(NetworkTrafficRingBuffer buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            return Inspect(buffer.Snapshot());
        }

        /// <summary>Inspects an existing stable sequence of captured traffic events.</summary>
        public IReadOnlyList<NetworkTrafficInspectionRow> Inspect(
            IReadOnlyList<NetworkTrafficEvent> traffic)
        {
            if (traffic == null) throw new ArgumentNullException(nameof(traffic));

            var rows = new NetworkTrafficInspectionRow[traffic.Count];
            for (var i = 0; i < traffic.Count; i++)
            {
                var item = traffic[i] ?? throw new ArgumentException(
                    "Traffic collections cannot contain null events.", nameof(traffic));
                rows[i] = InspectOne(item);
            }

            return rows;
        }

        private NetworkTrafficInspectionRow InspectOne(NetworkTrafficEvent traffic)
        {
            var direction = traffic.Direction == NetworkTrafficDirection.Outbound
                ? ProtocolDirection.ClientToServer
                : ProtocolDirection.ServerToClient;
            var kind = TryGetPacketKind(traffic.Flags);
            var candidates = _catalogs.FindMessages(
                traffic.CatalogId,
                traffic.OpCode,
                direction,
                kind);

            ProtocolDecodeResult decode;
            if (candidates.Count == 0)
                decode = ProtocolDecodeResult.Failed(
                    "No matching protocol message is registered.",
                    ProtocolDecodeFailureKind.UnknownMessage);
            else if (candidates.Count > 1)
                decode = ProtocolDecodeResult.Failed(
                    "Packet kind is ambiguous for this traffic event.",
                    ProtocolDecodeFailureKind.AmbiguousMessage);
            else if (traffic.IsPayloadPreviewTruncated)
                decode = ProtocolDecodeResult.Failed(
                    "Payload preview is truncated; capture the complete payload before decoding.",
                    ProtocolDecodeFailureKind.PayloadPreviewTruncated);
            else
            {
                var payload = new ArraySegment<byte>(traffic.PayloadPreview.ToArray());
                decode = _decoders.Decode(traffic.CatalogId, candidates[0], payload);
            }

            return new NetworkTrafficInspectionRow(traffic, candidates, decode);
        }

        private static ProtocolPacketKind? TryGetPacketKind(NetworkPacketFlags flags)
        {
            var hasRequest = (flags & NetworkPacketFlags.Request) != 0;
            var hasResponse = (flags & NetworkPacketFlags.Response) != 0;
            var hasPush = (flags & NetworkPacketFlags.ServerPush) != 0;
            var count = (hasRequest ? 1 : 0) + (hasResponse ? 1 : 0) + (hasPush ? 1 : 0);
            if (count != 1) return null;
            if (hasRequest) return ProtocolPacketKind.Request;
            if (hasResponse) return ProtocolPacketKind.Response;
            return ProtocolPacketKind.Push;
        }
    }
}
