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
            BattleSessionDiagnostics diagnostics,
            Action resetTickState)
        {
            ExecuteCleanupSteps(
                "Failed to dispose confirmed world resources.",
                handles.ClearWorldRuntime,
                resetTickState,
                handles.DisposeInput,
                handles.DisposeViewEventPipeline,
                () => ConfirmedAuthorityDebugStatsPublisher.Clear(diagnostics));
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
