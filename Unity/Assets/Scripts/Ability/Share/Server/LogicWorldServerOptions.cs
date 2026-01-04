using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Server.Hooks;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Server
{
    public sealed class LogicWorldServerOptions
    {
        public readonly Hook<WorldCreateOptions> BeforeCreateWorld = new Hook<WorldCreateOptions>();
        public readonly Hook<IWorld> WorldCreated = new Hook<IWorld>();
        public readonly Hook<WorldId> WorldDestroyed = new Hook<WorldId>();
        public readonly Hook<WorldId, FrameIndex, PlayerInputCommand[]> InputsFlushed = new Hook<WorldId, FrameIndex, PlayerInputCommand[]>();
        public readonly Hook<FrameIndex, float> PostStep = new Hook<FrameIndex, float>();
        public readonly Hook<FramePacket> BeforeBroadcastFrame = new Hook<FramePacket>();
        public readonly Hook<FramePacket> AfterBroadcastFrame = new Hook<FramePacket>();

        public Action<WorldCreateOptions> OnBeforeCreateWorld;

        public Action<IWorld> OnWorldCreated;

        public Action<WorldId> OnWorldDestroyed;

        public Action<WorldId, FrameIndex, PlayerInputCommand[]> OnInputsFlushed;

        public Action<FrameIndex, float> OnPostStep;

        public Action<FramePacket> OnBeforeBroadcastFrame;

        public Action<FramePacket> OnAfterBroadcastFrame;
    }
}
