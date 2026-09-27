using System;
using System.Threading.Tasks;
using AbilityKit.Core.Logging;
using AbilityKit.Game.Flow.Battle.Modules;

namespace AbilityKit.Game.Flow
{
    internal interface ISessionPlanHost
    {
        Task StartSessionAsync();
        Task StopSessionAsync();
        void ApplyAutoPlanActions();
        bool InvokeSubFeaturesPlanBuilt();
        void NotifySessionStarted(BattleStartPlan plan);
        void NotifySessionFailed(Exception exception);
    }

    internal sealed class SessionPlanController
    {
        public async Task OnAttachAsync(
            ISessionPlanHost host,
            IBattleBootstrapper bootstrapper,
            BattleSessionState state,
            BattleSessionHandles handles,
            BattleSessionHooks hooks,
            BattleContext ctx)
        {
            if (host == null || state == null || handles == null) return;

            var plan = BuildPlan(bootstrapper);
            state.Plan = plan;

            LogPlan(plan);

            var startedImmediately = false;
            if (!IsSessionStartIntercepted(host, hooks, plan))
            {
                if (!await TryStartSessionAsync(host, plan).ConfigureAwait(false)) return;
                startedImmediately = true;
            }

            SessionContextBinder.BindSession(ctx, state, handles, hooks, plan);
            if (startedImmediately &&
                host is BattleSessionFeature feature &&
                !await TryBeginColdStartRecoveryAsync(host, feature).ConfigureAwait(false))
            {
                return;
            }
        }

        private static BattleStartPlan BuildPlan(IBattleBootstrapper bootstrapper)
        {
            return bootstrapper?.Build() ?? default;
        }

        private static void LogPlan(BattleStartPlan plan)
        {
            var world = plan.World;
            var gateway = plan.Gateway;
            var auto = plan.Auto;
            Log.Info($"[BattleSessionFeature] OnAttach Plan: HostMode={plan.HostMode}, UseGatewayTransport={gateway.UseGatewayTransport}, Gateway={gateway.Host}:{gateway.Port}, NumericRoomId={gateway.NumericRoomId}, AutoConnect={auto.AutoConnect}, AutoCreateWorld={auto.AutoCreateWorld}, AutoJoin={auto.AutoJoin}, AutoReady={auto.AutoReady}, WorldId={world.WorldId}, PlayerId={world.PlayerId}");
        }

        private static bool IsSessionStartIntercepted(ISessionPlanHost host, BattleSessionHooks hooks, BattleStartPlan plan)
        {
            if (hooks != null && hooks.PlanBuilt.Invoke(plan)) return true;

            return host.InvokeSubFeaturesPlanBuilt();
        }

        private static async Task<bool> TryStartSessionAsync(
            ISessionPlanHost host,
            BattleStartPlan plan)
        {
            try
            {
                await host.StartSessionAsync().ConfigureAwait(false);
                host.NotifySessionStarted(plan);
                host.ApplyAutoPlanActions();
                return true;
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[BattleSessionFeature] StartSession failed in OnAttach");
                var failure = await StopAfterFailureAsync(host, ex).ConfigureAwait(false);
                host.NotifySessionFailed(failure);
                return false;
            }
        }

        private static async Task<bool> TryBeginColdStartRecoveryAsync(
            ISessionPlanHost host,
            BattleSessionFeature feature)
        {
            try
            {
                // Cold recovery reads the bound BattleContext plan and session. It must run only
                // after SessionContextBinder has published both; otherwise the context still has
                // the default Local plan and BeginColdStartRecovery rejects the restored battle.
                feature.BeginColdStartRecoveryAfterImmediateSessionStart();
                return true;
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[BattleSessionFeature] Cold-start recovery failed in OnAttach");
                var failure = await StopAfterFailureAsync(host, ex).ConfigureAwait(false);
                host.NotifySessionFailed(failure);
                return false;
            }
        }

        private static async Task<Exception> StopAfterFailureAsync(
            ISessionPlanHost host,
            Exception failure)
        {
            try
            {
                await host.StopSessionAsync().ConfigureAwait(false);
                return failure;
            }
            catch (Exception cleanupFailure)
            {
                return new AggregateException(
                    "Session startup failed and cleanup also reported failures.",
                    failure,
                    cleanupFailure);
            }
        }

    }
}
