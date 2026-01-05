using System;
using System.Collections.Generic;
using Entitas;

namespace AbilityKit.Ability.Share.ECS.Entitas
{
    public sealed class EntitasActorIdLookup : IDisposable
    {
        private readonly IGroup<ActorEntity> _group;
        private readonly Dictionary<int, ActorEntity> _byActorId = new Dictionary<int, ActorEntity>();
        private bool _disposed;

        public EntitasActorIdLookup(ActorContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            _group = context.GetGroup(ActorMatcher.ActorId);

            var entities = _group.GetEntities();
            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                if (e == null || !e.hasActorId) continue;
                _byActorId[e.actorId.Value] = e;
            }

            _group.OnEntityAdded += OnEntityAdded;
            _group.OnEntityRemoved += OnEntityRemoved;
            _group.OnEntityUpdated += OnEntityUpdated;
        }

        public bool TryGet(int actorId, out ActorEntity entity)
        {
            if (actorId > 0 && _byActorId.TryGetValue(actorId, out var e) && e != null)
            {
                entity = e;
                return true;
            }

            entity = null;
            return false;
        }

        private void OnEntityAdded(IGroup<ActorEntity> group, ActorEntity entity, int index, IComponent component)
        {
            if (entity == null || !entity.hasActorId) return;
            _byActorId[entity.actorId.Value] = entity;
        }

        private void OnEntityRemoved(IGroup<ActorEntity> group, ActorEntity entity, int index, IComponent component)
        {
            if (entity == null || !entity.hasActorId) return;

            var id = entity.actorId.Value;
            if (_byActorId.TryGetValue(id, out var cached) && ReferenceEquals(cached, entity))
            {
                _byActorId.Remove(id);
            }
        }

        private void OnEntityUpdated(IGroup<ActorEntity> group, ActorEntity entity, int index, IComponent previousComponent, IComponent newComponent)
        {
            if (entity == null) return;

            if (index != ActorComponentsLookup.ActorId) return;

            if (previousComponent is AbilityKit.Ability.Impl.Moba.Conponents.ActorIdComponent prev)
            {
                if (_byActorId.TryGetValue(prev.Value, out var cached) && ReferenceEquals(cached, entity))
                {
                    _byActorId.Remove(prev.Value);
                }
            }

            if (newComponent is AbilityKit.Ability.Impl.Moba.Conponents.ActorIdComponent cur)
            {
                _byActorId[cur.Value] = entity;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _group.OnEntityAdded -= OnEntityAdded;
            _group.OnEntityRemoved -= OnEntityRemoved;
            _group.OnEntityUpdated -= OnEntityUpdated;

            _byActorId.Clear();
        }
    }
}
