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

        public void Clear()
        {
            _entities.Clear();
            _transforms.Clear();
            _health.Clear();
            _scores.Clear();
            _projectileLifetimes.Clear();
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

            var existed = _entities.ContainsKey(change.Key);
            if (!existed)
            {
                IncrementKindCount(change.Key.Kind);
            }

            _entities[change.Key] = new ShooterViewEntityState(change.Key, change.OwnerEntityId, change.Alive);
            return existed;
        }

        public bool RemoveEntity(ShooterViewEntityKey key)
        {
            var removed = _entities.Remove(key);
            if (removed)
            {
                DecrementKindCount(key.Kind);
            }

            _transforms.Remove(key);
            _health.Remove(key);
            _scores.Remove(key);
            _projectileLifetimes.Remove(key);
            return removed;
        }

        public bool UpsertTransform(in ShooterViewTransformComponentChange change)
        {
            if (!_entities.ContainsKey(change.Key))
            {
                return false;
            }

            _transforms[change.Key] = new ShooterViewTransformState(
                change.Key,
                change.X,
                change.Y,
                change.FacingX,
                change.FacingY,
                change.VelocityX,
                change.VelocityY);
            return true;
        }

        public bool UpsertHealth(in ShooterViewHealthComponentChange change)
        {
            if (!_entities.ContainsKey(change.Key))
            {
                return false;
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

            _scores[change.Key] = new ShooterViewScoreState(change.Key, change.Score);
            return true;
        }

        public bool UpsertProjectileLifetime(in ShooterViewProjectileLifetimeComponentChange change)
        {
            if (!_entities.ContainsKey(change.Key))
            {
                return false;
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
        {
            Key = key;
            OwnerEntityId = ownerEntityId;
            Alive = alive;
        }

        public ShooterViewEntityKey Key { get; }

        public ShooterViewEntityKind Kind => Key.Kind;

        public int EntityId => Key.EntityId;

        public int OwnerEntityId { get; }

        public bool Alive { get; }
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
