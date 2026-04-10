using System;
using AbilityKit.Ability.Host.Extensions.WorldStart;
using AbilityKit.Core.Common.Log;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.Host.Extensions.Moba.Room;

namespace AbilityKit.Ability.Host.Extensions.Moba.StartGame
{
    public sealed class MobaWorldAutoStartHandler : IWorldAutoStartHandler
    {
        private readonly IMobaRoomOrchestrator _room;
        private readonly IMobaGameStartOrchestrator _orchestrator;
        private readonly Entitas.IContexts _contexts;

        public MobaWorldAutoStartHandler(IMobaRoomOrchestrator room, IMobaGameStartOrchestrator orchestrator, Entitas.IContexts contexts)
        {
            _room = room ?? throw new ArgumentNullException(nameof(room));
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));
        }

        public bool TryAutoStart(IWorld world, float deltaTime)
        {
            if (!CanStartGame(_room)) return false;

            try
            {
                var actorContext = ((global::Contexts)_contexts).actor;
                return _orchestrator.TryStartGame(actorContext);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[MobaWorldAutoStartHandler(host.extension.moba)] TryAutoStart failed");
                return false;
            }
        }

        private static bool CanStartGame(IMobaRoomOrchestrator room)
        {
            if (room == null) return false;
            return room.State != null && room.State.CanStart();
        }

        public void Dispose()
        {
        }
    }
}
