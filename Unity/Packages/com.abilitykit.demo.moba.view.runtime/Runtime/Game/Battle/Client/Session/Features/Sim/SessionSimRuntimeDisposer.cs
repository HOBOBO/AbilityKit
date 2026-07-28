using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    internal static class SessionSimRuntimeDisposer
    {
        public static void DestroyBattleWorlds(
            BattleStartPlan plan,
            BattleSessionHandles handles)
        {
            DestroyBattleWorlds(
                () => handles.RemoteDriven.DestroyWorld(new WorldId(plan.World.WorldId)),
                () => handles.Confirmed.DestroyWorld(ConfirmedAuthorityWorldId.Create(plan)));
        }

        internal static void DestroyBattleWorlds(
            Action destroyRemoteDrivenWorld,
            Action destroyConfirmedWorld)
        {
            ExecuteCleanupSteps(
                "Failed to destroy battle worlds.",
                destroyRemoteDrivenWorld,
                destroyConfirmedWorld);
        }

        public static void DisposeConfirmedView(
            GameFlowDomain flow,
            BattleSessionConfirmedWorldRuntime handles,
            Action<IEntity> destroyEntityTree)
        {
            ExecuteCleanupSteps(
                "Failed to dispose confirmed view resources.",
                () => DetachConfirmedViewFeature(flow, handles),
                handles.DisposeViewSnapshotRuntime,
                () => DisposeConfirmedViewContext(handles, destroyEntityTree));
        }

        public static void DisposeRemoteDrivenWorld(
            BattleSessionRemoteDrivenWorldRuntime handles,
            Action resetTickState)
        {
            ExecuteCleanupSteps(
                "Failed to dispose remote-driven world resources.",
                handles.ClearWorldRuntime,
                resetTickState,
                handles.DisposeInput);
        }

        public static void DisposeConfirmedWorld(
            BattleContext ctx,
            BattleSessionConfirmedWorldRuntime handles,
            Action resetTickState)
        {
            ExecuteCleanupSteps(
                "Failed to dispose confirmed world resources.",
                handles.ClearWorldRuntime,
                resetTickState,
                handles.DisposeInput,
                handles.DisposeViewEventPipeline,
                () => ConfirmedAuthorityDebugStatsPublisher.Clear(ctx));
        }

        internal static void ExecuteCleanupSteps(string message, params Action[] cleanupSteps)
        {
            var failures = new List<Exception>(cleanupSteps?.Length ?? 0);
            if (cleanupSteps != null)
            {
                for (var i = 0; i < cleanupSteps.Length; i++)
                {
                    TryCleanup(cleanupSteps[i], failures);
                }
            }

            if (failures.Count == 1) throw failures[0];
            if (failures.Count > 1) throw new AggregateException(message, failures);
        }

        private static void DetachConfirmedViewFeature(
            GameFlowDomain flow,
            BattleSessionConfirmedWorldRuntime handles)
        {
            var feature = handles.GetViewFeature();
            if (flow != null && feature != null) flow.Detach(feature);
            handles.ClearViewFeature(feature);
        }

        private static void DisposeConfirmedViewContext(
            BattleSessionConfirmedWorldRuntime handles,
            Action<IEntity> destroyEntityTree)
        {
            var context = handles.GetViewContext();
            ConfirmedViewContextDisposer.Dispose(context, destroyEntityTree);
            handles.ClearViewContext(context);
        }

        private static void TryCleanup(Action cleanup, ICollection<Exception> failures)
        {
            try
            {
                cleanup?.Invoke();
            }
            catch (Exception ex)
            {
                failures.Add(ex);
            }
        }
    }
}
