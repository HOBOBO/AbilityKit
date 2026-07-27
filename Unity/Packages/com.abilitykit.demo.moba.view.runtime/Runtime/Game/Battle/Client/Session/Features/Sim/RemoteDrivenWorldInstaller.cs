using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Game.Flow
{
    internal readonly struct RemoteDrivenWorldInstallOptions
    {
        public readonly BattleStartPlan Plan;
        public readonly BattleContext Context;
        public readonly BattleSessionRemoteDrivenWorldRuntime Handles;
        public readonly float FixedDeltaSeconds;
        public readonly Func<WorldId, int> ResolveIdealFrameLimit;
        public readonly Func<bool> ShouldForceHashMismatch;
        public readonly Action ResetTickState;

        public RemoteDrivenWorldInstallOptions(
            BattleStartPlan plan,
            BattleContext context,
            BattleSessionRemoteDrivenWorldRuntime handles,
            float fixedDeltaSeconds,
            Func<WorldId, int> resolveIdealFrameLimit,
            Func<bool> shouldForceHashMismatch,
            Action resetTickState)
        {
            Plan = plan;
            Context = context;
            Handles = handles;
            FixedDeltaSeconds = fixedDeltaSeconds;
            ResolveIdealFrameLimit = resolveIdealFrameLimit;
            ShouldForceHashMismatch = shouldForceHashMismatch;
            ResetTickState = resetTickState;
        }
    }

    internal static class RemoteDrivenWorldInstaller
    {
        public static void EnsureStarted(RemoteDrivenWorldInstallOptions options)
        {
            var handles = options.Handles;
            if (handles.World != null) return;

            var inputDelayFrames = ResolveInputDelay(options.Plan);
            CreateWorldRuntime(
                options.Plan,
                options.Context,
                handles,
                options.FixedDeltaSeconds,
                inputDelayFrames,
                options.ResolveIdealFrameLimit,
                options.ShouldForceHashMismatch);

            options.ResetTickState?.Invoke();
            CreateInputRuntime(handles, inputDelayFrames);
        }

        private static void CreateWorldRuntime(
            BattleStartPlan plan,
            BattleContext ctx,
            BattleSessionRemoteDrivenWorldRuntime handles,
            float fixedDeltaSeconds,
            int inputDelayFrames,
            Func<WorldId, int> resolveIdealFrameLimit,
            Func<bool> shouldForceHashMismatch)
        {
            var worldRuntime = RemoteDrivenWorldRuntimeFactory.Create(new RemoteDrivenWorldRuntimeFactoryOptions(
                plan,
                fixedDeltaSeconds,
                inputDelayFrames,
                plan.Authority.EnableClientPrediction,
                _ => handles.Consumable,
                _ => ctx != null ? ctx.LocalInputQueue : null,
                resolveIdealFrameLimit,
                RemoteDrivenRollbackRegistryFactory.Create,
                world => CreateStateHash(world, shouldForceHashMismatch)));

            handles.BindWorldRuntime(worldRuntime);
            RemoteDrivenPredictionContextBinder.Bind(ctx, plan, handles.Runtime);
            SessionWorldBootstrapValidator.ValidateServices(handles.World, "RemoteDrivenLocalWorld");
        }

        private static Func<FrameIndex, WorldStateHash> CreateStateHash(
            IWorld world,
            Func<bool> shouldForceHashMismatch)
        {
            return RemoteDrivenStateHashFactory.Create(
                world,
                () => shouldForceHashMismatch != null && shouldForceHashMismatch());
        }

        private static void CreateInputRuntime(
            BattleSessionRemoteDrivenWorldRuntime handles,
            int inputDelayFrames)
        {
            var inputRuntime = RemoteDrivenInputRuntime.Create(inputDelayFrames);
            handles.BindInputRuntime(inputRuntime);
            inputRuntime?.PublishDebugStats();
        }

        private static int ResolveInputDelay(BattleStartPlan plan)
        {
            return SessionSimRuntimeTuning.NormalizeInputDelayFrames(plan.World.InputDelayFrames);
        }
    }
}
