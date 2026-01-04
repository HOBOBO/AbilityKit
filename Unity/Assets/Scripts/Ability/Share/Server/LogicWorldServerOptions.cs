using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Server
{
    public sealed class LogicWorldServerOptions
    {
        public Action<IWorld> OnWorldCreated;

        public Action<WorldId> OnWorldDestroyed;

        public Action<WorldId, FrameIndex, PlayerInputCommand[]> OnInputsFlushed;

        public Action<FrameIndex, float> OnPostStep;

        public Action<FramePacket> OnBeforeBroadcastFrame;

        public Action<FramePacket> OnAfterBroadcastFrame;
    }
}
