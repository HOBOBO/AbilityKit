using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Management;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Game.Flow.Battle.FrameSync;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Game.Flow
{
    internal sealed class RemoteDrivenWorldRuntime
    {
        public readonly WorldId WorldId;
        public readonly IWorldManager Worlds;
        public readonly HostRuntime Runtime;
        public readonly IWorld World;

        public RemoteDrivenWorldRuntime(
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

    internal sealed class RemoteDrivenWorldRuntimeFactoryOptions
    {
        public readonly BattleStartPlan Plan;
        public readonly float FixedDelta;
        public readonly int InputDelayFrames;
        public readonly bool EnableClientPrediction;
        public readonly Func<WorldId, IConsumableRemoteFrameSource<PlayerInputCommand[]>> ResolveRemoteInputs;
        public readonly Func<WorldId, ILocalInputSource<LocalPlayerInputEvent[]>> ResolveLocalInputs;
        public readonly Func<WorldId, int> ResolveIdealFrameLimit;
        public readonly Func<IWorld, RollbackRegistry> BuildRollbackRegistry;
        public readonly Func<IWorld, Func<FrameIndex, WorldStateHash>> BuildComputeHash;
        public readonly ClientPredictionDriverBufferOptions PredictionBufferOptions;

        public RemoteDrivenWorldRuntimeFactoryOptions(
            BattleStartPlan plan,
            float fixedDelta,
            int inputDelayFrames,
            bool enableClientPrediction,
            Func<WorldId, IConsumableRemoteFrameSource<PlayerInputCommand[]>> resolveRemoteInputs,
            Func<WorldId, ILocalInputSource<LocalPlayerInputEvent[]>> resolveLocalInputs,
            Func<WorldId, int> resolveIdealFrameLimit,
            Func<IWorld, RollbackRegistry> buildRollbackRegistry,
            Func<IWorld, Func<FrameIndex, WorldStateHash>> buildComputeHash,
            ClientPredictionDriverBufferOptions predictionBufferOptions = null)
        {
            Plan = plan;
            FixedDelta = fixedDelta;
            InputDelayFrames = inputDelayFrames < 0 ? 0 : inputDelayFrames;
            EnableClientPrediction = enableClientPrediction;
            ResolveRemoteInputs = resolveRemoteInputs;
            ResolveLocalInputs = resolveLocalInputs;
            ResolveIdealFrameLimit = resolveIdealFrameLimit;
            BuildRollbackRegistry = buildRollbackRegistry;
            BuildComputeHash = buildComputeHash;
            PredictionBufferOptions = predictionBufferOptions;
        }
    }

    internal static class RemoteDrivenWorldRuntimeFactory
    {
        public static RemoteDrivenWorldRuntime Create(RemoteDrivenWorldRuntimeFactoryOptions options)
        {
            var worlds = SessionMobaWorldBootstrapFactory.CreateWorldManager();
            var runtimeOptions = new HostRuntimeOptions();
            var runtime = new HostRuntime(worlds, runtimeOptions);

            RemoteDrivenRuntimeModuleFactory.Create(options).InstallAll(runtime, runtimeOptions);

            var worldId = new WorldId(options.Plan.World.WorldId);
            var authorityFramesSource = CreateAuthorityFramesSource(runtime);
            var worldOptions = SessionMobaWorldBootstrapFactory.CreateWorldOptions(
                options.Plan,
                worldId,
                authorityFramesSource);
            IWorld world = null;
            try
            {
                world = runtime.CreateWorld(worldOptions);
                BindAuthorityFrameService(world);
                return new RemoteDrivenWorldRuntime(worldId, worlds, runtime, world);
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

        private static IWorldAuthorityFramesSource CreateAuthorityFramesSource(HostRuntime runtime)
        {
            try
            {
                return runtime.Features.TryGetFeature<IClientPredictionDriverStats>(out var stats) && stats != null
                    ? new ClientPredictionDriverStatsFramesSource(stats)
                    : null;
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[BattleSessionFeature] RemoteDrivenLocalWorld stats lookup failed");
                return null;
            }
        }

        private static void BindAuthorityFrameService(IWorld world)
        {
            try
            {
                if (world?.Services != null &&
                    world.Services.TryResolve<MobaAuthorityFrameService>(out var authorityFrameService) &&
                    authorityFrameService != null)
                {
                    authorityFrameService.BindWorld(world.Id);
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[BattleSessionFeature] RemoteDrivenLocalWorld authority frame service bind failed");
            }
        }
    }
}
