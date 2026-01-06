using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Server;
using AbilityKit.Ability.Share.Impl.Moba.Move;
using AbilityKit.Ability.Share.Math;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class MobaLobbyInputSink : IWorldInputSink
    {
        private readonly MobaLobbyStateService _lobby;
        private readonly MobaEnterGameFlowService _enterGame;
        private readonly MobaPlayerActorMapService _playerActorMap;
        private readonly MobaActorLookupService _actorLookup;
        private readonly global::Contexts _contexts;
        private readonly MobaMoveService _moves;
        private readonly SkillExecutor _skills;

        private readonly Dictionary<int, Action<PlayerInputCommand>> _handlers;

        public MobaLobbyInputSink(
            MobaLobbyStateService lobby,
            MobaEnterGameFlowService enterGame,
            MobaPlayerActorMapService playerActorMap,
            MobaActorLookupService actorLookup,
            global::Contexts contexts,
            MobaMoveService moves)
            : this(lobby, enterGame, playerActorMap, actorLookup, contexts, moves, skills: null)
        {
        }

        public MobaLobbyInputSink(MobaLobbyStateService lobby, MobaEnterGameFlowService enterGame, MobaPlayerActorMapService playerActorMap, MobaActorLookupService actorLookup, global::Contexts contexts, MobaMoveService moves, SkillExecutor skills)
        {
            _lobby = lobby ?? throw new ArgumentNullException(nameof(lobby));
            _enterGame = enterGame ?? throw new ArgumentNullException(nameof(enterGame));
            _playerActorMap = playerActorMap ?? throw new ArgumentNullException(nameof(playerActorMap));
            _actorLookup = actorLookup ?? throw new ArgumentNullException(nameof(actorLookup));
            _contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));
            _moves = moves ?? throw new ArgumentNullException(nameof(moves));
            _skills = skills;

            _handlers = new Dictionary<int, Action<PlayerInputCommand>>
            {
                { (int)MobaOpCode.Ready, cmd => _lobby.SetReady(cmd.Player, true) },
                { (int)MobaOpCode.Unready, cmd => _lobby.SetReady(cmd.Player, false) },
                { (int)MobaOpCode.Move, HandleMove },
                { (int)MobaOpCode.Skill1, cmd => HandleSkill(cmd, 1) },
                { (int)MobaOpCode.Skill2, cmd => HandleSkill(cmd, 2) },
                { (int)MobaOpCode.Skill3, cmd => HandleSkill(cmd, 3) },
            };
        }

        private void HandleMove(PlayerInputCommand cmd)
        {
            if (!_lobby.Started) return;
            if (!_playerActorMap.TryGetActorId(cmd.Player, out var actorId)) return;
            if (!_actorLookup.TryGetActorEntity(actorId, out var entity) || entity == null) return;
            if (!entity.hasTransform) return;

            MobaMoveCodec.Deserialize(cmd.Payload, out var dx, out var dz);
            _moves.SetInput(actorId, dx, dz);
        }

        private void HandleSkill(PlayerInputCommand cmd, int slot)
        {
            if (!_lobby.Started) return;
            if (!_playerActorMap.TryGetActorId(cmd.Player, out var actorId)) return;
            if (!_actorLookup.TryGetActorEntity(actorId, out var entity) || entity == null) return;
            if (!entity.hasTransform) return;

            _skills?.CastBySlot(actorId, slot);
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
