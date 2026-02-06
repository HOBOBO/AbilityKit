using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Game.Flow.Battle.Modules;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private sealed class GatewayRoomModule : IBattleSessionModule, IBattleSessionModuleId, IBattleSessionModuleDependencies
        {
            private readonly BattleSessionFeature _feature;

            private IDisposable _planBuiltSub;

            private BattleEventBus _events;

            public GatewayRoomModule(BattleSessionFeature feature)
            {
                _feature = feature;
            }

            public string Id => "gateway_room";

            public IEnumerable<string> Dependencies => null;

            public void OnAttach(in BattleSessionModuleContext ctx)
            {
                _events = ctx.Events;

                _planBuiltSub = ctx.Events?.SubscribeIntercept<PlanBuiltEvent>(_ =>
                {
                    if (!_feature.ShouldPrepareGatewayRoom()) return false;
                    _feature.StartGatewayRoomPreparation();
                    return true;
                });
            }

            public void OnDetach(in BattleSessionModuleContext ctx)
            {
                _planBuiltSub?.Dispose();
                _planBuiltSub = null;

                _events = null;

                _feature.StopGatewayRoomPreparation();
            }

            public void Tick(in BattleSessionModuleContext ctx, float deltaTime)
            {
            }

            public void PreTick(in BattleSessionModuleContext ctx, float deltaTime)
            {
                if (_feature._gatewayRoomConn == null) return;

                _feature._gatewayRoomConn.Tick(deltaTime);

                if (_feature._gatewayRoomTask == null || !_feature._gatewayRoomTask.IsCompleted) return;

                if (_feature._gatewayRoomTask.IsFaulted)
                {
                    var ex = _feature._gatewayRoomTask.Exception != null ? _feature._gatewayRoomTask.Exception.GetBaseException() : null;
                    var wrapped = new InvalidOperationException("Gateway room preparation failed.", ex);
                    Log.Exception(wrapped, "[BattleSessionFeature] Gateway room preparation failed");
                    _feature.StopGatewayRoomPreparation();
                    _events?.Publish(new SessionFailedEvent(wrapped));
                    return;
                }

                _feature.StopGatewayRoomPreparation();

                _events?.Publish(new StartSessionRequested());
            }
        }
    }
}
