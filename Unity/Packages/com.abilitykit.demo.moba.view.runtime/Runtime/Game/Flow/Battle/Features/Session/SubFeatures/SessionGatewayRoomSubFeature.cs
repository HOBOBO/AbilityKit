using System;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Game.Flow.Battle.Modules;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private sealed class SessionGatewayRoomSubFeature :
            ISessionSubFeature<BattleSessionFeature>,
            ISessionPreTickSubFeature<BattleSessionFeature>,
            IGameModuleId,
            IGameModuleDependencies
        {
            private IDisposable _planBuiltSub;

            private BattleEventBus _events;

            public string Id => "gateway_room";

            public System.Collections.Generic.IEnumerable<string> Dependencies => new[] { "session_events" };

            public void OnAttach(in FeatureModuleContext<BattleSessionFeature> ctx)
            {
                var f = ctx.Feature;
                if (f == null) return;

                _events = f.Events;

                _planBuiltSub = _events?.SubscribeIntercept<PlanBuiltEvent>(_ =>
                {
                    if (!f.ShouldPrepareGatewayRoom()) return false;
                    f.StartGatewayRoomPreparation();
                    return true;
                });
            }

            public void OnDetach(in FeatureModuleContext<BattleSessionFeature> ctx)
            {
                _planBuiltSub?.Dispose();
                _planBuiltSub = null;

                _events = null;

                var f = ctx.Feature;
                f?.StopGatewayRoomPreparation();
            }

            public void PreTick(in FeatureModuleContext<BattleSessionFeature> ctx, float deltaTime)
            {
                var f = ctx.Feature;
                if (f == null) return;
                if (!f.HasGatewayRoomConnection) return;

                f.TickGatewayRoomConnection(deltaTime);

                var task = f.GatewayRoomPreparationTask;
                if (task == null || !task.IsCompleted) return;

                if (task.IsFaulted)
                {
                    var ex = task.Exception != null ? task.Exception.GetBaseException() : null;
                    var wrapped = new InvalidOperationException("Gateway room preparation failed.", ex);
                    Log.Exception(wrapped, "[BattleSessionFeature] Gateway room preparation failed");
                    f.StopGatewayRoomPreparation();
                    _events?.Publish(new SessionFailedEvent(wrapped));
                    return;
                }

                f.StopGatewayRoomPreparation();

                _events?.Publish(new StartSessionRequested());
            }

            public void Tick(in FeatureModuleContext<BattleSessionFeature> ctx, float deltaTime) { }

            public void RebindAll(in FeatureModuleContext<BattleSessionFeature> ctx) { }
        }
    }
}
