#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Demo.Shooter.View
{
    public sealed class ShooterViewEntityStore
    {
        private readonly Dictionary<ShooterViewEntityKey, ShooterViewEntityState> _entities = new Dictionary<ShooterViewEntityKey, ShooterViewEntityState>();
        private readonly Dictionary<ShooterViewEntityKey, ShooterViewTransformState> _transforms = new Dictionary<ShooterViewEntityKey, ShooterViewTransformState>();
        private readonly Dictionary<ShooterViewEntityKey, ShooterViewHealthState> _health = new Dictionary<ShooterViewEntityKey, ShooterViewHealthState>();
        private readonly Dictionary<ShooterViewEntityKey, ShooterViewScoreState> _scores = new Dictionary<ShooterViewEntityKey, ShooterViewScoreState>();
        private readonly Dictionary<ShooterViewEntityKey, ShooterViewProjectileLifetimeState> _projectileLifetimes = new Dictionary<ShooterViewEntityKey, ShooterViewProjectileLifetimeState>();
        private ShooterViewEntityState[] _denseEntities = Array.Empty<ShooterViewEntityState>();
        private ShooterViewTransformState[] _denseTransforms = Array.Empty<ShooterViewTransformState>();
        private bool[] _denseHasTransform = Array.Empty<bool>();
        private int _denseCount;
        private int _playerCount;
        private int _bulletCount;
        private int _enemyCount;

        public IReadOnlyDictionary<ShooterViewEntityKey, ShooterViewEntityState> Entities => _entities;

        public IReadOnlyDictionary<ShooterViewEntityKey, ShooterViewTransformState> Transforms => _transforms;

        public IReadOnlyDictionary<ShooterViewEntityKey, ShooterViewHealthState> Health => _health;

        public IReadOnlyDictionary<ShooterViewEntityKey, ShooterViewScoreState> Scores => _scores;

        public IReadOnlyDictionary<ShooterViewEntityKey, ShooterViewProjectileLifetimeState> ProjectileLifetimes => _projectileLifetimes;

        public int EntityCount => _entities.Count;

        public int PlayerCount => _playerCount;

        public int BulletCount => _bulletCount;

        public int EnemyCount => _enemyCount;

        public int DenseCount => _denseCount;

        public void EnsureCapacity(int capacity)
        {
            if (capacity <= 0)
            {
                return;
            }

            _entities.EnsureCapacity(capacity);
            _transforms.EnsureCapacity(capacity);
            _health.EnsureCapacity(capacity);
            _scores.EnsureCapacity(capacity);
            _projectileLifetimes.EnsureCapacity(capacity);
            EnsureDenseCapacity(capacity);
        }

        public void EnsureCapacityForEntityChanges(IReadOnlyList<ShooterViewEntityChange> changes)
        {
            if (changes == null || changes.Count == 0)
            {
                return;
            }

            var upperBound = checked(_entities.Count + changes.Count);
            if (upperBound <= _entities.EnsureCapacity(0))
            {
                return;
            }

            var additions = 0;
            for (var i = 0; i < changes.Count; i++)
            {
                var change = changes[i];
                if (change.Alive && !_entities.ContainsKey(change.Key))
                {
                    additions++;
                }
            }

            EnsureCapacity(checked(_entities.Count + additions));
        }

        public void Clear()
        {
            _entities.Clear();
            _transforms.Clear();
            _health.Clear();
            _scores.Clear();
            _projectileLifetimes.Clear();
            Array.Clear(_denseEntities, 0, _denseCount);
            Array.Clear(_denseTransforms, 0, _denseCount);
            Array.Clear(_denseHasTransform, 0, _denseCount);
            _denseCount = 0;
            _playerCount = 0;
            _bulletCount = 0;
            _enemyCount = 0;
        }

        public bool UpsertEntity(in ShooterViewEntityChange change)
        {
            if (!change.Alive)
            {
                return RemoveEntity(change.Key);
            }

            var existed = _entities.TryGetValue(change.Key, out var existing);
            if (existed &&
                existing.OwnerEntityId == change.OwnerEntityId &&
                existing.Alive == change.Alive)
            {
                return true;
            }

            var denseIndex = existed ? existing.DenseIndex : _denseCount++;
            if (!existed)
            {
                EnsureDenseCapacity(_denseCount);
                IncrementKindCount(change.Key.Kind);
            }

            var entity = new ShooterViewEntityState(change.Key, change.OwnerEntityId, change.Alive, denseIndex);
            _entities[change.Key] = entity;
            _denseEntities[denseIndex] = entity;
            return existed;
        }

        public bool RemoveEntity(ShooterViewEntityKey key)
        {
            if (!_entities.TryGetValue(key, out var removedEntity))
            {
                return false;
            }

            _entities.Remove(key);
            DecrementKindCount(key.Kind);
            var removedIndex = removedEntity.DenseIndex;
            var lastIndex = --_denseCount;
            if (removedIndex != lastIndex)
            {
                var movedEntity = _denseEntities[lastIndex].WithDenseIndex(removedIndex);
                _denseEntities[removedIndex] = movedEntity;
                _denseTransforms[removedIndex] = _denseTransforms[lastIndex];
                _denseHasTransform[removedIndex] = _denseHasTransform[lastIndex];
                _entities[movedEntity.Key] = movedEntity;
            }

            _denseEntities[lastIndex] = default;
            _denseTransforms[lastIndex] = default;
            _denseHasTransform[lastIndex] = false;

            _transforms.Remove(key);
            _health.Remove(key);
            _scores.Remove(key);
            _projectileLifetimes.Remove(key);
            return true;
        }

        public bool UpsertTransform(in ShooterViewTransformComponentChange change)
        {
            if (!_entities.TryGetValue(change.Key, out var entity))
            {
                return false;
            }

            return UpsertTransform(in change, in entity);
        }

        internal bool UpsertEntityAndTransform(
            in ShooterViewEntityChange entityChange,
            in ShooterViewTransformComponentChange transformChange,
            out bool transformApplied)
        {
            if (!entityChange.Key.Equals(transformChange.Key) || !entityChange.Alive)
            {
                throw new ArgumentException("Fused entity/transform updates require the same live entity key.");
            }

            var existed = _entities.TryGetValue(entityChange.Key, out var entity);
            if (!existed)
            {
                var denseIndex = _denseCount++;
                EnsureDenseCapacity(_denseCount);
                IncrementKindCount(entityChange.Kind);
                entity = new ShooterViewEntityState(entityChange.Key, entityChange.OwnerEntityId, alive: true, denseIndex);
                _entities.Add(entityChange.Key, entity);
                _denseEntities[denseIndex] = entity;
            }
            else if (entity.OwnerEntityId != entityChange.OwnerEntityId || entity.Alive != entityChange.Alive)
            {
                entity = new ShooterViewEntityState(entityChange.Key, entityChange.OwnerEntityId, entityChange.Alive, entity.DenseIndex);
                _entities[entityChange.Key] = entity;
                _denseEntities[entity.DenseIndex] = entity;
            }

            transformApplied = UpsertTransform(in transformChange, in entity);
            return existed;
        }

        private bool UpsertTransform(
            in ShooterViewTransformComponentChange change,
            in ShooterViewEntityState entity)
        {

            var transform = new ShooterViewTransformState(
                change.Key,
                change.X,
                change.Y,
                change.FacingX,
                change.FacingY,
                change.VelocityX,
                change.VelocityY);
            if (_denseHasTransform[entity.DenseIndex] && TransformEquals(_denseTransforms[entity.DenseIndex], transform))
            {
                return true;
            }

            _transforms[change.Key] = transform;
            _denseTransforms[entity.DenseIndex] = transform;
            _denseHasTransform[entity.DenseIndex] = true;
            return true;
        }

        public bool UpsertHealth(in ShooterViewHealthComponentChange change)
        {
            if (!_entities.ContainsKey(change.Key))
            {
                return false;
            }

            if (_health.TryGetValue(change.Key, out var existing) && existing.Hp == change.Hp)
            {
                return true;
            }

            _health[change.Key] = new ShooterViewHealthState(change.Key, change.Hp);
            return true;
        }

        public bool UpsertScore(in ShooterViewScoreComponentChange change)
        {
            if (!_entities.ContainsKey(change.Key))
            {
                return false;
            }

            if (_scores.TryGetValue(change.Key, out var existing) && existing.Score == change.Score)
            {
                return true;
            }

            _scores[change.Key] = new ShooterViewScoreState(change.Key, change.Score);
            return true;
        }

        public bool UpsertProjectileLifetime(in ShooterViewProjectileLifetimeComponentChange change)
        {
            if (!_entities.ContainsKey(change.Key))
            {
                return false;
            }

            if (_projectileLifetimes.TryGetValue(change.Key, out var existing) &&
                existing.RemainingFrames == change.RemainingFrames)
            {
                return true;
            }

            _projectileLifetimes[change.Key] = new ShooterViewProjectileLifetimeState(change.Key, change.RemainingFrames);
            return true;
        }

        public bool ContainsEntity(ShooterViewEntityKey key)
        {
            return _entities.ContainsKey(key);
        }

        public bool TryGetEntity(ShooterViewEntityKey key, out ShooterViewEntityState state)
        {
            return _entities.TryGetValue(key, out state);
        }

        public bool TryGetTransform(ShooterViewEntityKey key, out ShooterViewTransformState state)
        {
            return _transforms.TryGetValue(key, out state);
        }

        public bool TryGetHealth(ShooterViewEntityKey key, out ShooterViewHealthState state)
        {
            return _health.TryGetValue(key, out state);
        }

        public bool TryGetScore(ShooterViewEntityKey key, out ShooterViewScoreState state)
        {
            return _scores.TryGetValue(key, out state);
        }

        public bool TryGetProjectileLifetime(ShooterViewEntityKey key, out ShooterViewProjectileLifetimeState state)
        {
            return _projectileLifetimes.TryGetValue(key, out state);
        }

        public bool TryGetDenseEntityAndTransform(
            int index,
            out ShooterViewEntityState entity,
            out ShooterViewTransformState transform)
        {
            if ((uint)index >= (uint)_denseCount)
            {
                entity = default;
                transform = default;
                return false;
            }

            entity = _denseEntities[index];
            if (!_denseHasTransform[index])
            {
                transform = default;
                return false;
            }

            transform = _denseTransforms[index];
            return true;
        }

        private void EnsureDenseCapacity(int capacity)
        {
            if (_denseEntities.Length >= capacity)
            {
                return;
            }

            var newCapacity = _denseEntities.Length == 0 ? 16 : _denseEntities.Length;
            while (newCapacity < capacity)
            {
                newCapacity = checked(newCapacity * 2);
            }

            Array.Resize(ref _denseEntities, newCapacity);
            Array.Resize(ref _denseTransforms, newCapacity);
            Array.Resize(ref _denseHasTransform, newCapacity);
        }

        private static bool TransformEquals(in ShooterViewTransformState left, in ShooterViewTransformState right)
        {
            return left.X.Equals(right.X) &&
                left.Y.Equals(right.Y) &&
                left.FacingX.Equals(right.FacingX) &&
                left.FacingY.Equals(right.FacingY) &&
                left.VelocityX.Equals(right.VelocityX) &&
                left.VelocityY.Equals(right.VelocityY);
        }

        private void IncrementKindCount(ShooterViewEntityKind kind)
        {
            switch (kind)
            {
                case ShooterViewEntityKind.Player:
                    _playerCount++;
                    break;
                case ShooterViewEntityKind.Bullet:
                    _bulletCount++;
                    break;
                case ShooterViewEntityKind.Enemy:
                    _enemyCount++;
                    break;
            }
        }

        private void DecrementKindCount(ShooterViewEntityKind kind)
        {
            switch (kind)
            {
                case ShooterViewEntityKind.Player:
                    _playerCount = Math.Max(0, _playerCount - 1);
                    break;
                case ShooterViewEntityKind.Bullet:
                    _bulletCount = Math.Max(0, _bulletCount - 1);
                    break;
                case ShooterViewEntityKind.Enemy:
                    _enemyCount = Math.Max(0, _enemyCount - 1);
                    break;
            }
        }
    }

    public readonly struct ShooterViewEntityState
    {
        public ShooterViewEntityState(ShooterViewEntityKey key, int ownerEntityId, bool alive)
            : this(key, ownerEntityId, alive, denseIndex: -1)
        {
        }

        internal ShooterViewEntityState(ShooterViewEntityKey key, int ownerEntityId, bool alive, int denseIndex)
        {
            Key = key;
            OwnerEntityId = ownerEntityId;
            Alive = alive;
            DenseIndex = denseIndex;
        }

        public ShooterViewEntityKey Key { get; }

        public ShooterViewEntityKind Kind => Key.Kind;

        public int EntityId => Key.EntityId;

        public int OwnerEntityId { get; }

        public bool Alive { get; }

        internal int DenseIndex { get; }

        internal ShooterViewEntityState WithDenseIndex(int denseIndex)
        {
            return new ShooterViewEntityState(Key, OwnerEntityId, Alive, denseIndex);
        }
    }

    public readonly struct ShooterViewTransformState
    {
        public ShooterViewTransformState(
            ShooterViewEntityKey key,
            float x,
            float y,
            float facingX,
            float facingY,
            float velocityX,
            float velocityY)
        {
            Key = key;
            X = x;
            Y = y;
            FacingX = facingX;
            FacingY = facingY;
            VelocityX = velocityX;
            VelocityY = velocityY;
        }

        public ShooterViewEntityKey Key { get; }

        public float X { get; }

        public float Y { get; }

        public float FacingX { get; }

        public float FacingY { get; }

        public float VelocityX { get; }

        public float VelocityY { get; }
    }

    public readonly struct ShooterViewHealthState
    {
        public ShooterViewHealthState(ShooterViewEntityKey key, int hp)
        {
            Key = key;
            Hp = hp;
        }

        public ShooterViewEntityKey Key { get; }

        public int Hp { get; }
    }

    public readonly struct ShooterViewScoreState
    {
        public ShooterViewScoreState(ShooterViewEntityKey key, int score)
        {
            Key = key;
            Score = score;
        }

        public ShooterViewEntityKey Key { get; }

        public int Score { get; }
    }

    public readonly struct ShooterViewProjectileLifetimeState
    {
        public ShooterViewProjectileLifetimeState(ShooterViewEntityKey key, int remainingFrames)
        {
            Key = key;
            RemainingFrames = remainingFrames;
        }

        public ShooterViewEntityKey Key { get; }

        public int RemainingFrames { get; }
    }
}
