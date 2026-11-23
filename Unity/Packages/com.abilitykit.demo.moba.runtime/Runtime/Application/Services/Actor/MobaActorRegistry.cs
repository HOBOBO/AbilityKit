using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services.Motion;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(MobaActorRegistry))]
    public sealed class MobaActorRegistry : IService
    {
        private readonly Dictionary<int, global::ActorEntity> _byId = new Dictionary<int, global::ActorEntity>();
        private readonly Dictionary<int, int> _entityVersions = new Dictionary<int, int>();
        private readonly Dictionary<int, global::ActorEntity> _lastEntityById = new Dictionary<int, global::ActorEntity>();
        private readonly Dictionary<int, (int Epoch, long Order, int Version, int CreationIndex)> _summonBirths =
            new Dictionary<int, (int, long, int, int)>();
        private readonly Dictionary<int, RetainedActor> _retained = new Dictionary<int, RetainedActor>();
        private readonly HashSet<global::ActorEntity> _retainedShells = new HashSet<global::ActorEntity>();
        private readonly List<int> _reclaim = new List<int>();
        private global::ActorContext _holdingContext;

        public IEnumerable<KeyValuePair<int, global::ActorEntity>> Entries => _byId;

        private readonly List<int> _orderedIdScratch = new List<int>(16);

        /// <summary>
        /// 按 ActorId 升序填充 id 列表（复用内部 scratch，消费方须在下一调用前用完）。
        /// 快照/投影/跨网络输出必须按此序遍历：字典遍历序跨运行时不保证一致，
        /// 按 id 定序保证两端字节序一致。
        /// </summary>
        public List<int> CopyActorIdsInOrder()
        {
            _orderedIdScratch.Clear();
            _orderedIdScratch.AddRange(_byId.Keys);
            _orderedIdScratch.Sort();
            return _orderedIdScratch;
        }

        public void Register(int actorId, global::ActorEntity entity)
        {
            if (actorId <= 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (_retained.ContainsKey(actorId))
                throw new InvalidOperationException($"Actor {actorId} is still retained for recovery.");

            if (!_lastEntityById.TryGetValue(actorId, out var previous) ||
                !ReferenceEquals(previous, entity))
            {
                _entityVersions.TryGetValue(actorId, out var version);
                _entityVersions[actorId] = version == int.MaxValue ? 1 : version + 1;
                _lastEntityById[actorId] = entity;
                _summonBirths.Remove(actorId);
            }
            _byId[actorId] = entity;
        }

        public int GetEntityVersion(int actorId)
        {
            return actorId > 0 && _entityVersions.TryGetValue(actorId, out var version)
                ? version
                : 0;
        }

        public bool MatchesEntityVersion(int actorId, int entityVersion)
        {
            return entityVersion > 0 && GetEntityVersion(actorId) == entityVersion;
        }

        internal void RecordSummonBirth(int actorId, global::ActorEntity entity, int epoch, long order) =>
            _summonBirths[actorId] = (epoch, order, GetEntityVersion(actorId), entity.creationIndex);

        internal bool MatchesSummonBirth(int actorId, global::ActorEntity entity, int epoch, long order) =>
            _summonBirths.TryGetValue(actorId, out var birth) && birth.Epoch == epoch && birth.Order == order &&
            birth.CreationIndex == entity.creationIndex && MatchesEntityVersion(actorId, birth.Version) &&
            _lastEntityById.TryGetValue(actorId, out var last) && ReferenceEquals(last, entity);

        internal void RegisterRestored(int actorId, global::ActorEntity entity, int entityVersion)
        {
            if (actorId <= 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (entityVersion <= 0) throw new ArgumentOutOfRangeException(nameof(entityVersion));

            if (!MatchesEntityVersion(actorId, entityVersion) ||
                !_lastEntityById.TryGetValue(actorId, out var last) || !ReferenceEquals(last, entity))
                _summonBirths.Remove(actorId);
            _entityVersions[actorId] = entityVersion;
            _lastEntityById[actorId] = entity;
            _byId[actorId] = entity;
        }

        internal void ForgetEntityIdentity(int actorId)
        {
            if (actorId <= 0) return;
            _entityVersions.Remove(actorId);
            _lastEntityById.Remove(actorId);
            _summonBirths.Remove(actorId);
        }

        public bool Contains(int actorId)
        {
            return actorId > 0 && _byId.ContainsKey(actorId);
        }

        public bool TryGet(int actorId, out global::ActorEntity entity)
        {
            if (TryGetRegistered(actorId, out entity) && entity.isEnabled)
            {
                return true;
            }

            entity = null;
            return false;
        }

        internal bool TryGetRegistered(int actorId, out global::ActorEntity entity)
        {
            entity = null;
            return actorId > 0 &&
                   _byId.TryGetValue(actorId, out entity) &&
                   entity != null;
        }

        public void Unregister(int actorId)
        {
            if (actorId <= 0) return;
            _byId.Remove(actorId);
        }

        internal bool TryGetRetainedForRecovery(int actorId, int frame, out global::ActorEntity componentOwner)
            => TryGetRetainedForRecovery(actorId, frame, out componentOwner, out _);

        internal bool TryGetRetainedForRecovery(int actorId, int frame, out global::ActorEntity componentOwner,
            out global::ActorEntity originalEntity)
        {
            componentOwner = null;
            originalEntity = null;
            if (!_retained.TryGetValue(actorId, out var retained) || frame >= retained.ExitFrame ||
                !retained.Entity.isEnabled || retained.Entity.creationIndex != retained.CreationIndex ||
                !MatchesEntityVersion(actorId, retained.Version) || !_lastEntityById.TryGetValue(actorId, out var original) ||
                !ReferenceEquals(original, retained.Entity)) return false;
            componentOwner = retained.Components;
            originalEntity = retained.Entity;
            return true;
        }

        internal bool IsRetainedShell(global::ActorEntity entity) => _retainedShells.Contains(entity);

        internal void RetainExpiredActor(int actorId, global::ActorEntity entity, int exitFrame)
        {
            if (Contains(actorId) || GetEntityVersion(actorId) <= 0 || !entity.isEnabled ||
                !entity.hasActorId || entity.actorId.Value != actorId || !entity.hasSummonMeta ||
                !_lastEntityById.TryGetValue(actorId, out var original) || !ReferenceEquals(original, entity))
                throw new InvalidOperationException($"Actor {actorId} must be detached from gameplay before retention.");

            _holdingContext ??= new global::ActorContext();
            var held = _holdingContext.CreateEntity();
            MoveComponents(entity, held);
            entity.AddActorDespawnRequest(exitFrame, exitFrame, ActorDespawnReason.SummonTimeout, 0, 0);
            _retained.Add(actorId, new RetainedActor(entity, held, GetEntityVersion(actorId), exitFrame));
            _retainedShells.Add(entity);
        }

        internal global::ActorEntity ReattachExpiredActor(int actorId, int frame, int version)
        {
            if (Contains(actorId) || !MatchesEntityVersion(actorId, version) ||
                !TryGetRetainedForRecovery(actorId, frame, out var held))
                throw new InvalidOperationException($"Actor {actorId} no longer owns the retained identity.");

            var entity = _retained[actorId].Entity;
            entity.RemoveActorDespawnRequest();
            MoveComponents(held, entity);
            held.Destroy();
            _retained.Remove(actorId);
            _retainedShells.Remove(entity);
            RegisterRestored(actorId, entity, version);
            return entity;
        }

        internal void ReclaimExpiredActors(int floor, MobaBrainService brains, MobaLocomotionRuntimeService locomotion)
        {
            _reclaim.Clear();
            foreach (var entry in _retained)
                if (entry.Value.ExitFrame <= floor) _reclaim.Add(entry.Key);
            _reclaim.Sort();
            foreach (var actorId in _reclaim)
            {
                var retained = _retained[actorId];
                _retained.Remove(actorId);
                _retainedShells.Remove(retained.Entity);
                _lastEntityById.Remove(actorId);
                _summonBirths.Remove(actorId);
                locomotion?.ForgetPipelineForRollback(retained.Entity);
                brains.ReleaseExpiredBrain(retained.Components);
                ReleaseComponents(retained.Components);
                retained.Entity.Destroy();
            }
            if (_reclaim.Count > 0) _holdingContext.ClearComponentPools();
            _reclaim.Clear();
        }

        private static void ReleaseComponents(global::ActorEntity entity)
        {
            if (entity.hasCharacterHfsm) entity.RemoveAndDisposeCharacterHfsm();
            entity.Destroy();
        }

        private static void MoveComponents(global::ActorEntity from, global::ActorEntity to)
        {
            /*
             * 转移原组件，不复制权威值。逐个从原池取回，避免同一个引用同时归属活动实体与组件池。
             */
            foreach (var index in from.GetComponentIndices())
            {
                var component = from.GetComponent(index);
                var pool = from.GetComponentPool(index);
                from.RemoveComponent(index);
                if (pool.Count == 0 || !ReferenceEquals(pool.Peek(), component))
                    throw new InvalidOperationException("Entitas did not return the detached component to the expected pool.");
                pool.Pop();
                to.AddComponent(index, component);
            }
        }

        private sealed class RetainedActor
        {
            internal readonly global::ActorEntity Entity;
            internal readonly global::ActorEntity Components;
            internal readonly int CreationIndex;
            internal readonly int Version;
            internal readonly int ExitFrame;

            internal RetainedActor(global::ActorEntity entity, global::ActorEntity components, int version, int exitFrame)
            {
                Entity = entity;
                Components = components;
                CreationIndex = entity.creationIndex;
                Version = version;
                ExitFrame = exitFrame;
            }
        }

        public void Clear()
        {
            foreach (var retained in _retained.Values)
            {
                if (retained.Entity.isEnabled && retained.Entity.creationIndex == retained.CreationIndex)
                    retained.Entity.Destroy();
                if (retained.Components.isEnabled) ReleaseComponents(retained.Components);
            }
            _retained.Clear();
            _retainedShells.Clear();
            _holdingContext = null;
            _byId.Clear();
            _entityVersions.Clear();
            _lastEntityById.Clear();
            _summonBirths.Clear();
        }

        public void Dispose()
        {
            Clear();
        }
    }
}
