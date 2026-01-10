using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Server;
using AbilityKit.Ability.Share.Impl.Moba.Move;
using AbilityKit.Ability.Share.Impl.Moba.Services.EntityManager;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Ability.Share.Math;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class MobaLobbyInputSink : IWorldInputSink
    {
        private readonly MobaLobbyStateService _lobby;
        private readonly MobaEnterGameFlowService _enterGame;
        private readonly MobaPlayerActorMapService _playerActorMap;
        private readonly MobaEntityManager _entities;
        private readonly global::Contexts _contexts;
        private readonly MobaMoveService _moves;
        private readonly SkillExecutor _skills;

        private readonly Dictionary<int, Action<PlayerInputCommand>> _handlers;

        public MobaLobbyInputSink(
            MobaLobbyStateService lobby,
            MobaEnterGameFlowService enterGame,
            MobaPlayerActorMapService playerActorMap,
            MobaEntityManager entities,
            global::Contexts contexts,
            MobaMoveService moves)
            : this(lobby, enterGame, playerActorMap, entities, contexts, moves, skills: null)
        {
        }

        public MobaLobbyInputSink(MobaLobbyStateService lobby, MobaEnterGameFlowService enterGame, MobaPlayerActorMapService playerActorMap, MobaEntityManager entities, global::Contexts contexts, MobaMoveService moves, SkillExecutor skills)
        {
            _lobby = lobby ?? throw new ArgumentNullException(nameof(lobby));
            _enterGame = enterGame ?? throw new ArgumentNullException(nameof(enterGame));
            _playerActorMap = playerActorMap ?? throw new ArgumentNullException(nameof(playerActorMap));
            _entities = entities ?? throw new ArgumentNullException(nameof(entities));
            _contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));
            _moves = moves ?? throw new ArgumentNullException(nameof(moves));
            _skills = skills;

            _handlers = new Dictionary<int, Action<PlayerInputCommand>>
            {
                { (int)MobaOpCode.Ready, cmd => _lobby.SetReady(cmd.Player, true) },
                { (int)MobaOpCode.Unready, cmd => _lobby.SetReady(cmd.Player, false) },
                { (int)MobaOpCode.Move, HandleMove },
                { (int)MobaOpCode.Skill1, cmd => HandleSkillLegacy(cmd, 1) },
                { (int)MobaOpCode.Skill2, cmd => HandleSkillLegacy(cmd, 2) },
                { (int)MobaOpCode.Skill3, cmd => HandleSkillLegacy(cmd, 3) },
                { (int)MobaOpCode.SkillInput, HandleSkillInput },
            };
        }

        private void HandleMove(PlayerInputCommand cmd)
        {
            if (!_lobby.Started) return;
            if (!_playerActorMap.TryGetActorId(cmd.Player, out var actorId)) return;
            if (!TryGetEntity(actorId, out var entity) || entity == null) return;
            if (!entity.hasTransform) return;

            MobaMoveCodec.Deserialize(cmd.Payload, out var dx, out var dz);

            if (!entity.hasMoveInput)
            {
                entity.AddMoveInput(dx, dz);
            }
            else
            {
                entity.ReplaceMoveInput(dx, dz);
            }
        }

        private void HandleSkillLegacy(PlayerInputCommand cmd, int slot)
        {
            if (!_lobby.Started) return;
            if (!_playerActorMap.TryGetActorId(cmd.Player, out var actorId)) return;
            if (!TryGetEntity(actorId, out var entity) || entity == null) return;
            if (!entity.hasTransform) return;

            var evt = new SkillInputEvent(slot: slot, phase: SkillInputPhase.Press);
            _skills?.HandleInput(actorId, in evt);
        }

        private void HandleSkillInput(PlayerInputCommand cmd)
        {
            if (!_lobby.Started) return;
            if (!_playerActorMap.TryGetActorId(cmd.Player, out var actorId)) return;
            if (!TryGetEntity(actorId, out var entity) || entity == null) return;
            if (!entity.hasTransform) return;

            if (cmd.Payload == null || cmd.Payload.Length == 0) return;
            var evt = SkillInputCodec.Deserialize(cmd.Payload);
            _skills?.HandleInput(actorId, in evt);
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

        private bool TryGetEntity(int actorId, out global::ActorEntity entity)
        {
            if (_entities != null && _entities.TryGetActorEntity(actorId, out entity) && entity != null)
            {
                return true;
            }

            entity = null;
            return false;
        }
    }
}
