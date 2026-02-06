using System;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Game.Flow.Battle.Modules;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private sealed class SessionPlanSubFeature :
            ISessionSubFeature<BattleSessionFeature>,
            IGameModuleId,
            IGameModuleDependencies
        {
            public string Id => "session_plan";

            public System.Collections.Generic.IEnumerable<string> Dependencies => new[] { "session_events" };

            public void OnAttach(in FeatureModuleContext<BattleSessionFeature> ctx)
            {
                var f = ctx.Feature;
                if (f == null) return;

                f._plan = f._bootstrapper?.Build() ?? default;

                f.Events?.Publish(new PlanBuiltEvent(f._plan));
                f.Events?.Flush();

                var planBuiltHandled = f.Events != null && f.Events.Intercept(new PlanBuiltEvent(f._plan));
                planBuiltHandled = (f.Hooks != null && f.Hooks.PlanBuilt.Invoke(f._plan)) || planBuiltHandled;

                Log.Info($"[BattleSessionFeature] OnAttach Plan: HostMode={f._plan.HostMode}, UseGatewayTransport={f._plan.UseGatewayTransport}, Gateway={f._plan.GatewayHost}:{f._plan.GatewayPort}, NumericRoomId={f._plan.NumericRoomId}, AutoConnect={f._plan.AutoConnect}, AutoCreateWorld={f._plan.AutoCreateWorld}, AutoJoin={f._plan.AutoJoin}, AutoReady={f._plan.AutoReady}, WorldId={f._plan.WorldId}, PlayerId={f._plan.PlayerId}");

                if (!(planBuiltHandled || f.InvokeModulesPlanBuilt()))
                {
                    try
                    {
                        f.StartSession();
                        f.Events?.Publish(new SessionStartedEvent(f._plan));
                        f.Events?.Flush();
                        f.ApplyAutoPlanActions();
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex, "[BattleSessionFeature] StartSession failed in OnAttach");
                        f.StopSession();
                        f.Events?.Publish(new SessionFailedEvent(ex));
                        f.Events?.Flush();
                        return;
                    }
                }

                if (f._ctx != null)
                {
                    f._ctx.Plan = f._plan;
                    f._ctx.Session = f._session;
                    f._ctx.LastFrame = f._lastFrame;
                }
            }

            public void OnDetach(in FeatureModuleContext<BattleSessionFeature> ctx)
            {
            }

            public void Tick(in FeatureModuleContext<BattleSessionFeature> ctx, float deltaTime) { }

            public void RebindAll(in FeatureModuleContext<BattleSessionFeature> ctx) { }
        }
    }
}
