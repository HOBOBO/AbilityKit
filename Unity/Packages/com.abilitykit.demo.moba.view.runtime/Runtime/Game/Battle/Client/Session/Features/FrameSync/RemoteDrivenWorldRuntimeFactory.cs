using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Game.Flow.Battle.FrameSync;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Game.Flow
{
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
        public readonly Func<WorldId, IConsumableRemoteFrameSource<WorldSettledInputFrame>> ResolveSettledFrames;

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
            ClientPredictionDriverBufferOptions predictionBufferOptions = null,
            Func<WorldId, IConsumableRemoteFrameSource<WorldSettledInputFrame>> resolveSettledFrames = null)
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
            ResolveSettledFrames = resolveSettledFrames;
        }
    }

    internal static class RemoteDrivenWorldRuntimeFactory
    {
        public static RemoteDrivenWorldRuntime Create(RemoteDrivenWorldRuntimeFactoryOptions options)
        {
            var runtime = RemoteDrivenWorldRuntime.Create(options.Plan,
                RemoteDrivenRuntimeModuleFactory.Create(options), CreateAuthorityFramesSource);
            BindAuthorityFrameService(runtime.World, options.EnableClientPrediction || options.ResolveSettledFrames != null
                ? RemoteDrivenRuntimeModuleFactory.PredictionRollbackHistoryFrames : 0);
            return runtime;
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

        private static void BindAuthorityFrameService(IWorld world, int rollbackHistoryFrames)
        {
            try
            {
                if (world?.Services != null &&
                    world.Services.TryResolve<MobaAuthorityFrameService>(out var authorityFrameService) &&
                    authorityFrameService != null)
                {
                    authorityFrameService.BindWorld(world.Id);
                    authorityFrameService.ConfigureRollbackHistoryFrames(rollbackHistoryFrames);
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[BattleSessionFeature] RemoteDrivenLocalWorld authority frame service bind failed");
            }
        }
    }
}
