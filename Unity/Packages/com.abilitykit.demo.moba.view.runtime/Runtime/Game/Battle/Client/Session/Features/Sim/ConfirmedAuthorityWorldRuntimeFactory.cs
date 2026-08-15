using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Management;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Game.Flow
{
    internal sealed class ConfirmedAuthorityWorldRuntime
    {
        public readonly WorldId WorldId;
        public readonly IWorldManager Worlds;
        public readonly HostRuntime Runtime;
        public readonly IWorld World;

        public ConfirmedAuthorityWorldRuntime(
            WorldId worldId,
            IWorldManager worlds,
            HostRuntime runtime,
            IWorld world)
        {
            WorldId = worldId;
            Worlds = worlds;
            Runtime = runtime;
            World = world;
        }

        public void DestroyWorld()
        {
            Runtime?.DestroyWorld(WorldId);
        }
    }

    internal static class ConfirmedAuthorityWorldRuntimeFactory
    {
        public static ConfirmedAuthorityWorldRuntime Create(
            BattleStartPlan plan,
            float fixedDelta,
            Func<WorldId, IConsumableRemoteFrameSource<PlayerInputCommand[]>> resolveRemoteInputs,
            Func<WorldId, int> resolveIdealFrameLimit)
        {
            var worlds = SessionMobaWorldBootstrapFactory.CreateWorldManager();
            var options = new HostRuntimeOptions();
            var runtime = new HostRuntime(worlds, options);

            ConfirmedAuthorityRuntimeModuleFactory.Create(
                    fixedDelta,
                    resolveRemoteInputs,
                    resolveIdealFrameLimit)
                .InstallAll(runtime, options);

            var worldId = CreateWorldId(plan);
            var worldOptions = SessionMobaWorldBootstrapFactory.CreateWorldOptions(plan, worldId);
            IWorld world = null;
            try
            {
                world = runtime.CreateWorld(worldOptions);
                return new ConfirmedAuthorityWorldRuntime(worldId, worlds, runtime, world);
            }
            catch
            {
                if (world != null)
                {
                    runtime.DestroyWorld(worldId);
                }

                throw;
            }
        }

        private static WorldId CreateWorldId(BattleStartPlan plan)
        {
            return ConfirmedAuthorityWorldId.Create(plan);
        }
    }
}
