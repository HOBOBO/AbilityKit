using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Server;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class MobaLobbyInputSink : IWorldInputSink
    {
        private readonly MobaLobbyStateService _lobby;
        private readonly MobaEnterGameFlowService _enterGame;
        private readonly global::Contexts _contexts;

        private readonly Dictionary<int, Action<PlayerInputCommand>> _handlers;

        public MobaLobbyInputSink(MobaLobbyStateService lobby, MobaEnterGameFlowService enterGame, global::Contexts contexts)
        {
            _lobby = lobby ?? throw new ArgumentNullException(nameof(lobby));
            _enterGame = enterGame ?? throw new ArgumentNullException(nameof(enterGame));
            _contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));

            _handlers = new Dictionary<int, Action<PlayerInputCommand>>
            {
                { (int)MobaOpCode.Ready, cmd => _lobby.SetReady(cmd.Player, true) },
                { (int)MobaOpCode.Unready, cmd => _lobby.SetReady(cmd.Player, false) },
            };
        }

        public void Submit(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs)
        {
            if (inputs == null || inputs.Count == 0) return;

            for (int i = 0; i < inputs.Count; i++)
            {
                var cmd = inputs[i];
                if (_handlers.TryGetValue(cmd.OpCode, out var handler)) handler(cmd);
            }

            _enterGame.TryStartGame(_contexts.actor);
        }
    }
}
