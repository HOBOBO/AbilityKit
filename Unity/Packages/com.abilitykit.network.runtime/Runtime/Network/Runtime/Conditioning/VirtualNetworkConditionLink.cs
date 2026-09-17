#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Network.Protocol;

namespace AbilityKit.Network.Runtime.Conditioning
{
    public enum VirtualNetworkLinkEventKind
    {
        Disconnected,
        Reconnected,
        BlockedInbound,
        BlockedOutbound
    }

    public readonly struct VirtualNetworkLinkEvent
    {
        public VirtualNetworkLinkEvent(long atMs, VirtualNetworkLinkEventKind kind, uint opCode = 0, uint sequence = 0)
        {
            AtMs = atMs;
            Kind = kind;
            OpCode = opCode;
            Sequence = sequence;
        }

        public long AtMs { get; }
        public VirtualNetworkLinkEventKind Kind { get; }
        public uint OpCode { get; }
        public uint Sequence { get; }
    }

    /// <summary>Protocol-level link for deterministic, socket-free scenario playback.</summary>
    public sealed class VirtualNetworkConditionLink : IDisposable
    {
        private readonly List<VirtualNetworkLinkEvent> _events = new List<VirtualNetworkLinkEvent>();
        private long _nowMs;
        private bool _disposed;

        public VirtualNetworkConditionLink(NetworkConditionScenario scenario, int seed = 0,
            int decisionCapacity = 4096, int maxPendingPackets = 4096)
        {
            Middleware = new NetworkConditioningMiddleware(scenario, () => _nowMs, seed,
                decisionCapacity, maxPendingPackets, scenarioStartMs: 0, platformStableRandom: true);
        }

        public NetworkConditioningMiddleware Middleware { get; }
        public long NowMs => _nowMs;
        public bool IsConnected { get; private set; } = true;
        public IReadOnlyList<VirtualNetworkLinkEvent> Events => _events.AsReadOnly();

        /// <summary>Flush due packets before executing DSL actions at this timestamp.</summary>
        public void AdvanceTo(long nowMs)
        {
            ThrowIfDisposed();
            if (nowMs < _nowMs) throw new ArgumentOutOfRangeException(nameof(nowMs), "Virtual time cannot go backwards.");
            long? next;
            while ((next = Middleware.NextDeliveryAtMs).HasValue && next.Value <= nowMs)
            {
                _nowMs = Math.Max(_nowMs, next.Value);
                Middleware.Advance(_nowMs);
            }
            _nowMs = nowMs;
        }

        public void Disconnect()
        {
            ThrowIfDisposed();
            if (!IsConnected) return;
            IsConnected = false;
            Middleware.ClearPending();
            _events.Add(new VirtualNetworkLinkEvent(_nowMs, VirtualNetworkLinkEventKind.Disconnected));
        }

        public void Reconnect()
        {
            ThrowIfDisposed();
            if (IsConnected) return;
            IsConnected = true;
            _events.Add(new VirtualNetworkLinkEvent(_nowMs, VirtualNetworkLinkEventKind.Reconnected));
        }

        public void InjectInbound(NetworkPacketHeader header, ArraySegment<byte> payload,
            Action<NetworkPacketHeader, ArraySegment<byte>> deliver)
        {
            ThrowIfDisposed();
            if (!IsConnected)
            {
                _events.Add(new VirtualNetworkLinkEvent(_nowMs, VirtualNetworkLinkEventKind.BlockedInbound,
                    header.OpCode, header.Seq));
                return;
            }

            Middleware.OnInbound(null!, header, payload, deliver);
        }

        public void InjectOutbound(NetworkPacketHeader header, ArraySegment<byte> payload,
            Action<NetworkPacketHeader, ArraySegment<byte>> deliver)
        {
            ThrowIfDisposed();
            if (!IsConnected)
            {
                _events.Add(new VirtualNetworkLinkEvent(_nowMs, VirtualNetworkLinkEventKind.BlockedOutbound,
                    header.OpCode, header.Seq));
                return;
            }

            Middleware.OnOutbound(null!, header, payload, deliver);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Middleware.ClearPending();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(VirtualNetworkConditionLink));
        }
    }
}
