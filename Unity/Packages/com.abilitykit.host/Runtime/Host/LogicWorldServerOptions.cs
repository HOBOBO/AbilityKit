using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host.Drivers;
using AbilityKit.Ability.Host.Hooks;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Management;

namespace AbilityKit.Ability.Host
{
    public sealed class LogicWorldServerOptions
    {
        public readonly Hook<WorldCreateOptions> BeforeCreateWorld = new Hook<WorldCreateOptions>();
        public readonly Hook<IWorld> WorldCreated = new Hook<IWorld>();
        public readonly Hook<WorldId> WorldDestroyed = new Hook<WorldId>();
        public readonly Hook<WorldId, PlayerId> PlayerJoined = new Hook<WorldId, PlayerId>();
        public readonly Hook<WorldId, PlayerId> PlayerLeft = new Hook<WorldId, PlayerId>();
        public readonly Hook<WorldId, FrameIndex, PlayerInputCommand[]> InputsFlushed = new Hook<WorldId, FrameIndex, PlayerInputCommand[]>();
        public readonly Hook<FrameIndex, float> PostStep = new Hook<FrameIndex, float>();
        public readonly Hook<FramePacket> BeforeBroadcastFrame = new Hook<FramePacket>();
        public readonly Hook<FramePacket> AfterBroadcastFrame = new Hook<FramePacket>();

        public bool EnablePlayerLifecycleHooks = true;
        public bool BroadcastPlayerLifecycleMessages = true;

        public Func<FrameSyncWorldServerDriver.IFrameScheduler> CreateFrameScheduler;
        public Func<IWorldManager, System.Collections.Generic.Dictionary<WorldId, FrameSyncWorldServerDriver.WorldSession>, LogicWorldServerOptions, FrameSyncWorldServerDriver.IInputModule> CreateInputModule;
        public Func<IWorldManager, FrameSyncWorldServerDriver.ISnapshotModule> CreateSnapshotModule;

        public Action<WorldCreateOptions> OnBeforeCreateWorld;

        public Action<IWorld> OnWorldCreated;

        public Action<WorldId> OnWorldDestroyed;

        public Action<WorldId, PlayerId> OnPlayerJoined;

        public Action<WorldId, PlayerId> OnPlayerLeft;

        public Action<WorldId, FrameIndex, PlayerInputCommand[]> OnInputsFlushed;

        public Action<FrameIndex, float> OnPostStep;

        public Action<FramePacket> OnBeforeBroadcastFrame;

        public Action<FramePacket> OnAfterBroadcastFrame;
    }
}
