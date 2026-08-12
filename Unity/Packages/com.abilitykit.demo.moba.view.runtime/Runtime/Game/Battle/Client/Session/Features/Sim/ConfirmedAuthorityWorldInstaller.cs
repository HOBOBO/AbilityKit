using System;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Game.Flow
{
    internal readonly struct ConfirmedAuthorityWorldInstallOptions
    {
        public readonly BattleStartPlan Plan;
        public readonly BattleContext Context;
        public readonly GameFlowDomain Flow;
        public readonly BattleSessionConfirmedWorldRuntime Handles;
        public readonly BattleSessionDiagnostics Diagnostics;
        public readonly bool HasSession;
        public readonly float FixedDeltaSeconds;
        public readonly Func<WorldId, int> ResolveIdealFrameLimit;
        public readonly Action ResetTickState;

        public ConfirmedAuthorityWorldInstallOptions(
            BattleStartPlan plan,
            BattleContext context,
            GameFlowDomain flow,
            BattleSessionConfirmedWorldRuntime handles,
            BattleSessionDiagnostics diagnostics,
            bool hasSession,
            float fixedDeltaSeconds,
            Func<WorldId, int> resolveIdealFrameLimit,
            Action resetTickState)
        {
            Plan = plan;
            Context = context;
            Flow = flow;
            Handles = handles;
            Diagnostics = diagnostics;
            HasSession = hasSession;
            FixedDeltaSeconds = fixedDeltaSeconds;
            ResolveIdealFrameLimit = resolveIdealFrameLimit;
            ResetTickState = resetTickState;
        }
    }

    internal static class ConfirmedAuthorityWorldInstaller
    {
        public static void EnsureStarted(ConfirmedAuthorityWorldInstallOptions options)
        {
            var handles = options.Handles;
            if (handles.World != null) return;

            options.ResetTickState?.Invoke();

            CreateWorldRuntime(
                options.Plan,
                handles,
                options.FixedDeltaSeconds,
                options.ResolveIdealFrameLimit);

            CreateInputRuntime(handles);
            CreateViewEventPipeline(options.Plan, handles, options.HasSession);
            ConfirmedAuthorityDebugStatsPublisher.Initialize(
                options.Diagnostics,
                ConfirmedAuthorityWorldId.Create(options.Plan));
        }

        private static void CreateWorldRuntime(
            BattleStartPlan plan,
            BattleSessionConfirmedWorldRuntime handles,
            float fixedDeltaSeconds,
            Func<WorldId, int> resolveIdealFrameLimit)
        {
            var worldRuntime = ConfirmedAuthorityWorldRuntimeFactory.Create(
                plan,
                fixedDeltaSeconds,
                _ => handles.Consumable,
                resolveIdealFrameLimit);

            handles.BindWorldRuntime(worldRuntime);
        }

        private static void CreateInputRuntime(BattleSessionConfirmedWorldRuntime handles)
        {
            var inputRuntime = ConfirmedAuthorityInputRuntime.Create();
            handles.BindInputRuntime(inputRuntime);
            SessionWorldBootstrapValidator.ValidateServices(handles.World, "ConfirmedAuthorityWorld");
        }

        private static void CreateViewEventPipeline(
            BattleStartPlan plan,
            BattleSessionConfirmedWorldRuntime handles,
            bool hasSession)
        {
            if (!hasSession) return;

            var pipeline = ConfirmedViewEventPipelineFactory.Create(
                handles.World,
                plan.Sync.ViewEventSourceMode,
                maxDebugLines: 32);

            handles.BindViewEventPipeline(pipeline);
        }
    }
}
