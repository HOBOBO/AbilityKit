#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Core.Pooling;
using AbilityKit.Network.Runtime;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Demo.Shooter.View
{
    public sealed class ShooterSnapshotViewModelMapper
    {
        private const int DefaultPooledListCapacity = 256;
        private const int MaxPooledListsPerType = 128;

        private static readonly ObjectPool<List<ShooterViewEntityChange>> EntityChangePool = CreateListPool<ShooterViewEntityChange>();
        private static readonly ObjectPool<List<ShooterViewEntityKey>> RemovedEntityPool = CreateListPool<ShooterViewEntityKey>();
        private static readonly ObjectPool<List<ShooterViewTransformComponentChange>> TransformChangePool = CreateListPool<ShooterViewTransformComponentChange>();
        private static readonly ObjectPool<List<ShooterViewHealthComponentChange>> HealthChangePool = CreateListPool<ShooterViewHealthComponentChange>();
        private static readonly ObjectPool<List<ShooterViewScoreComponentChange>> ScoreChangePool = CreateListPool<ShooterViewScoreComponentChange>();
        private static readonly ObjectPool<List<ShooterViewProjectileLifetimeComponentChange>> ProjectileLifetimeChangePool = CreateListPool<ShooterViewProjectileLifetimeComponentChange>();
        private static readonly ObjectPool<List<ShooterEventSnapshot>> EventPool = CreateListPool<ShooterEventSnapshot>();

        private List<ShooterViewEntityChange>? _entityChanges;
        private List<ShooterViewEntityKey>? _removedEntities;
        private List<ShooterViewTransformComponentChange>? _transformChanges;
        private List<ShooterViewHealthComponentChange>? _healthChanges;
        private List<ShooterViewScoreComponentChange>? _scoreChanges;
        private List<ShooterViewProjectileLifetimeComponentChange>? _projectileLifetimeChanges;
        private List<ShooterEventSnapshot>? _events;
        private HashSet<ShooterViewEntityKey> _localAuthoritativePreviousLiveEntities = new HashSet<ShooterViewEntityKey>();
        private HashSet<ShooterViewEntityKey> _localAuthoritativeCurrentLiveEntities = new HashSet<ShooterViewEntityKey>();
        private readonly Dictionary<ShooterViewEntityKey, int> _localAuthoritativeHealth = new Dictionary<ShooterViewEntityKey, int>();
        private readonly Dictionary<ShooterViewEntityKey, int> _localAuthoritativeScore = new Dictionary<ShooterViewEntityKey, int>();
        private readonly Dictionary<ShooterViewEntityKey, int> _localAuthoritativeProjectileLifetime = new Dictionary<ShooterViewEntityKey, int>();
        private ulong _nextSequence;

        public ShooterSnapshotViewBatch Map(in ShooterStateSnapshotPayload snapshot)
        {
            return Map(in snapshot, ShooterViewBatchSource.LocalPrediction);
        }

        public ShooterSnapshotViewBatch Map(in ShooterStateSnapshotPayload snapshot, ShooterViewBatchSource source)
        {
            BeginSnapshot();

            var players = snapshot.Players ?? Array.Empty<ShooterPlayerSnapshot>();
            var bullets = snapshot.Bullets ?? Array.Empty<ShooterBulletSnapshot>();
            var enemies = snapshot.Enemies ?? Array.Empty<ShooterEnemySnapshot>();
            var events = snapshot.Events ?? Array.Empty<ShooterEventSnapshot>();
            var trackLocalAuthoritativeEntities = source == ShooterViewBatchSource.LocalAuthoritative;
            EnsureMappingCapacity(
                SaturatingAdd(players.Length, bullets.Length, enemies.Length),
                SaturatingAdd(players.Length, bullets.Length, enemies.Length),
                SaturatingAdd(players.Length, enemies.Length),
                players.Length,
                bullets.Length,
                events.Length,
                trackLocalAuthoritativeEntities ? _localAuthoritativePreviousLiveEntities.Count : 0);
            if (trackLocalAuthoritativeEntities)
            {
                _localAuthoritativeCurrentLiveEntities.Clear();
            }

            for (int i = 0; i < players.Length; i++)
            {
                var player = players[i];
                var key = new ShooterViewEntityKey(ShooterViewEntityKind.Player, player.PlayerId);
                AddEntityIfNeeded(trackLocalAuthoritativeEntities, key, 0, player.Alive);
                TrackLocalAuthoritativeLiveEntity(trackLocalAuthoritativeEntities, key, player.Alive);
                AddTransform(key, player.X, player.Y, player.AimX, player.AimY, 0f, 0f);
                AddHealthIfChanged(trackLocalAuthoritativeEntities, key, player.Hp);
                AddScoreIfChanged(trackLocalAuthoritativeEntities, key, player.Score);
            }

            for (int i = 0; i < bullets.Length; i++)
            {
                var bullet = bullets[i];
                var key = new ShooterViewEntityKey(ShooterViewEntityKind.Bullet, bullet.BulletId);
                var alive = bullet.RemainingFrames > 0;
                AddEntityIfNeeded(trackLocalAuthoritativeEntities, key, bullet.OwnerPlayerId, alive);
                TrackLocalAuthoritativeLiveEntity(trackLocalAuthoritativeEntities, key, alive);
                AddTransform(key, bullet.X, bullet.Y, bullet.VelocityX, bullet.VelocityY, bullet.VelocityX, bullet.VelocityY);
                AddProjectileLifetimeIfChanged(trackLocalAuthoritativeEntities, key, bullet.RemainingFrames);
            }

            for (int i = 0; i < enemies.Length; i++)
            {
                var enemy = enemies[i];
                var key = new ShooterViewEntityKey(ShooterViewEntityKind.Enemy, enemy.EnemyId);
                AddEntityIfNeeded(trackLocalAuthoritativeEntities, key, 0, enemy.Alive);
                TrackLocalAuthoritativeLiveEntity(trackLocalAuthoritativeEntities, key, enemy.Alive);
                AddTransform(key, enemy.X, enemy.Y, enemy.FacingX, enemy.FacingY, 0f, 0f);
                AddHealthIfChanged(trackLocalAuthoritativeEntities, key, enemy.Hp);
            }

            if (trackLocalAuthoritativeEntities)
            {
                AddLocalAuthoritativeMissingEntityRemovals();
            }

            if (events.Length > 0)
            {
                _events!.AddRange(events);
            }

            return CompleteSnapshot(
                0UL,
                snapshot.Frame,
                trackLocalAuthoritativeEntities ? ShooterViewSnapshotKind.Delta : ShooterViewSnapshotKind.Full,
                source);
        }

        public ShooterSnapshotViewBatch MapControlledPlayerPrediction(
            in ShooterStateSnapshotPayload snapshot,
            int controlledPlayerId)
        {
            BeginSnapshot();

            var players = snapshot.Players ?? Array.Empty<ShooterPlayerSnapshot>();
            EnsureMappingCapacity(1, 1, 1, 1, 0, 0, 0);
            for (var i = 0; i < players.Length; i++)
            {
                var player = players[i];
                if (player.PlayerId != controlledPlayerId)
                {
                    continue;
                }

                var key = new ShooterViewEntityKey(ShooterViewEntityKind.Player, player.PlayerId);
                AddEntity(key, 0, player.Alive);
                AddTransform(key, player.X, player.Y, player.AimX, player.AimY, 0f, 0f);
                AddHealth(key, player.Hp);
                AddScore(key, player.Score);
                break;
            }

            return CompleteSnapshot(
                0UL,
                snapshot.Frame,
                ShooterViewSnapshotKind.Delta,
                ShooterViewBatchSource.LocalPrediction);
        }

        public ShooterSnapshotViewBatch Map(in ShooterGatewaySnapshot snapshot)
        {
            return Map(in snapshot, controlledPlayerId: -1);
        }

        public ShooterSnapshotViewBatch Map(in ShooterGatewaySnapshot snapshot, int controlledPlayerId)
        {
            if (snapshot.PackedSnapshot.HasValue)
            {
                var packed = snapshot.PackedSnapshot.Value;
                return MapPackedSnapshot(snapshot.WorldId, in packed, ShooterViewBatchSource.AuthoritativeCorrection, controlledPlayerId);
            }

            if (snapshot.PureStateSnapshot.HasValue)
            {
                var pureState = snapshot.PureStateSnapshot.Value;
                return MapPureStateSnapshot(in pureState, ShooterViewBatchSource.AuthoritativeCorrection, controlledPlayerId);
            }

            BeginSnapshot();

            var actors = snapshot.Actors;
            for (int i = 0; i < actors.Count; i++)
            {
                var actor = actors[i];
                var key = new ShooterViewEntityKey(ShooterViewEntityKind.Player, actor.ActorId);
                AddEntity(key, 0, actor.Hp > 0f);
                AddHealth(key, ToDisplayHp(actor.Hp));

                AddTransform(key, actor.X, actor.Y, 0f, 1f, actor.VelocityX, actor.VelocityY);
            }

            return CompleteSnapshot(
                snapshot.WorldId,
                snapshot.Frame,
                snapshot.IsFullSnapshot ? ShooterViewSnapshotKind.Full : ShooterViewSnapshotKind.Delta,
                ShooterViewBatchSource.JoinOrReconnect);
        }

        public ShooterSnapshotViewBatch Map(in ShooterPackedSnapshotPayload snapshot)
        {
            return MapPackedSnapshot(0UL, in snapshot, ShooterViewBatchSource.AuthoritativeCorrection, controlledPlayerId: -1);
        }

        private ShooterSnapshotViewBatch MapPackedSnapshot(
            ulong worldId,
            in ShooterPackedSnapshotPayload snapshot,
            ShooterViewBatchSource source,
            int controlledPlayerId)
        {
            BeginSnapshot();

            var componentChunks = snapshot.ComponentChunks;
            if (componentChunks != null)
            {
                for (int i = 0; i < componentChunks.Length; i++)
                {
                    var chunk = componentChunks[i];
                    ApplyPackedComponentChunk(in chunk, controlledPlayerId);
                }
            }

            var snapshotKind = (snapshot.SnapshotFlags & ShooterPackedSnapshotFlags.Full) != 0
                ? ShooterViewSnapshotKind.Full
                : ShooterViewSnapshotKind.Delta;

            return CompleteSnapshot(
                worldId,
                snapshot.Frame,
                snapshotKind,
                source);
        }

        public ShooterSnapshotViewBatch Map(in ShooterPureStateSnapshotPayload snapshot)
        {
            return Map(in snapshot, controlledPlayerId: -1);
        }

        public ShooterSnapshotViewBatch Map(in ShooterPureStateSnapshotPayload snapshot, int controlledPlayerId)
        {
            return MapPureStateSnapshot(in snapshot, ShooterViewBatchSource.AuthoritativeCorrection, controlledPlayerId);
        }

        private ShooterSnapshotViewBatch MapPureStateSnapshot(
            in ShooterPureStateSnapshotPayload snapshot,
            ShooterViewBatchSource source,
            int controlledPlayerId)
        {
            BeginSnapshot();

            var entities = snapshot.Entities ?? Array.Empty<ShooterPureStateEntityDelta>();
            var entityCount = Math.Min(snapshot.EffectiveEntityCount, entities.Length);
            EnsureMappingCapacity(entityCount, entityCount, entityCount, entityCount, entityCount, 0, entityCount);
            for (var i = 0; i < entityCount; i++)
            {
                ApplyPureStateEntity(in entities[i], controlledPlayerId);
            }

            var snapshotKind = snapshot.SnapshotKind == ShooterPureStateSnapshotKinds.FullBaseline
                ? ShooterViewSnapshotKind.Full
                : ShooterViewSnapshotKind.Delta;

            return CompleteSnapshot(
                snapshot.WorldId,
                snapshot.Frame,
                snapshotKind,
                source);
        }

        private void ApplyPureStateEntity(in ShooterPureStateEntityDelta entity, int controlledPlayerId)
        {
            if (entity.EntityId <= 0)
            {
                return;
            }

            var key = CreateViewEntityKey(entity.EntityKind, entity.EntityId);
            if (!key.HasValue)
            {
                return;
            }

            if (entity.DeltaKind == ShooterPureStateDeltaKinds.Despawn)
            {
                RemoveEntity(key.Value);
                return;
            }

            var alive = (entity.Flags & ShooterPureStateEntityFlags.Alive) != 0 &&
                (entity.Flags & ShooterPureStateEntityFlags.Visible) != 0;
            AddEntity(key.Value, entity.OwnerId, alive);
            var deliveryHints = MapDeliveryHints(entity.Flags);
            var isLocallyControlled = key.Value.Kind == ShooterViewEntityKind.Player &&
                key.Value.EntityId == controlledPlayerId;
            if (SnapshotDeliveryPolicy.ShouldApplyAuthoritativeTransform(deliveryHints, isLocallyControlled))
            {
                AddTransform(
                    key.Value,
                    entity.QuantizedX / 1000f,
                    entity.QuantizedY / 1000f,
                    entity.QuantizedVelocityX == 0 && entity.QuantizedVelocityY == 0 ? 0f : entity.QuantizedVelocityX / 1000f,
                    entity.QuantizedVelocityX == 0 && entity.QuantizedVelocityY == 0 ? 1f : entity.QuantizedVelocityY / 1000f,
                    entity.QuantizedVelocityX / 1000f,
                    entity.QuantizedVelocityY / 1000f,
                    deliveryHints);
            }

            if (key.Value.Kind == ShooterViewEntityKind.Player)
            {
                AddHealth(key.Value, entity.Hp);
                AddScore(key.Value, entity.Score);
            }
            else if (key.Value.Kind == ShooterViewEntityKind.Enemy)
            {
                AddHealth(key.Value, entity.Hp);
            }
            else if (key.Value.Kind == ShooterViewEntityKind.Bullet)
            {
                AddProjectileLifetime(key.Value, entity.RemainingFrames);
            }
        }

        private void ApplyPackedComponentChunk(in ShooterPackedComponentChunk chunk, int controlledPlayerId)
        {
            switch (chunk.ComponentKind)
            {
                case ShooterPackedComponentKinds.EntityLifecycle:
                    ApplyPackedLifecycleComponents(in chunk);
                    break;
                case ShooterPackedComponentKinds.Transform:
                    ApplyPackedTransformComponents(in chunk, controlledPlayerId);
                    break;
                case ShooterPackedComponentKinds.Health:
                    ApplyPackedHealthComponents(in chunk);
                    break;
                case ShooterPackedComponentKinds.Score:
                    ApplyPackedScoreComponents(in chunk);
                    break;
                case ShooterPackedComponentKinds.ProjectileLifetime:
                    ApplyPackedProjectileLifetimeComponents(in chunk);
                    break;
            }
        }

        private void ApplyPackedLifecycleComponents(in ShooterPackedComponentChunk chunk)
        {
            var count = Math.Max(0, chunk.Count);
            for (int i = 0; i < count; i++)
            {
                var entityId = GetInt(chunk.EntityIds, i);
                if (entityId <= 0) continue;

                var key = CreateViewEntityKey(chunk.EntityKind, entityId);
                if (!key.HasValue) continue;

                var flags = GetByte(chunk.Flags, i);
                if ((flags & ShooterPackedEntityFlags.Despawned) != 0)
                {
                    RemoveEntity(key.Value);
                    continue;
                }

                AddEntity(key.Value, GetInt(chunk.OwnerIds, i), (flags & ShooterPackedEntityFlags.Alive) != 0);
            }
        }

        private void ApplyPackedTransformComponents(in ShooterPackedComponentChunk chunk, int controlledPlayerId)
        {
            var count = Math.Max(0, chunk.Count);
            for (int i = 0; i < count; i++)
            {
                var entityId = GetInt(chunk.EntityIds, i);
                if (entityId <= 0) continue;

                var key = CreateViewEntityKey(chunk.EntityKind, entityId);
                if (!key.HasValue) continue;

                AddTransform(
                    key.Value,
                    GetFloat(chunk.ValueX, i),
                    GetFloat(chunk.ValueY, i),
                    GetFloat(chunk.ValueZ, i, 1f),
                    GetFloat(chunk.ValueW, i),
                    GetPackedPairValue(chunk.Aux, i, 0),
                    GetPackedPairValue(chunk.Aux, i, 1));
            }
        }

        private void ApplyPackedHealthComponents(in ShooterPackedComponentChunk chunk)
        {
            var count = Math.Max(0, chunk.Count);
            for (int i = 0; i < count; i++)
            {
                var entityId = GetInt(chunk.EntityIds, i);
                if (entityId <= 0) continue;

                var key = CreateViewEntityKey(chunk.EntityKind, entityId);
                if (!key.HasValue || key.Value.Kind == ShooterViewEntityKind.Bullet) continue;

                AddHealth(key.Value, GetInt(chunk.IntValues, i));
            }
        }

        private void ApplyPackedScoreComponents(in ShooterPackedComponentChunk chunk)
        {
            if (chunk.EntityKind != ShooterPackedEntityKinds.Player)
            {
                return;
            }

            var count = Math.Max(0, chunk.Count);
            for (int i = 0; i < count; i++)
            {
                var playerId = GetInt(chunk.EntityIds, i);
                if (playerId <= 0) continue;

                AddScore(new ShooterViewEntityKey(ShooterViewEntityKind.Player, playerId), GetInt(chunk.IntValues, i));
            }
        }

        private void ApplyPackedProjectileLifetimeComponents(in ShooterPackedComponentChunk chunk)
        {
            if (chunk.EntityKind != ShooterPackedEntityKinds.Projectile)
            {
                return;
            }

            var count = Math.Max(0, chunk.Count);
            for (int i = 0; i < count; i++)
            {
                var bulletId = GetInt(chunk.EntityIds, i);
                if (bulletId <= 0) continue;

                AddProjectileLifetime(new ShooterViewEntityKey(ShooterViewEntityKind.Bullet, bulletId), GetInt(chunk.IntValues, i));
            }
        }

        public void ClearTrackedState()
        {
            _localAuthoritativePreviousLiveEntities.Clear();
            _localAuthoritativeCurrentLiveEntities.Clear();
            _localAuthoritativeHealth.Clear();
            _localAuthoritativeScore.Clear();
            _localAuthoritativeProjectileLifetime.Clear();
        }

        private void BeginSnapshot()
        {
            _entityChanges = EntityChangePool.Get();
            _removedEntities = RemovedEntityPool.Get();
            _transformChanges = TransformChangePool.Get();
            _healthChanges = HealthChangePool.Get();
            _scoreChanges = ScoreChangePool.Get();
            _projectileLifetimeChanges = ProjectileLifetimeChangePool.Get();
            _events = EventPool.Get();
        }

        private void EnsureMappingCapacity(
            int entityChanges,
            int transformChanges,
            int healthChanges,
            int scoreChanges,
            int projectileLifetimeChanges,
            int events,
            int removedEntities)
        {
            EnsureCapacity(_entityChanges!, entityChanges);
            EnsureCapacity(_transformChanges!, transformChanges);
            EnsureCapacity(_healthChanges!, healthChanges);
            EnsureCapacity(_scoreChanges!, scoreChanges);
            EnsureCapacity(_projectileLifetimeChanges!, projectileLifetimeChanges);
            EnsureCapacity(_events!, events);
            EnsureCapacity(_removedEntities!, removedEntities);
        }

        private static void EnsureCapacity<T>(List<T> values, int capacity)
        {
            if (capacity > values.Capacity)
            {
                values.Capacity = capacity;
            }
        }

        private static int SaturatingAdd(int first, int second)
        {
            return first > int.MaxValue - second ? int.MaxValue : first + second;
        }

        private static int SaturatingAdd(int first, int second, int third)
        {
            return SaturatingAdd(SaturatingAdd(first, second), third);
        }

        private void AddEntity(ShooterViewEntityKey key, int ownerEntityId, bool alive)
        {
            _entityChanges!.Add(new ShooterViewEntityChange(key, ownerEntityId, alive));
        }

        private void AddEntityIfNeeded(bool localAuthoritativeDelta, ShooterViewEntityKey key, int ownerEntityId, bool alive)
        {
            if (!localAuthoritativeDelta || !alive || !_localAuthoritativePreviousLiveEntities.Contains(key))
            {
                AddEntity(key, ownerEntityId, alive);
            }

            if (localAuthoritativeDelta && !alive)
            {
                RemoveTrackedLocalAuthoritativeComponents(key);
            }
        }

        private void TrackLocalAuthoritativeLiveEntity(bool enabled, ShooterViewEntityKey key, bool alive)
        {
            if (enabled && alive)
            {
                _localAuthoritativeCurrentLiveEntities.Add(key);
            }
        }

        private void AddLocalAuthoritativeMissingEntityRemovals()
        {
            foreach (var key in _localAuthoritativePreviousLiveEntities)
            {
                if (!_localAuthoritativeCurrentLiveEntities.Contains(key))
                {
                    RemoveEntity(key);
                    RemoveTrackedLocalAuthoritativeComponents(key);
                }
            }

            var previous = _localAuthoritativePreviousLiveEntities;
            _localAuthoritativePreviousLiveEntities = _localAuthoritativeCurrentLiveEntities;
            _localAuthoritativeCurrentLiveEntities = previous;
        }

        private void RemoveEntity(ShooterViewEntityKey key)
        {
            _removedEntities!.Add(key);
        }

        private void AddTransform(
            ShooterViewEntityKey key,
            float x,
            float y,
            float facingX,
            float facingY,
            float velocityX,
            float velocityY,
            SnapshotDeliveryHints deliveryHints = SnapshotDeliveryHints.None)
        {
            _transformChanges!.Add(new ShooterViewTransformComponentChange(key, x, y, facingX, facingY, velocityX, velocityY, deliveryHints));
        }

        private static SnapshotDeliveryHints MapDeliveryHints(byte entityFlags)
        {
            var hints = SnapshotDeliveryHints.None;
            if ((entityFlags & ShooterPureStateEntityFlags.LowFrequency) != 0)
            {
                hints |= SnapshotDeliveryHints.SparseUpdate;
            }

            if ((entityFlags & ShooterPureStateEntityFlags.PredictedLocal) != 0)
            {
                hints |= SnapshotDeliveryHints.PredictedOwner;
            }

            return hints;
        }

        private void AddHealth(ShooterViewEntityKey key, int hp)
        {
            _healthChanges!.Add(new ShooterViewHealthComponentChange(key, hp));
        }

        private void AddHealthIfChanged(bool localAuthoritativeDelta, ShooterViewEntityKey key, int hp)
        {
            if (!localAuthoritativeDelta)
            {
                AddHealth(key, hp);
                return;
            }

            if (!_localAuthoritativeHealth.TryGetValue(key, out var previous) || previous != hp)
            {
                AddHealth(key, hp);
            }

            _localAuthoritativeHealth[key] = hp;
        }

        private void AddScore(ShooterViewEntityKey key, int score)
        {
            _scoreChanges!.Add(new ShooterViewScoreComponentChange(key, score));
        }

        private void AddScoreIfChanged(bool localAuthoritativeDelta, ShooterViewEntityKey key, int score)
        {
            if (!localAuthoritativeDelta)
            {
                AddScore(key, score);
                return;
            }

            if (!_localAuthoritativeScore.TryGetValue(key, out var previous) || previous != score)
            {
                AddScore(key, score);
            }

            _localAuthoritativeScore[key] = score;
        }

        private void AddProjectileLifetime(ShooterViewEntityKey key, int remainingFrames)
        {
            _projectileLifetimeChanges!.Add(new ShooterViewProjectileLifetimeComponentChange(key, remainingFrames));
        }

        private void AddProjectileLifetimeIfChanged(bool localAuthoritativeDelta, ShooterViewEntityKey key, int remainingFrames)
        {
            if (!localAuthoritativeDelta)
            {
                AddProjectileLifetime(key, remainingFrames);
                return;
            }

            if (!_localAuthoritativeProjectileLifetime.TryGetValue(key, out var previous) || previous != remainingFrames)
            {
                AddProjectileLifetime(key, remainingFrames);
            }

            _localAuthoritativeProjectileLifetime[key] = remainingFrames;
        }

        private void RemoveTrackedLocalAuthoritativeComponents(ShooterViewEntityKey key)
        {
            _localAuthoritativeHealth.Remove(key);
            _localAuthoritativeScore.Remove(key);
            _localAuthoritativeProjectileLifetime.Remove(key);
        }

        private ShooterSnapshotViewBatch CompleteSnapshot(
            ulong worldId,
            int frame,
            ShooterViewSnapshotKind snapshotKind,
            ShooterViewBatchSource source)
        {
            var batch = new ShooterSnapshotViewBatch(
                worldId,
                frame,
                ++_nextSequence,
                snapshotKind,
                source,
                _entityChanges!,
                _removedEntities!,
                _transformChanges!,
                _healthChanges!,
                _scoreChanges!,
                _projectileLifetimeChanges!,
                _events!);

            _entityChanges = null;
            _removedEntities = null;
            _transformChanges = null;
            _healthChanges = null;
            _scoreChanges = null;
            _projectileLifetimeChanges = null;
            _events = null;

            return batch;
        }

        private static ObjectPool<List<T>> CreateListPool<T>()
        {
            return Pools.GetPool(
                () => new List<T>(DefaultPooledListCapacity),
                onGet: static list => list.Clear(),
                onRelease: static list => list.Clear(),
                defaultCapacity: 0,
                maxSize: MaxPooledListsPerType,
                collectionCheck: false);
        }

        internal static void ReleasePooledList(List<ShooterViewEntityChange> values) => EntityChangePool.Release(values);

        internal static void ReleasePooledList(List<ShooterViewEntityKey> values) => RemovedEntityPool.Release(values);

        internal static void ReleasePooledList(List<ShooterViewTransformComponentChange> values) => TransformChangePool.Release(values);

        internal static void ReleasePooledList(List<ShooterViewHealthComponentChange> values) => HealthChangePool.Release(values);

        internal static void ReleasePooledList(List<ShooterViewScoreComponentChange> values) => ScoreChangePool.Release(values);

        internal static void ReleasePooledList(List<ShooterViewProjectileLifetimeComponentChange> values) => ProjectileLifetimeChangePool.Release(values);

        internal static void ReleasePooledList(List<ShooterEventSnapshot> values) => EventPool.Release(values);

        internal static List<ShooterViewEntityChange> RentPooledEntityChanges(int capacity) =>
            RentPooledList(EntityChangePool, capacity);

        internal static List<ShooterViewEntityKey> RentPooledRemovedEntities(int capacity) =>
            RentPooledList(RemovedEntityPool, capacity);

        internal static List<ShooterViewTransformComponentChange> RentPooledTransformChanges(int capacity) =>
            RentPooledList(TransformChangePool, capacity);

        private static List<T> RentPooledList<T>(ObjectPool<List<T>> pool, int capacity)
        {
            var values = pool.Get();
            if (values.Capacity < capacity)
            {
                values.Capacity = capacity;
            }

            return values;
        }

        private static ShooterViewEntityKey? CreateViewEntityKey(int entityKind, int entityId)
        {
            switch (entityKind)
            {
                case ShooterPackedEntityKinds.Player:
                    return new ShooterViewEntityKey(ShooterViewEntityKind.Player, entityId);
                case ShooterPackedEntityKinds.Projectile:
                    return new ShooterViewEntityKey(ShooterViewEntityKind.Bullet, entityId);
                case ShooterPackedEntityKinds.Enemy:
                    return new ShooterViewEntityKey(ShooterViewEntityKind.Enemy, entityId);
                default:
                    return null;
            }
        }

        private static int ToDisplayHp(float hp)
        {
            if (hp <= 0f)
            {
                return 0;
            }

            return (int)Math.Round(hp);
        }

        private static int GetInt(int[] values, int index, int fallback = 0)
        {
            return values != null && index >= 0 && index < values.Length ? values[index] : fallback;
        }

        private static float GetFloat(float[] values, int index, float fallback = 0f)
        {
            return values != null && index >= 0 && index < values.Length ? values[index] : fallback;
        }

        private static byte GetByte(byte[] values, int index, byte fallback = 0)
        {
            return values != null && index >= 0 && index < values.Length ? values[index] : fallback;
        }

        private static float GetPackedPairValue(int[] values, int index, int slot)
        {
            return GetInt(values, (index * 2) + slot) / 10000f;
        }
    }
}
