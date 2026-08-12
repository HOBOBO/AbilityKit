using System;
using System.Threading.Tasks;
using AbilityKit.Ability.Host.Extensions.FrameSync.Spectator;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Network.Battle;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        public SpectatorWorldDriver SpectatorDriver => _runtime.Spectator.Driver;

        public IWorld SpectatorWorld => _runtime.Spectator.World;

        public bool IsSpectating => _runtime.Spectator.IsSpectating;

        public Task TryStartSpectating(
            INetworkClient gatewayClient,
            ulong roomId,
            Func<IWorld> worldFactory)
        {
            return _runtime.Spectator.StartAsync(gatewayClient, roomId, worldFactory);
        }

        public void StopSpectating()
        {
            _runtime.Spectator.Stop();
        }

        public void UpdateSpectatorWorld(int stepsBudget = 10)
        {
            _runtime.Spectator.Tick(stepsBudget);
        }
    }
}
