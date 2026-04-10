using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Core.Common.SnapshotRouting;

namespace AbilityKit.Ability.Host
{
    public sealed class RemoteFrameAggregator
    {
        private readonly Dictionary<int, List<PlayerInputCommand>> _inputsByFrame = new Dictionary<int, List<PlayerInputCommand>>(256);
        private readonly Dictionary<int, List<ISnapshotEnvelope>> _envelopesByFrame = new Dictionary<int, List<ISnapshotEnvelope>>(256);

        public void AddPacket(FramePacket packet)
        {
            if (packet == null) return;

            var frame = packet.Frame.Value;
            if (frame < 0) return;

            if (packet.Inputs != null && packet.Inputs.Count > 0)
            {
                if (!_inputsByFrame.TryGetValue(frame, out var list) || list == null)
                {
                    list = new List<PlayerInputCommand>(packet.Inputs.Count);
                    _inputsByFrame[frame] = list;
                }

                for (int i = 0; i < packet.Inputs.Count; i++)
                {
                    list.Add(packet.Inputs[i]);
                }
            }

            if (packet.Snapshot.HasValue)
            {
                if (!_envelopesByFrame.TryGetValue(frame, out var list) || list == null)
                {
                    list = new List<ISnapshotEnvelope>(4);
                    _envelopesByFrame[frame] = list;
                }

                list.Add(packet);
            }
        }

        public RemoteInputFrame BuildInputFrame(FrameIndex frame)
        {
            var f = frame.Value;
            if (_inputsByFrame.TryGetValue(f, out var list) && list != null && list.Count > 0)
            {
                return new RemoteInputFrame(frame, list.ToArray());
            }

            return new RemoteInputFrame(frame, Array.Empty<PlayerInputCommand>());
        }

        public RemoteSnapshotFrame BuildSnapshotFrame(FrameIndex frame)
        {
            var f = frame.Value;
            if (_envelopesByFrame.TryGetValue(f, out var list) && list != null && list.Count > 0)
            {
                return new RemoteSnapshotFrame(frame, list.ToArray());
            }

            return new RemoteSnapshotFrame(frame, Array.Empty<ISnapshotEnvelope>());
        }

        public void TrimBefore(int minFrameInclusive)
        {
            if (_inputsByFrame.Count > 0)
            {
                var keys = new List<int>(_inputsByFrame.Count);
                foreach (var kv in _inputsByFrame)
                {
                    if (kv.Key < minFrameInclusive) keys.Add(kv.Key);
                }

                for (int i = 0; i < keys.Count; i++)
                {
                    _inputsByFrame.Remove(keys[i]);
                }
            }

            if (_envelopesByFrame.Count > 0)
            {
                var keys = new List<int>(_envelopesByFrame.Count);
                foreach (var kv in _envelopesByFrame)
                {
                    if (kv.Key < minFrameInclusive) keys.Add(kv.Key);
                }

                for (int i = 0; i < keys.Count; i++)
                {
                    _envelopesByFrame.Remove(keys[i]);
                }
            }
        }

        public void Clear()
        {
            _inputsByFrame.Clear();
            _envelopesByFrame.Clear();
        }
    }
}
