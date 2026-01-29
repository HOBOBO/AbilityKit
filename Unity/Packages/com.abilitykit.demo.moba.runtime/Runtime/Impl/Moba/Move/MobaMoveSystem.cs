using System;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Move
{
    public sealed class MobaMoveSystem : global::Entitas.IExecuteSystem
    {
        private readonly global::Entitas.IContexts _contexts;
        private readonly MobaLobbyStateService _lobby;
        private readonly MobaMoveService _moves;
        private readonly IWorldClock _clock;

        private readonly global::Entitas.IGroup<global::ActorEntity> _group;

        public MobaMoveSystem(global::Entitas.IContexts contexts, MobaLobbyStateService lobby, MobaMoveService moves, IWorldClock clock)
        {
            _contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));
            _lobby = lobby ?? throw new ArgumentNullException(nameof(lobby));
            _moves = moves ?? throw new ArgumentNullException(nameof(moves));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));

            var ctx = (global::Contexts)_contexts;
            _group = ctx.actor.GetGroup(ActorMatcher.AllOf(ActorComponentsLookup.ActorId, ActorComponentsLookup.Transform));
        }

        public void Execute()
        {
            if (!_lobby.Started) return;

            var dt = _clock.DeltaTime;
            if (dt <= 0f) return;

            var entities = _group.GetEntities();
            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                if (e == null) continue;

                var actorId = e.actorId.Value;
                var t = e.transform.Value;
                var p = t.Position;

                var delta = _moves.Tick(actorId, p, dt);
                if (delta.SqrMagnitude <= 0.0000001f) continue;

                var np = new Vec3(p.X + delta.X, p.Y + delta.Y, p.Z + delta.Z);
                e.ReplaceTransform(new Transform3(np, t.Rotation, t.Scale));
            }
        }
    }
}

