using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Core.Collections;

namespace AbilityKit.Ability.Host
{
    public sealed class RemoteFrameAggregator
    {
        private readonly Dictionary<int, List<PlayerInputCommand>> _inputsByFrame = new Dictionary<int, List<PlayerInputCommand>>(256);
        private readonly Dictionary<int, List<ISnapshotEnvelope>> _envelopesByFrame = new Dictionary<int, List<ISnapshotEnvelope>>(256);
        private readonly SortedIntSet _inputFrames = new SortedIntSet(256);
        private readonly SortedIntSet _snapshotFrames = new SortedIntSet(256);

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
                    _inputFrames.Add(frame);
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
                    _snapshotFrames.Add(frame);
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
            TrimDictionaryBefore(_inputsByFrame, _inputFrames, minFrameInclusive);
            TrimDictionaryBefore(_envelopesByFrame, _snapshotFrames, minFrameInclusive);
        }

        public void Clear()
        {
            _inputsByFrame.Clear();
            _envelopesByFrame.Clear();
            _inputFrames.Clear();
            _snapshotFrames.Clear();
        }

        private static void TrimDictionaryBefore<T>(
            Dictionary<int, List<T>> values,
            SortedIntSet frames,
            int minFrameInclusive)
        {
            var removeCount = frames.LowerBound(minFrameInclusive);
            for (var index = 0; index < removeCount; index++)
            {
                values.Remove(frames[index]);
            }

            if (removeCount > 0) frames.RemoveRange(0, removeCount);
        }
    }
}
