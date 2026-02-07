using System;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Game.Flow.Battle.Modules;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        internal interface ISessionPlanHost
        {
            void StartSession();
            void StopSession();
            void ApplyAutoPlanActions();
            bool InvokeModulesPlanBuilt();
        }

        private sealed class SessionPlanController
        {
            public void OnAttach(
                ISessionPlanHost host,
                IBattleBootstrapper bootstrapper,
                BattleSessionState state,
                BattleSessionHandles handles,
                BattleEventBus events,
                BattleSessionHooks hooks,
                BattleContext ctx)
            {
                if (host == null || state == null || handles == null) return;

                var plan = bootstrapper?.Build() ?? default;
                state.Plan = plan;

                events?.Publish(new PlanBuiltEvent(plan));
                events?.Flush();

                var planBuiltHandled = events != null && events.Intercept(new PlanBuiltEvent(plan));
                planBuiltHandled = (hooks != null && hooks.PlanBuilt.Invoke(plan)) || planBuiltHandled;

                Log.Info($"[BattleSessionFeature] OnAttach Plan: HostMode={plan.HostMode}, UseGatewayTransport={plan.UseGatewayTransport}, Gateway={plan.GatewayHost}:{plan.GatewayPort}, NumericRoomId={plan.NumericRoomId}, AutoConnect={plan.AutoConnect}, AutoCreateWorld={plan.AutoCreateWorld}, AutoJoin={plan.AutoJoin}, AutoReady={plan.AutoReady}, WorldId={plan.WorldId}, PlayerId={plan.PlayerId}");

                if (!(planBuiltHandled || host.InvokeModulesPlanBuilt()))
                {
                    try
                    {
                        host.StartSession();
                        events?.Publish(new SessionStartedEvent(plan));
                        events?.Flush();
                        host.ApplyAutoPlanActions();
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex, "[BattleSessionFeature] StartSession failed in OnAttach");
                        host.StopSession();
                        events?.Publish(new SessionFailedEvent(ex));
                        events?.Flush();
                        return;
                    }
                }

                if (ctx != null)
                {
                    ctx.Plan = plan;
                    ctx.Session = handles.Session;
                    ctx.LastFrame = state.Tick.LastFrame;
                }
            }
        }
    }
}
