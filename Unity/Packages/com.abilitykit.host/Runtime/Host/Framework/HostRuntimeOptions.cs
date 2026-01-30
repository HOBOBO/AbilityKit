using System;
using AbilityKit.Ability.Host.Hooks;
using AbilityKit.Ability.Host.Transport;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Host.Framework
{
    public sealed class HostRuntimeOptions
    {
        public readonly Hook<WorldCreateOptions> BeforeCreateWorld = new Hook<WorldCreateOptions>();
        public readonly Hook<IWorld> WorldCreated = new Hook<IWorld>();
        public readonly Hook<WorldId> WorldDestroyed = new Hook<WorldId>();

        public readonly Hook<float> PreTick = new Hook<float>();
        public readonly Hook<float> PostTick = new Hook<float>();

        public readonly Hook<ServerClientId, ServerMessage> BeforeSendMessage = new Hook<ServerClientId, ServerMessage>();
        public readonly Hook<ServerClientId, ServerMessage> AfterSendMessage = new Hook<ServerClientId, ServerMessage>();

        public Action<WorldCreateOptions> OnBeforeCreateWorld;
        public Action<IWorld> OnWorldCreated;
        public Action<WorldId> OnWorldDestroyed;

        public Action<float> OnPreTick;
        public Action<float> OnPostTick;

        public Action<ServerClientId, ServerMessage> OnBeforeSendMessage;
        public Action<ServerClientId, ServerMessage> OnAfterSendMessage;
    }
}
