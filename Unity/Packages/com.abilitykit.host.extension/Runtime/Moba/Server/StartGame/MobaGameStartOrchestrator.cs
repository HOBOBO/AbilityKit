using System;
using AbilityKit.Core.Common.Log;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Ability.Host.Extensions.Moba.Room;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Host.Extensions.Moba.StartGame
{
    public interface IMobaGameStartOrchestrator : IService
    {
        bool TryStartGame(ActorContext actorContext);
    }

    public sealed class MobaGameStartOrchestrator : IMobaGameStartOrchestrator
    {
        private readonly IMobaRoomOrchestrator _room;
        private readonly MobaEnterGameFlowService _flow;

        private bool _started;

        public MobaGameStartOrchestrator(IMobaRoomOrchestrator room, MobaEnterGameFlowService flow)
        {
            _room = room ?? throw new ArgumentNullException(nameof(room));
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
        }

        public bool TryStartGame(ActorContext actorContext)
        {
            if (actorContext == null) throw new ArgumentNullException(nameof(actorContext));

            if (_started)
            {
                Log.Info("[MobaGameStartOrchestrator] TryStartGame: already started");
                return false;
            }

            if (!CanStartGame(_room))
            {
                Log.Info("[MobaGameStartOrchestrator] TryStartGame: CanStartGame=false");
                return false;
            }

            if (!TryBuildGameStartSpec(_room, out var spec))
            {
                Log.Info("[MobaGameStartOrchestrator] TryStartGame: GameStartSpec not found/built");
                return false;
            }

            var ok = _flow.ApplyGameStartSpec(actorContext, in spec);
            if (ok) _started = true;
            return ok;
        }

        private static bool CanStartGame(IMobaRoomOrchestrator room)
        {
            if (room == null) return false;
            return room.State != null && room.State.CanStart();
        }

        private static bool TryBuildGameStartSpec(IMobaRoomOrchestrator room, out MobaGameStartSpec spec)
        {
            spec = default;
            if (room?.State == null) return false;

            // Pick any joined player as localPlayerId for spec building.
            foreach (var kv in room.State.Players)
            {
                var localPlayerId = new PlayerId(kv.Key);
                return room.TryBuildGameStartSpec(localPlayerId, out spec);
            }

            return false;
        }

        public void Dispose()
        {
        }
    }
}
