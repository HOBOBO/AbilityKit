using System;
using System.Collections.Generic;
using AbilityKit.Core.Common.Log;
using AbilityKit.Game.Flow.Battle.Modules;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private interface IGatewayRoomModuleHost
        {
            bool ShouldPrepareGatewayRoom();
            void StartGatewayRoomPreparation();
            void StopGatewayRoomPreparation();

            bool HasGatewayRoomConnection { get; }
            void TickGatewayRoomConnection(float deltaTime);
            System.Threading.Tasks.Task GatewayRoomTask { get; }
        }

        private sealed class GatewayRoomModule : IBattleSessionModule, IBattleSessionModuleId, IBattleSessionModuleDependencies
        {
            private readonly IGatewayRoomModuleHost _host;

            private IDisposable _planBuiltSub;

            private BattleEventBus _events;

            public GatewayRoomModule(IGatewayRoomModuleHost host)
            {
                _host = host;
            }

            public string Id => "gateway_room";

            public IEnumerable<string> Dependencies => null;

            public void OnAttach(in BattleSessionModuleContext ctx)
            {
                _events = ctx.Events;

                _planBuiltSub = ctx.Events?.SubscribeIntercept<PlanBuiltEvent>(_ =>
                {
                    if (_host == null || !_host.ShouldPrepareGatewayRoom()) return false;
                    _host.StartGatewayRoomPreparation();
                    return true;
                });
            }

            public void OnDetach(in BattleSessionModuleContext ctx)
            {
                _planBuiltSub?.Dispose();
                _planBuiltSub = null;

                _events = null;

                _host?.StopGatewayRoomPreparation();
            }

            public void Tick(in BattleSessionModuleContext ctx, float deltaTime)
            {
            }

            public void PreTick(in BattleSessionModuleContext ctx, float deltaTime)
            {
                if (_host == null || !_host.HasGatewayRoomConnection) return;

                _host.TickGatewayRoomConnection(deltaTime);

                var task = _host.GatewayRoomTask;
                if (task == null || !task.IsCompleted) return;

                if (task.IsFaulted)
                {
                    var ex = task.Exception != null ? task.Exception.GetBaseException() : null;
                    var wrapped = new InvalidOperationException("Gateway room preparation failed.", ex);
                    Log.Exception(wrapped, "[BattleSessionFeature] Gateway room preparation failed");
                    _host.StopGatewayRoomPreparation();
                    _events?.Publish(new SessionFailedEvent(wrapped));
                    return;
                }

                _host.StopGatewayRoomPreparation();

                _events?.Publish(new StartSessionRequested());
            }
        }
    }
}
