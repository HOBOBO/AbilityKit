using System;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public interface IMobaGameStartOrchestrator : IService
    {
        bool TryStartGame(ActorContext actorContext);
    }

    public sealed class MobaGameStartOrchestrator : IMobaGameStartOrchestrator
    {
        private readonly MobaLobbyStateService _lobby;
        private readonly MobaEnterGameFlowService _flow;

        public MobaGameStartOrchestrator(MobaLobbyStateService lobby, MobaEnterGameFlowService flow)
        {
            _lobby = lobby ?? throw new ArgumentNullException(nameof(lobby));
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
        }

        public bool TryStartGame(ActorContext actorContext)
        {
            if (actorContext == null) throw new ArgumentNullException(nameof(actorContext));

            if (_lobby.Started)
            {
                Log.Info("[MobaGameStartOrchestrator] TryStartGame: already started");
                return false;
            }
            if (!_lobby.CanStartGame())
            {
                Log.Info($"[MobaGameStartOrchestrator] TryStartGame: CanStartGame=false (playerCount={_lobby.PlayerCount}, allReady={_lobby.AllReady})");
                return false;
            }
            if (!_lobby.TryMarkStarted())
            {
                Log.Info("[MobaGameStartOrchestrator] TryStartGame: TryMarkStarted failed");
                return false;
            }

            if (!_lobby.TryGetGameStartSpec(out var spec))
            {
                Log.Info("[MobaGameStartOrchestrator] TryStartGame: GameStartSpec not found");
                return false;
            }

            return _flow.ApplyGameStartSpec(actorContext, in spec);
        }

        public void Dispose()
        {
        }
    }
}
