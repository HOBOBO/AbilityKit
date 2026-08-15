#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Core.Timing;

namespace AbilityKit.Network.Runtime.Conditioning
{
    /// <summary>
    /// 可复现的网络环境模拟器，可接入中间件链。
    /// 它会将 <see cref="NetworkConditionProfile"/>（延迟、抖动、丢包、乱序、带宽）应用到入站和出站包，
    /// 让任意同步模型都能在受控且可重复的不利网络条件下运行。
    ///
    /// 投递由时间驱动且具备确定性：未被丢弃的包会按计划投递时间进入缓冲，
    /// 只有在使用达到或超过该时间的时钟值调用 <see cref="Advance"/> 时，才会释放到下一阶段。
    /// 固定的 <c>seed</c> 与可注入时钟让“同步模型 x 网络配置”的对比能在测试中完全重放，
    /// 且不需要真实等待。
    /// </summary>
    public sealed class NetworkConditioningMiddleware : INetworkMiddleware
    {
        private sealed class PendingPacket
        {
            public long DeliverAtMs;
            public long Sequence;
            public bool Inbound;
            public NetworkPacketHeader Header;
            public byte[] Payload = Array.Empty<byte>();
            public int PayloadLength;
            public Action<NetworkPacketHeader, ArraySegment<byte>>? Next;
        }

        private const int MaxPooledPackets = 256;

        private readonly NetworkConditionProfile _profile;
        private readonly Func<long> _clockMs;
        private readonly Random _random;
        private readonly List<PendingPacket> _pending = new List<PendingPacket>();
        private readonly List<PendingPacket> _ready = new List<PendingPacket>();
        private readonly Stack<PendingPacket> _packetPool = new Stack<PendingPacket>();

        private long _enqueueCounter;
        private long _inboundBandwidthAvailableAtMs;
        private long _outboundBandwidthAvailableAtMs;

        private long _inboundReceived;
        private long _inboundDelivered;
        private long _inboundDropped;
        private long _inboundReordered;
        private long _outboundReceived;
        private long _outboundDelivered;
        private long _outboundDropped;
        private long _outboundReordered;

        /// <summary>
        /// 创建网络调理中间件。
        /// </summary>
        /// <param name="profile">要应用的网络条件。</param>
        /// <param name="clockMs">
        /// 返回当前毫秒时间的单调时钟。可注入该时钟以便测试驱动虚拟时钟；为 null 时使用真实墙钟。
        /// </param>
        /// <param name="seed">用于抖动、丢包和乱序的确定性随机源种子。</param>
        public NetworkConditioningMiddleware(NetworkConditionProfile profile, Func<long>? clockMs = null, int seed = 0)
        {
            _profile = profile;
            _clockMs = clockMs ?? DefaultClock;
            _random = new Random(seed);
        }

        public void OnInbound(ISessionContext context, NetworkPacketHeader header, ArraySegment<byte> payload, Action<NetworkPacketHeader, ArraySegment<byte>> next)
        {
            _inboundReceived++;
            Schedule(inbound: true, header, payload, next);
        }

        public void OnOutbound(ISessionContext context, NetworkPacketHeader header, ArraySegment<byte> payload, Action<NetworkPacketHeader, ArraySegment<byte>> next)
        {
            _outboundReceived++;
            Schedule(inbound: false, header, payload, next);
        }

        /// <summary>
        /// 按投递时间顺序释放所有计划投递时间早于或等于 <paramref name="nowMs"/> 的缓冲包。
        /// 宿主循环（或测试）应使用当前时钟值调用该方法，以冲刷到期包。
        /// </summary>
        public void Advance(long nowMs)
        {
            if (_pending.Count == 0)
            {
                return;
            }

            // 稳定顺序：先按投递时间，再按原始入队序号；除非调度时显式乱序，否则相同时间保持到达顺序。
            _pending.Sort(static (a, b) =>
            {
                int byTime = a.DeliverAtMs.CompareTo(b.DeliverAtMs);
                return byTime != 0 ? byTime : a.Sequence.CompareTo(b.Sequence);
            });

            int dueCount = 0;
            while (dueCount < _pending.Count && _pending[dueCount].DeliverAtMs <= nowMs)
            {
                dueCount++;
            }

            if (dueCount == 0)
            {
                return;
            }

            _ready.Clear();
            if (_ready.Capacity < dueCount)
            {
                _ready.Capacity = dueCount;
            }

            for (int i = 0; i < dueCount; i++)
            {
                _ready.Add(_pending[i]);
            }

            _pending.RemoveRange(0, dueCount);

            int deliveryIndex = 0;
            try
            {
                for (; deliveryIndex < _ready.Count; deliveryIndex++)
                {
                    var packet = _ready[deliveryIndex];

                    if (packet.Inbound) _inboundDelivered++;
                    else _outboundDelivered++;

                    try
                    {
                        packet.Next!(packet.Header, new ArraySegment<byte>(packet.Payload, 0, packet.PayloadLength));
                    }
                    finally
                    {
                        ReleasePacket(packet);
                    }
                }
            }
            catch
            {
                // Preserve packets that had not yet reached their callback, matching the previous
                // one-at-a-time removal behavior when a downstream callback throws.
                for (int i = deliveryIndex + 1; i < _ready.Count; i++)
                {
                    _pending.Add(_ready[i]);
                }

                throw;
            }
            finally
            {
                _ready.Clear();
            }
        }

        /// <summary>
        /// Discards queued packets and returns their storage to the shared pools.
        /// The middleware is not thread-safe; call this outside <see cref="Advance"/>.
        /// </summary>
        public void ClearPending()
        {
            for (int i = 0; i < _pending.Count; i++)
            {
                ReleasePacket(_pending[i]);
            }

            _pending.Clear();
            _inboundBandwidthAvailableAtMs = 0;
            _outboundBandwidthAvailableAtMs = 0;
        }

        public NetworkConditioningStats GetStats()
        {
            return new NetworkConditioningStats(
                _inboundReceived,
                _inboundDelivered,
                _inboundDropped,
                _inboundReordered,
                _outboundReceived,
                _outboundDelivered,
                _outboundDropped,
                _outboundReordered,
                _pending.Count);
        }

        private void Schedule(bool inbound, NetworkPacketHeader header, ArraySegment<byte> payload, Action<NetworkPacketHeader, ArraySegment<byte>> next)
        {
            if (_profile.PacketLossRate > 0d && _random.NextDouble() < _profile.PacketLossRate)
            {
                if (inbound) _inboundDropped++;
                else _outboundDropped++;
                return;
            }

            long now = _clockMs();
            long delay = _profile.BaseLatencyMs;
            if (_profile.JitterMs > 0)
            {
                // 对称抖动范围为 [-JitterMs, +JitterMs]。
                delay += _random.Next(-_profile.JitterMs, _profile.JitterMs + 1);
            }

            bool reordered = false;
            if (_profile.ReorderRate > 0d && _random.NextDouble() < _profile.ReorderRate)
            {
                // 将包提前，使其可以越过原本排在它前面的相邻包。
                long pullForward = _profile.BaseLatencyMs + _profile.JitterMs + 1;
                delay -= pullForward;
                reordered = true;
                if (inbound) _inboundReordered++;
                else _outboundReordered++;
            }

            if (delay < 0) delay = 0;

            long bandwidthDelay = ReserveBandwidth(inbound, now, payload.Count);

            // 复制载荷，因为调用方缓冲区可能在该调用返回后被复用。
            var copy = payload.Count == 0
                ? Array.Empty<byte>()
                : ArrayPool<byte>.Shared.Rent(payload.Count);
            if (payload.Count > 0)
            {
                Buffer.BlockCopy(payload.Array!, payload.Offset, copy, 0, payload.Count);
            }

            var packet = RentPacket();
            packet.DeliverAtMs = now + delay + bandwidthDelay;
            packet.Sequence = reordered ? long.MinValue + _enqueueCounter++ : _enqueueCounter++;
            packet.Inbound = inbound;
            packet.Header = header;
            packet.Payload = copy;
            packet.PayloadLength = payload.Count;
            packet.Next = next;
            _pending.Add(packet);
        }

        private PendingPacket RentPacket()
        {
            return _packetPool.Count > 0 ? _packetPool.Pop() : new PendingPacket();
        }

        private void ReleasePacket(PendingPacket packet)
        {
            if (packet.Payload.Length > 0)
            {
                ArrayPool<byte>.Shared.Return(packet.Payload);
            }

            packet.DeliverAtMs = 0;
            packet.Sequence = 0;
            packet.Inbound = false;
            packet.Header = default;
            packet.Payload = Array.Empty<byte>();
            packet.PayloadLength = 0;
            packet.Next = null;

            if (_packetPool.Count < MaxPooledPackets)
            {
                _packetPool.Push(packet);
            }
        }

        private long ReserveBandwidth(bool inbound, long nowMs, int payloadBytes)
        {
            if (_profile.BandwidthKbps <= 0 || payloadBytes <= 0)
            {
                return 0;
            }

            // 1 Kbps = 1 bit/ms。每个方向独立串行化，完成发送后才能投递。
            long serializationMs = ((long)payloadBytes * 8L + _profile.BandwidthKbps - 1L) /
                                   _profile.BandwidthKbps;
            long availableAtMs = inbound ? _inboundBandwidthAvailableAtMs : _outboundBandwidthAvailableAtMs;
            long transmissionStartsAtMs = Math.Max(nowMs, availableAtMs);
            long transmissionCompletesAtMs = transmissionStartsAtMs + serializationMs;

            if (inbound)
            {
                _inboundBandwidthAvailableAtMs = transmissionCompletesAtMs;
            }
            else
            {
                _outboundBandwidthAvailableAtMs = transmissionCompletesAtMs;
            }

            return transmissionCompletesAtMs - nowMs;
        }

        private static long DefaultClock()
        {
            return MonotonicTime.GetMilliseconds();
        }
    }
}
