using System;
using System.Threading.Tasks;
using AbilityKit.Core.Logging;
using AbilityKit.Game.Flow.Battle.Modules;

namespace AbilityKit.Game.Flow
{
    internal interface ISessionPlanHost
    {
        void ValidateModulesForPlan(BattleStartPlan plan);
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
            BattleContext ctx,
            Func<bool> isCurrent = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (handles == null) throw new ArgumentNullException(nameof(handles));
            if (isCurrent != null && !isCurrent()) return;

            var plan = BuildPlan(bootstrapper);
            host.ValidateModulesForPlan(plan);
            state.Plan = plan;

            LogPlan(plan);

            var startedImmediately = false;
            if (!IsSessionStartIntercepted(host, hooks, plan))
            {
                if (!await TryStartSessionAsync(host, plan, isCurrent)) return;
                startedImmediately = true;
            }
            if (isCurrent != null && !isCurrent()) return;

            SessionContextBinder.BindSession(ctx, state, handles, hooks, plan);
            if (isCurrent != null && !isCurrent()) return;
            if (startedImmediately &&
                host is BattleSessionFeature feature &&
                !await TryBeginColdStartRecoveryAsync(host, feature, isCurrent))
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
            BattleStartPlan plan,
            Func<bool> isCurrent)
        {
            try
            {
                await host.StartSessionAsync();
                if (isCurrent != null && !isCurrent()) return false;
                host.NotifySessionStarted(plan);
                if (isCurrent != null && !isCurrent()) return false;
                if (plan.RunModeOptions.RunMode != BattleRunMode.Replay)
                    host.ApplyAutoPlanActions();
                return true;
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[BattleSessionFeature] StartSession failed in OnAttach");
                var failure = await StopAfterFailureAsync(host, ex);
                if (isCurrent == null || isCurrent()) host.NotifySessionFailed(failure);
                return false;
            }
        }

        private static async Task<bool> TryBeginColdStartRecoveryAsync(
            ISessionPlanHost host,
            BattleSessionFeature feature,
            Func<bool> isCurrent)
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
                var failure = await StopAfterFailureAsync(host, ex);
                if (isCurrent == null || isCurrent()) host.NotifySessionFailed(failure);
                return false;
            }
        }

        private static async Task<Exception> StopAfterFailureAsync(
            ISessionPlanHost host,
            Exception failure)
        {
            try
            {
                await host.StopSessionAsync();
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
