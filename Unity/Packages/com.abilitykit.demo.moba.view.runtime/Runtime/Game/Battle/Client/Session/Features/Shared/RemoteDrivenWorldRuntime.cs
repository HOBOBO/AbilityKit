using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Management;
using AbilityKit.Demo.Moba.Services;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// 持有远端会话的本地逻辑世界；模块和时钟由各同步模式选择。
    /// </summary>
    internal sealed class RemoteDrivenWorldRuntime
    {
        public readonly WorldId WorldId;
        public readonly IWorldManager Worlds;
        public readonly HostRuntime Runtime;
        public readonly IWorld World;

        public RemoteDrivenWorldRuntime(WorldId worldId, IWorldManager worlds, HostRuntime runtime, IWorld world)
        {
            WorldId = worldId;
            Worlds = worlds;
            Runtime = runtime;
            World = world;
        }

        internal static RemoteDrivenWorldRuntime Create(
            BattleStartPlan plan,
            HostRuntimeModuleHost modules,
            Func<HostRuntime, IWorldAuthorityFramesSource> resolveAuthorityFrames = null)
        {
            var worlds = SessionMobaWorldBootstrapFactory.CreateWorldManager();
            var runtimeOptions = new HostRuntimeOptions();
            var runtime = new HostRuntime(worlds, runtimeOptions);
            modules.InstallAll(runtime, runtimeOptions);
            var worldId = new WorldId(plan.World.WorldId);
            var options = SessionMobaWorldBootstrapFactory.CreateWorldOptions(
                plan, worldId, resolveAuthorityFrames?.Invoke(runtime));
            return new RemoteDrivenWorldRuntime(worldId, worlds, runtime, runtime.CreateWorld(options));
        }

        /// <summary>
        /// 销毁由该宿主持有的逻辑世界。
        /// </summary>
        public void DestroyWorld() => Runtime?.DestroyWorld(WorldId);
    }
}
