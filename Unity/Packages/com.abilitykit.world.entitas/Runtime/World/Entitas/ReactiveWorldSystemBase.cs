using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using Entitas;

namespace AbilityKit.Ability.World.Entitas
{
    public abstract class ReactiveWorldSystemBase<TEntity> : WorldSystemBase where TEntity : class, IEntity
    {
        private readonly HashSet<TEntity> _pending = new HashSet<TEntity>();
        private readonly Dictionary<TEntity, EntityComponentReplaced> _replaceHandlers = new Dictionary<TEntity, EntityComponentReplaced>();

        private IGroup<TEntity> _group;

        protected IGroup<TEntity> Group => _group;

        protected ReactiveWorldSystemBase(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
        }

        protected abstract IGroup<TEntity> CreateGroup(global::Entitas.IContexts contexts);

        protected abstract bool ShouldReactToReplace(int componentIndex);

        protected abstract void OnEntityChanged(TEntity entity);

        protected abstract void OnEntityRemovedFromGroup(TEntity entity);

        protected virtual bool EnableGroupRemoveDebugLog => false;

        protected virtual void OnGroupRemoveDebug(IGroup<TEntity> group, TEntity entity, int index, IComponent component)
        {
        }

        protected override void OnInit()
        {
            _group = CreateGroup(Contexts);
            if (_group == null) return;

            _group.OnEntityAdded += OnGroupEntityAdded;
            _group.OnEntityRemoved += OnGroupEntityRemoved;

            var existing = _group.GetEntities();
            if (existing != null)
            {
                for (int i = 0; i < existing.Length; i++)
                {
                    Track(existing[i]);
                    Enqueue(existing[i]);
                }
            }
        }

        protected override void OnExecute()
        {
            if (_pending.Count == 0) return;

            var list = new List<TEntity>(_pending);
            _pending.Clear();

            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e == null) continue;
                OnEntityChanged(e);
            }
        }

        protected override void OnCleanup()
        {
            // IMPORTANT: Entitas Cleanup() is called every tick.
            // Reactive wiring must be released in TearDown, not per-tick Cleanup.
        }

        protected override void OnTearDown()
        {
            if (_group != null)
            {
                _group.OnEntityAdded -= OnGroupEntityAdded;
                _group.OnEntityRemoved -= OnGroupEntityRemoved;

                var entities = _group.GetEntities();
                if (entities != null)
                {
                    for (int i = 0; i < entities.Length; i++)
                    {
                        Untrack(entities[i]);
                    }
                }
            }

            _pending.Clear();
            _replaceHandlers.Clear();
        }

        private void OnGroupEntityAdded(IGroup<TEntity> group, TEntity entity, int index, IComponent component)
        {
            Track(entity);
            Enqueue(entity);
        }

        private void OnGroupEntityRemoved(IGroup<TEntity> group, TEntity entity, int index, IComponent component)
        {
            _pending.Remove(entity);
            Untrack(entity);

            if (EnableGroupRemoveDebugLog)
            {
                try
                {
                    OnGroupRemoveDebug(group, entity, index, component);
                }
                catch
                {
                }
            }
            OnEntityRemovedFromGroup(entity);
        }

        private void Track(TEntity entity)
        {
            if (entity == null) return;
            if (_replaceHandlers.ContainsKey(entity)) return;

            EntityComponentReplaced handler = (e, componentIndex, previousComponent, newComponent) =>
            {
                if (!ShouldReactToReplace(componentIndex)) return;
                Enqueue((TEntity)e);
            };

            if (entity is Entity ent)
            {
                ent.OnComponentReplaced += handler;
                _replaceHandlers[entity] = handler;
            }
        }

        private void Untrack(TEntity entity)
        {
            if (entity == null) return;
            if (!_replaceHandlers.TryGetValue(entity, out var handler)) return;

            if (entity is Entity ent)
            {
                ent.OnComponentReplaced -= handler;
            }

            _replaceHandlers.Remove(entity);
        }

        private void Enqueue(TEntity entity)
        {
            if (entity == null) return;
            _pending.Add(entity);
        }
    }
}
