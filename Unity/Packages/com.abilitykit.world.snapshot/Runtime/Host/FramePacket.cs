using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Host
{
    public sealed class FramePacket : ISnapshotEnvelope
    {
        public FramePacket(WorldId worldId, FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs, WorldStateSnapshot? snapshot)
        {
            WorldId = worldId;
            Frame = frame;
            Inputs = inputs ?? Array.Empty<PlayerInputCommand>();
            Snapshot = snapshot;
        }

        public WorldId WorldId { get; }
        public FrameIndex Frame { get; }

        public IReadOnlyList<PlayerInputCommand> Inputs { get; }

        public WorldStateSnapshot? Snapshot { get; }
    }

    public static class SnapshotProviderDrain
    {
        public static int DrainSnapshots(
            IWorldStateSnapshotProvider provider,
            WorldId worldId,
            FrameIndex frame,
            int maxSnapshotsPerStep,
            Action<FramePacket> feed)
        {
            if (provider == null) return 0;
            if (feed == null) return 0;
            if (maxSnapshotsPerStep <= 0) return 0;

            var drained = 0;
            for (int i = 0; i < maxSnapshotsPerStep; i++)
            {
                if (!provider.TryGetSnapshot(frame, out var s)) break;
                feed(new FramePacket(worldId, frame, Array.Empty<PlayerInputCommand>(), s));
                drained++;
            }

            return drained;
        }
    }
}
