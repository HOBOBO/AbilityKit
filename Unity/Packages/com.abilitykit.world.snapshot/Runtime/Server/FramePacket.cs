using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Server
{
    public sealed class FramePacket
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
}
