#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Demo.Shooter.View
{
    public sealed class ShooterSnapshotViewProjection
    {
        private readonly ShooterViewEntityStore _store;
        private readonly HashSet<ShooterViewEntityKey> _presentEntities = new HashSet<ShooterViewEntityKey>();
        private readonly List<ShooterViewEntityKey> _staleEntities = new List<ShooterViewEntityKey>();

        public ShooterSnapshotViewProjection()
            : this(new ShooterViewEntityStore())
        {
        }

        public ShooterSnapshotViewProjection(ShooterViewEntityStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public ShooterViewEntityStore Store => _store;

        public ShooterViewProjectionApplyResult LastApplyResult { get; private set; } = ShooterViewProjectionApplyResult.Empty;

        public void Clear()
        {
            _store.Clear();
            _presentEntities.Clear();
            _staleEntities.Clear();
            LastApplyResult = ShooterViewProjectionApplyResult.Empty;
        }
 
        public ShooterViewProjectionApplyResult Apply(in ShooterSnapshotViewBatch batch)
        {
            var missingEntityRemovals = 0;
            if (ShouldReplaceMissingEntities(in batch))
            {
                missingEntityRemovals = RemoveEntitiesMissingFromFullSnapshot(in batch);
            }
 
            var explicitEntityRemovals = ApplyRemovedEntities(in batch);
            var entityChangeResult = ApplyEntityChanges(in batch);
            var recoveredPlayerEntities = RecoverMissingPlayerEntitiesFromComponents(in batch);
            var componentUpdates =
                ApplyTransformChanges(in batch) +
                ApplyHealthChanges(in batch) +
                ApplyScoreChanges(in batch) +
                ApplyProjectileLifetimeChanges(in batch);

            LastApplyResult = new ShooterViewProjectionApplyResult(
                batch.WorldId,
                batch.Frame,
                batch.Sequence,
                batch.SnapshotKind,
                batch.Source,
                entityChangeResult.AddedEntities + recoveredPlayerEntities,
                entityChangeResult.UpdatedEntities,
                missingEntityRemovals + explicitEntityRemovals + entityChangeResult.DeadEntityRemovals,
                missingEntityRemovals,
                explicitEntityRemovals,
                entityChangeResult.DeadEntityRemovals,
                componentUpdates,
                _store.EntityCount,
                _store.PlayerCount,
                _store.BulletCount);
            return LastApplyResult;
        }
 
        public bool HasEntity(ShooterViewEntityKey key)
        {
            return _store.ContainsEntity(key);
        }

        public bool TryGetEntity(ShooterViewEntityKey key, out ShooterViewEntityState state)
        {
            return _store.TryGetEntity(key, out state);
        }

        private static bool ShouldReplaceMissingEntities(in ShooterSnapshotViewBatch batch)
        {
            return batch.ShouldReplaceMissingEntities;
        }

        private int RemoveEntitiesMissingFromFullSnapshot(in ShooterSnapshotViewBatch batch)
        {
            _presentEntities.Clear();
            var entityChanges = batch.EntityChanges;
            _presentEntities.EnsureCapacity(entityChanges.Count);
            for (int i = 0; i < entityChanges.Count; i++)
            {
                var change = entityChanges[i];
                if (change.Alive)
                {
                    _presentEntities.Add(change.Key);
                }
            }

            _staleEntities.Clear();
            if (_store.EntityCount > _staleEntities.Capacity)
            {
                _staleEntities.Capacity = _store.EntityCount;
            }

            foreach (var key in _store.Entities.Keys)
            {
                if (!_presentEntities.Contains(key))
                {
                    _staleEntities.Add(key);
                }
            }

            var removedCount = 0;
            for (int i = 0; i < _staleEntities.Count; i++)
            {
                if (_store.RemoveEntity(_staleEntities[i]))
                {
                    removedCount++;
                }
            }

            return removedCount;
        }
 
        private int ApplyRemovedEntities(in ShooterSnapshotViewBatch batch)
        {
            var removedCount = 0;
            var removed = batch.RemovedEntities;
            for (int i = 0; i < removed.Count; i++)
            {
                if (_store.RemoveEntity(removed[i]))
                {
                    removedCount++;
                }
            }

            return removedCount;
        }
 
        private EntityChangeApplyResult ApplyEntityChanges(in ShooterSnapshotViewBatch batch)
        {
            var added = 0;
            var updated = 0;
            var deadRemovals = 0;
            var changes = batch.EntityChanges;
            for (int i = 0; i < changes.Count; i++)
            {
                var change = changes[i];
                var existed = _store.UpsertEntity(change);
                if (!change.Alive)
                {
                    if (existed)
                    {
                        deadRemovals++;
                    }
                }
                else if (existed)
                {
                    updated++;
                }
                else
                {
                    added++;
                }
            }

            return new EntityChangeApplyResult(added, updated, deadRemovals);
        }
 
        private int RecoverMissingPlayerEntitiesFromComponents(in ShooterSnapshotViewBatch batch)
        {
            if (ShouldReplaceMissingEntities(in batch))
            {
                return 0;
            }

            var added = 0;
            var transforms = batch.TransformChanges;
            for (int i = 0; i < transforms.Count; i++)
            {
                if (TryRecoverMissingPlayerEntity(transforms[i].Key))
                {
                    added++;
                }
            }

            var health = batch.HealthChanges;
            for (int i = 0; i < health.Count; i++)
            {
                if (TryRecoverMissingPlayerEntity(health[i].Key))
                {
                    added++;
                }
            }

            var scores = batch.ScoreChanges;
            for (int i = 0; i < scores.Count; i++)
            {
                if (TryRecoverMissingPlayerEntity(scores[i].Key))
                {
                    added++;
                }
            }

            return added;
        }

        private bool TryRecoverMissingPlayerEntity(ShooterViewEntityKey key)
        {
            if (key.Kind != ShooterViewEntityKind.Player)
            {
                return false;
            }

            return !_store.UpsertEntity(new ShooterViewEntityChange(key, ownerEntityId: 0, alive: true));
        }

        private int ApplyTransformChanges(in ShooterSnapshotViewBatch batch)
        {
            var applied = 0;
            var changes = batch.TransformChanges;
            for (int i = 0; i < changes.Count; i++)
            {
                if (_store.UpsertTransform(changes[i])) applied++;
            }

            return applied;
        }
 
        private int ApplyHealthChanges(in ShooterSnapshotViewBatch batch)
        {
            var applied = 0;
            var changes = batch.HealthChanges;
            for (int i = 0; i < changes.Count; i++)
            {
                if (_store.UpsertHealth(changes[i])) applied++;
            }

            return applied;
        }
 
        private int ApplyScoreChanges(in ShooterSnapshotViewBatch batch)
        {
            var applied = 0;
            var changes = batch.ScoreChanges;
            for (int i = 0; i < changes.Count; i++)
            {
                if (_store.UpsertScore(changes[i])) applied++;
            }

            return applied;
        }
 
        private int ApplyProjectileLifetimeChanges(in ShooterSnapshotViewBatch batch)
        {
            var applied = 0;
            var changes = batch.ProjectileLifetimeChanges;
            for (int i = 0; i < changes.Count; i++)
            {
                if (_store.UpsertProjectileLifetime(changes[i])) applied++;
            }

            return applied;
        }

        private readonly struct EntityChangeApplyResult
        {
            public EntityChangeApplyResult(int addedEntities, int updatedEntities, int deadEntityRemovals)
            {
                AddedEntities = addedEntities;
                UpdatedEntities = updatedEntities;
                DeadEntityRemovals = deadEntityRemovals;
            }

            public int AddedEntities { get; }

            public int UpdatedEntities { get; }

            public int DeadEntityRemovals { get; }
        }
    }

    public readonly struct ShooterViewProjectionApplyResult
    {
        public static readonly ShooterViewProjectionApplyResult Empty = new ShooterViewProjectionApplyResult(
            0UL,
            0,
            0UL,
            ShooterViewSnapshotKind.Full,
            ShooterViewBatchSource.DebugSnapshot,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0);

        public ShooterViewProjectionApplyResult(
            ulong worldId,
            int frame,
            ulong sequence,
            ShooterViewSnapshotKind snapshotKind,
            ShooterViewBatchSource source,
            int addedEntities,
            int updatedEntities,
            int removedEntities,
            int missingEntityRemovals,
            int explicitEntityRemovals,
            int deadEntityRemovals,
            int componentUpdates,
            int finalEntityCount,
            int finalPlayerCount,
            int finalBulletCount)
        {
            WorldId = worldId;
            Frame = frame;
            Sequence = sequence;
            SnapshotKind = snapshotKind;
            Source = source;
            AddedEntities = addedEntities;
            UpdatedEntities = updatedEntities;
            RemovedEntities = removedEntities;
            MissingEntityRemovals = missingEntityRemovals;
            ExplicitEntityRemovals = explicitEntityRemovals;
            DeadEntityRemovals = deadEntityRemovals;
            ComponentUpdates = componentUpdates;
            FinalEntityCount = finalEntityCount;
            FinalPlayerCount = finalPlayerCount;
            FinalBulletCount = finalBulletCount;
        }

        public ulong WorldId { get; }

        public int Frame { get; }

        public ulong Sequence { get; }

        public ShooterViewSnapshotKind SnapshotKind { get; }

        public ShooterViewBatchSource Source { get; }

        public int AddedEntities { get; }

        public int UpdatedEntities { get; }

        public int RemovedEntities { get; }

        public int MissingEntityRemovals { get; }

        public int ExplicitEntityRemovals { get; }

        public int DeadEntityRemovals { get; }

        public int ComponentUpdates { get; }

        public int FinalEntityCount { get; }

        public int FinalPlayerCount { get; }

        public int FinalBulletCount { get; }
    }
}
