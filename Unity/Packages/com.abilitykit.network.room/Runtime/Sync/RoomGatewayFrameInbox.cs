#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Network.Room
{
    /// <summary>Buffers Room frames and requires a covering snapshot when continuity is lost.</summary>
    public sealed class RoomGatewayFrameInbox
    {
        private readonly Queue<WireRoomFramePush> _frames = new Queue<WireRoomFramePush>();
        private readonly object _gate = new object();
        private readonly int _capacity;
        private bool _paused;
        private bool _overflowed;
        private int _highestFrame = -1;
        private int _minimumRecoveryFrame;
        private int _overflowCount;

        public RoomGatewayFrameInbox(int capacity = 128)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
        }

        public int OverflowCount
        {
            get { lock (_gate) return _overflowCount; }
        }

        public bool Enqueue(in WireRoomFramePush frame)
        {
            lock (_gate)
            {
                if (_paused) return false;
                if (_frames.Count == _capacity)
                {
                    _minimumRecoveryFrame = checked(Math.Max(_highestFrame, frame.Frame) + 1);
                    _frames.Clear();
                    _paused = true;
                    _overflowed = true;
                    _overflowCount++;
                    return false;
                }
                _frames.Enqueue(frame);
                _highestFrame = Math.Max(_highestFrame, frame.Frame);
                return true;
            }
        }

        public bool TryDequeue(out WireRoomFramePush frame)
        {
            lock (_gate)
            {
                if (!_paused && _frames.Count > 0)
                {
                    frame = _frames.Dequeue();
                    return true;
                }
            }
            frame = default;
            return false;
        }

        public bool TryGetOverflow(out int minimumRecoveryFrame)
        {
            lock (_gate)
            {
                minimumRecoveryFrame = _minimumRecoveryFrame;
                return _overflowed;
            }
        }

        public void PauseForRecovery()
        {
            lock (_gate)
            {
                _frames.Clear();
                _paused = true;
            }
        }

        public void ResumeAfterSnapshot()
        {
            lock (_gate)
            {
                _frames.Clear();
                _paused = false;
                _overflowed = false;
                _highestFrame = -1;
                _minimumRecoveryFrame = 0;
            }
        }
    }
}
