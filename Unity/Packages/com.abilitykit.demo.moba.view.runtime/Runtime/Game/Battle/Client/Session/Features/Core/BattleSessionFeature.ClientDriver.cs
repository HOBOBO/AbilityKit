using System;
using System.Threading.Tasks;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private sealed class SessionDriverHost : IBattleClientSessionHost
        {
            private readonly BattleSessionFeature _owner;

            public SessionDriverHost(BattleSessionFeature owner)
            {
                _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            }

            public Task StartAsync() => _owner._orchestrator.StartSessionAsync();
            public Task StopAsync() => _owner._orchestrator.StopSessionAsync();
            public void Tick(float deltaTime) => _owner._runtime.TickLoop.MainTick(deltaTime);
        }
    }
}
