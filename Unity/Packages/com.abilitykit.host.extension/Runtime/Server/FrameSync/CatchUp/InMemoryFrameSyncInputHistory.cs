using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Host.Extensions.FrameSync.CatchUp
{
    public sealed class InMemoryFrameSyncInputHistory : IFrameSyncInputHistory
    {
        private sealed class WorldBuffer
        {
            public readonly SortedDictionary<int, PlayerInputCommand[]> InputsByFrame = new SortedDictionary<int, PlayerInputCommand[]>();
        }

        private readonly Dictionary<WorldId, WorldBuffer> _buffers = new Dictionary<WorldId, WorldBuffer>();

        public void Append(WorldId worldId, FrameIndex frame, PlayerInputCommand[] inputs)
        {
            if (!_buffers.TryGetValue(worldId, out var buf))
            {
                buf = new WorldBuffer();
                _buffers[worldId] = buf;
            }

            buf.InputsByFrame[frame.Value] = inputs;
        }

        public void TrimBefore(WorldId worldId, FrameIndex frameExclusive)
        {
            if (!_buffers.TryGetValue(worldId, out var buf)) return;

            var keys = buf.InputsByFrame.Keys;
            var toRemove = new List<int>();
            foreach (var k in keys)
            {
                if (k <= frameExclusive.Value) toRemove.Add(k);
                else break;
            }

            for (int i = 0; i < toRemove.Count; i++) buf.InputsByFrame.Remove(toRemove[i]);
        }

        public bool TryBuildCatchUp(in FrameSyncCatchUpRequest request, out FrameSyncCatchUpPayload payload)
        {
            if (!_buffers.TryGetValue(request.WorldId, out var buf))
            {
                payload = default;
                return false;
            }

            var from = request.FromFrameExclusive.Value;
            var to = request.ToFrameInclusive.Value;
            if (to <= from)
            {
                payload = default;
                return false;
            }

            var list = new List<PlayerInputCommand[]>(to - from);
            var startFrame = from + 1;
            for (int f = startFrame; f <= to; f++)
            {
                if (!buf.InputsByFrame.TryGetValue(f, out var inputs))
                {
                    payload = default;
                    return false;
                }
                list.Add(inputs);
            }

            payload = new FrameSyncCatchUpPayload(request.WorldId, new FrameIndex(startFrame), list.ToArray());
            return true;
        }
    }
}
