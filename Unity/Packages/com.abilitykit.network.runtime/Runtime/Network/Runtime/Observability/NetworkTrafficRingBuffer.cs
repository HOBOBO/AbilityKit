#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Network.Runtime.Observability
{
    /// <summary>Thread-safe bounded collector. The oldest event is evicted when full.</summary>
    public sealed class NetworkTrafficRingBuffer : INetworkTrafficObserver
    {
        private readonly object _gate = new object();
        private readonly NetworkTrafficEvent?[] _events;
        private int _start;
        private int _count;
        private long _droppedCount;

        public NetworkTrafficRingBuffer(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _events = new NetworkTrafficEvent[capacity];
        }

        public int Capacity => _events.Length;

        public int Count
        {
            get { lock (_gate) return _count; }
        }

        public long DroppedCount
        {
            get { lock (_gate) return _droppedCount; }
        }

        public void OnTraffic(NetworkTrafficEvent trafficEvent)
        {
            if (trafficEvent == null) throw new ArgumentNullException(nameof(trafficEvent));

            lock (_gate)
            {
                if (_count == _events.Length)
                {
                    _events[_start] = trafficEvent;
                    _start = (_start + 1) % _events.Length;
                    _droppedCount++;
                    return;
                }

                _events[(_start + _count) % _events.Length] = trafficEvent;
                _count++;
            }
        }

        public IReadOnlyList<NetworkTrafficEvent> Snapshot()
        {
            lock (_gate)
            {
                var snapshot = new NetworkTrafficEvent[_count];
                for (var i = 0; i < _count; i++)
                    snapshot[i] = _events[(_start + i) % _events.Length]!;
                return snapshot;
            }
        }

        public void Clear()
        {
            lock (_gate)
            {
                Array.Clear(_events, 0, _events.Length);
                _start = 0;
                _count = 0;
                _droppedCount = 0;
            }
        }
    }
}
