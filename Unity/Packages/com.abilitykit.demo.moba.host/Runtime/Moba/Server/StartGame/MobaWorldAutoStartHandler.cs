using System;
using AbilityKit.Ability.Host.Extensions.WorldStart;
using AbilityKit.Core.Logging;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.Host.Extensions.Moba.Room;

namespace AbilityKit.Ability.Host.Extensions.Moba.StartGame
{
    public sealed class MobaWorldAutoStartHandler : IWorldAutoStartHandler
    {
        private readonly IMobaRoomOrchestrator _room;
        private readonly IMobaGameStartOrchestrator _orchestrator;

        public MobaWorldAutoStartHandler(IMobaRoomOrchestrator room, IMobaGameStartOrchestrator orchestrator)
        {
            _room = room ?? throw new ArgumentNullException(nameof(room));
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        }

        public bool TryAutoStart(IWorld world, float deltaTime)
        {
            if (!CanStartGame(_room)) return false;

            try
            {
                return _orchestrator.TryStartGame(world);
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
