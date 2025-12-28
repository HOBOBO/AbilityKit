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
        private readonly MobaPlayerActorMapService _playerActorMap;
        private readonly MobaActorLookupService _actorLookup;
        private readonly global::Contexts _contexts;

        private readonly Dictionary<int, Action<PlayerInputCommand>> _handlers;

        public MobaLobbyInputSink(MobaLobbyStateService lobby, MobaEnterGameFlowService enterGame, MobaPlayerActorMapService playerActorMap, MobaActorLookupService actorLookup, global::Contexts contexts)
        {
            _lobby = lobby ?? throw new ArgumentNullException(nameof(lobby));
            _enterGame = enterGame ?? throw new ArgumentNullException(nameof(enterGame));
            _playerActorMap = playerActorMap ?? throw new ArgumentNullException(nameof(playerActorMap));
            _actorLookup = actorLookup ?? throw new ArgumentNullException(nameof(actorLookup));
            _contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));

            _handlers = new Dictionary<int, Action<PlayerInputCommand>>
            {
                { (int)MobaOpCode.Ready, cmd => _lobby.SetReady(cmd.Player, true) },
                { (int)MobaOpCode.Unready, cmd => _lobby.SetReady(cmd.Player, false) },
                { (int)MobaOpCode.Move, HandleMove },
            };
        }

        private void HandleMove(PlayerInputCommand cmd)
        {
            if (!_lobby.Started) return;
            if (!_playerActorMap.TryGetActorId(cmd.Player, out var actorId)) return;
            if (!_actorLookup.TryGetActorEntity(actorId, out var entity) || entity == null) return;
            if (!entity.hasTransform) return;

            MobaMoveCodec.Deserialize(cmd.Payload, out var dx, out var dz);
            if (AbilityKit.Ability.Share.Math.MathUtil.Abs(dx) < 0.0001f && AbilityKit.Ability.Share.Math.MathUtil.Abs(dz) < 0.0001f) return;

            const float step = 0.15f;
            var t = entity.transform.Value;
            var p = t.Position;
            var np = new AbilityKit.Ability.Share.Math.Vec3(p.X + dx * step, p.Y, p.Z + dz * step);
            var nt = new AbilityKit.Ability.Share.Math.Transform3(np, t.Rotation, t.Scale);
            entity.ReplaceTransform(nt);
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
