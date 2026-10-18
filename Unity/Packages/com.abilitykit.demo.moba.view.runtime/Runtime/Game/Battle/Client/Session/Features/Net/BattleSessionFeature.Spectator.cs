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

        public async Task TryStartSpectating(
            INetworkClient gatewayClient,
            ulong roomId,
            Func<IWorld> worldFactory)
        {
            HideSpectatorView();
            await _runtime.Spectator.StartAsync(gatewayClient, roomId, worldFactory);
        }

        public void StopSpectating()
        {
            SessionAsyncOperation.RequireCompleted(
                StopSpectatingAsync(),
                "Spectator session stop");
        }

        public async Task StopSpectatingAsync()
        {
            Exception presentationFailure = null;
            try { HideSpectatorView(); }
            catch (Exception error) { presentationFailure = error; }
            try { await _runtime.Spectator.StopAsync(); }
            catch (Exception stopFailure)
            {
                if (presentationFailure != null)
                    throw new AggregateException(presentationFailure, stopFailure);
                throw;
            }
            if (presentationFailure != null) throw presentationFailure;
        }

        public void UpdateSpectatorWorld(int stepsBudget = 10)
        {
            _runtime.Spectator.Tick(stepsBudget);
        }
    }
}
