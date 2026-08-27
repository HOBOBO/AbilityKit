#nullable enable

using System;
using System.Threading;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime.Observability;
using AbilityKit.Protocol;
using AbilityKit.Protocol.Catalog;

namespace AbilityKit.Network.Sdk.Observability
{
    public readonly struct NetworkTrafficSamplingSnapshot
    {
        internal NetworkTrafficSamplingSnapshot(long examined, long captured, long sampledOut, long unresolved)
        {
            Examined = examined;
            Captured = captured;
            SampledOut = sampledOut;
            Unresolved = unresolved;
        }

        public long Examined { get; }
        public long Captured { get; }
        public long SampledOut { get; }
        public long Unresolved { get; }
    }

    public sealed class NetworkTrafficSamplingMetrics
    {
        private long _examined;
        private long _captured;
        private long _sampledOut;
        private long _unresolved;

        internal void Record(bool captured, bool unresolved)
        {
            Interlocked.Increment(ref _examined);
            if (captured) Interlocked.Increment(ref _captured);
            else Interlocked.Increment(ref _sampledOut);
            if (unresolved) Interlocked.Increment(ref _unresolved);
        }

        public NetworkTrafficSamplingSnapshot GetSnapshot() => new NetworkTrafficSamplingSnapshot(
            Interlocked.Read(ref _examined),
            Interlocked.Read(ref _captured),
            Interlocked.Read(ref _sampledOut),
            Interlocked.Read(ref _unresolved));
    }

    /// <summary>Creates deterministic Catalog capture filters before payload preview allocation.</summary>
    public static class NetworkTrafficCatalogSampler
    {
        /// <summary>
        /// Creates one filter per physical connection generation. Unknown or ambiguous packets are
        /// retained because no single Catalog sampling policy can be selected safely.
        /// </summary>
        public static NetworkTrafficCaptureFilterFactory CreateFilterFactory(
            ProtocolCatalogRegistry catalogs,
            NetworkTrafficCaptureFilter? innerFilter = null,
            NetworkTrafficSamplingMetrics? metrics = null)
        {
            if (catalogs == null) throw new ArgumentNullException(nameof(catalogs));
            return context => CreateFilter(context, catalogs, innerFilter, metrics);
        }

        public static NetworkTrafficCaptureFilter CreateFilter(
            NetworkTrafficConnectionContext connection,
            ProtocolCatalogRegistry catalogs,
            NetworkTrafficCaptureFilter? innerFilter = null,
            NetworkTrafficSamplingMetrics? metrics = null)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (catalogs == null) throw new ArgumentNullException(nameof(catalogs));

            return (direction, header) =>
            {
                if (innerFilter != null && !innerFilter(direction, header))
                {
                    metrics?.Record(captured: false, unresolved: false);
                    return false;
                }

                var protocolDirection = direction == NetworkTrafficDirection.Outbound
                    ? ProtocolDirection.ClientToServer
                    : ProtocolDirection.ServerToClient;
                var candidates = catalogs.FindMessages(
                    connection.CatalogId,
                    header.OpCode,
                    protocolDirection,
                    TryGetPacketKind(header.Flags));
                if (candidates.Count != 1)
                {
                    metrics?.Record(captured: true, unresolved: true);
                    return true;
                }

                var captured = ShouldCapture(
                    candidates[0].CaptureSampleRate,
                    connection,
                    direction,
                    header);
                metrics?.Record(captured, unresolved: false);
                return captured;
            };
        }

        /// <summary>Evaluates the stable FNV-1a sampling bucket for one packet identity.</summary>
        public static bool ShouldCapture(
            double sampleRate,
            NetworkTrafficConnectionContext connection,
            NetworkTrafficDirection direction,
            NetworkPacketHeader header)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (double.IsNaN(sampleRate) || sampleRate < 0d || sampleRate > 1d)
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (sampleRate <= 0d) return false;
            if (sampleRate >= 1d) return true;

            var hash = StableHash.Start();
            hash = StableHash.Append(hash, connection.CatalogId);
            hash = StableHash.Append(hash, connection.ConnectionId);
            hash = StableHash.Append(hash, connection.Generation);
            hash = StableHash.Append(hash, (int)direction);
            hash = StableHash.Append(hash, header.OpCode);
            hash = StableHash.Append(hash, header.Seq);
            hash = StableHash.Append(hash, (uint)header.Flags);

            // Taking the top 53 bits maps exactly into the precision of an IEEE-754 double.
            var bucket = (hash >> 11) * (1d / 9007199254740992d);
            return bucket < sampleRate;
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

        private static class StableHash
        {
            private const ulong Offset = 14695981039346656037UL;
            private const ulong Prime = 1099511628211UL;

            internal static ulong Start() => Offset;

            internal static ulong Append(ulong hash, string value)
            {
                value ??= string.Empty;
                hash = Append(hash, value.Length);
                for (var i = 0; i < value.Length; i++)
                {
                    var character = value[i];
                    hash = AppendByte(hash, (byte)character);
                    hash = AppendByte(hash, (byte)(character >> 8));
                }
                return hash;
            }

            internal static ulong Append(ulong hash, int value) =>
                Append(hash, unchecked((uint)value));

            internal static ulong Append(ulong hash, uint value)
            {
                hash = AppendByte(hash, (byte)value);
                hash = AppendByte(hash, (byte)(value >> 8));
                hash = AppendByte(hash, (byte)(value >> 16));
                return AppendByte(hash, (byte)(value >> 24));
            }

            private static ulong AppendByte(ulong hash, byte value) =>
                unchecked((hash ^ value) * Prime);
        }
    }
}
