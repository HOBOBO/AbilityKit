using System;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Network.Room;

namespace AbilityKit.Game.Flow
{
    internal sealed class LobbyBattleEntryCoordinator
    {
        private readonly MultiplayerBattleEntryGate _gate = new MultiplayerBattleEntryGate();
        private int _generation;

        public void Attach()
        {
            _generation++;
            _gate.Reset();
        }

        public void Detach()
        {
            _generation++;
            _gate.Reset();
        }

        public bool TryEnter(
            MultiplayerRoomFlowState state,
            MultiplayerRoomSnapshot snapshot,
            Action enter)
        {
            if (enter == null) throw new ArgumentNullException(nameof(enter));
            var generation = _generation;
            if (!_gate.TryAccept(state, snapshot)) return false;

            try
            {
                if (generation != _generation)
                {
                    _gate.Reset();
                    return false;
                }

                enter();
                return true;
            }
            catch
            {
                if (generation == _generation) _gate.Reset();
                throw;
            }
        }

        public bool TryEnterBattle(
            MultiplayerRoomFlowState state,
            MultiplayerRoomSnapshot snapshot,
            LobbyBattleEntrySelection selection,
            GatewayMultiplayerRoomSession session,
            DemoMultiplayerLaunchRequest launchRequest,
            uint localPlayerId,
            bool coldStartReconnect,
            Action<IBattleBootstrapper> enter)
        {
            if (selection == null) throw new ArgumentNullException(nameof(selection));
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (enter == null) throw new ArgumentNullException(nameof(enter));

            return TryEnter(state, snapshot, () =>
            {
                var configured = new ConfiguredBattleBootstrapper(
                    selection.Config,
                    selection.Preset);
                enter(new ExistingGatewayRoomBattleBootstrapper(
                    configured,
                    session.SessionToken,
                    snapshot.RoomId,
                    snapshot.BattleId,
                    snapshot.NumericRoomId,
                    snapshot.WorldId,
                    localPlayerId,
                    session,
                    launchRequest,
                    snapshot.Players,
                    snapshot.SyncCapabilities,
                    coldStartReconnect));
            });
        }
    }
}
