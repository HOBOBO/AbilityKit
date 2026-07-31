using System;
using System.Collections.Generic;
using AbilityKit.Protocol.Shooter;
using AbilityKit.World.Svelto;
using Svelto.DataStructures;
using Svelto.ECS;
using Svelto.ECS.Internal;
namespace AbilityKit.Demo.Shooter.Runtime
{
    /// <summary>
    /// Packed 快照导出器。
    ///
    /// 性能说明（2026-07-25 高单位量优化）：
    /// 1. 每类实体的 Svelto 查询与排序索引每次 Export 只计算一次，
    ///    由所有相关 chunk 共享（此前玩家/投射物/敌人各被查询+排序 3-4 次）。
    /// 2. chunk 数组使用持久化缓冲（按需翻倍扩容，不每帧 new）。
    /// 3. 排序由插入排序 O(n²) 改为 Array.Sort O(n log n)（见 ShooterSnapshotOrderBuffer）。
    ///
    /// 缓冲复用前提：Export 产出的 payload 必须在下一次 Export 前完成序列化
    /// （当前全部调用方——网关推送与回滚捕获——都是 Export→Serialize 同步完成）。
    /// 单线程战斗世界内安全。
    /// </summary>
    public sealed class ShooterPackedSnapshotExporter
    {
        private readonly ShooterBattleState _state;
        private readonly IShooterEntityManager _entities;
        private readonly IShooterBattleRules _rules;
        private readonly IShooterStateHashProvider _stateHashProvider;
        private readonly ISveltoWorldContext _context;
        private readonly ShooterSnapshotOrderBuffer _playerOrderBuffer = new();
        private readonly ShooterSnapshotOrderBuffer _projectileOrderBuffer = new();
        private readonly ShooterSnapshotOrderBuffer _enemyOrderBuffer = new();
        private readonly HashSet<int> _lastExportedProjectileIds = new HashSet<int>();
        private readonly HashSet<int> _lastExportedEnemyIds = new HashSet<int>();
        private readonly HashSet<int> _currentProjectileIds = new HashSet<int>();
        private readonly HashSet<int> _currentEnemyIds = new HashSet<int>();
        private readonly List<int> _despawnedProjectileIds = new List<int>();
        private readonly List<int> _despawnedEnemyIds = new List<int>();

        // ===== 持久化 chunk 缓冲（按 chunk 类型各持一份，避免 payload 间互相覆盖） =====
        private readonly IdsFlagsOwnersArrays _playerLifecycleArrays = new();
        private readonly IdsFlagsOwnersArrays _projectileLifecycleArrays = new();
        private readonly IdsFlagsArrays _enemyLifecycleArrays = new();
        private readonly TransformArrays _playerTransformArrays = new();
        private readonly TransformArrays _projectileTransformArrays = new();
        private readonly TransformArrays _enemyTransformArrays = new();
        private readonly IdsIntsArrays _playerHealthArrays = new();
        private readonly IdsInts2Arrays _enemyHealthArrays = new();
        private readonly IdsIntsArrays _playerScoreArrays = new();
        private readonly LifetimeArrays _projectileLifetimeArrays = new();
        private int[] _runtimeMetadataInts = Array.Empty<int>();

        public ShooterPackedSnapshotExporter(
            ShooterBattleState state,
            IShooterEntityManager entities,
            IShooterBattleRules rules,
            IShooterStateHashProvider stateHashProvider)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _entities = entities ?? throw new ArgumentNullException(nameof(entities));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _stateHashProvider = stateHashProvider ?? throw new ArgumentNullException(nameof(stateHashProvider));
            _context = _entities.SveltoContext;
        }

        public ShooterPackedSnapshotPayload Export(ulong worldId, bool isFullSnapshot = true, bool authorityOverride = false)
        {
            var componentChunks = ExportComponentChunks(isFullSnapshot);

            return new ShooterPackedSnapshotPayload(
                ShooterPackedSnapshotCodec.CurrentVersion,
                worldId,
                _state.CurrentFrame,
                _state.CurrentFrame,
                CreateSnapshotFlags(isFullSnapshot, authorityOverride),
                _stateHashProvider.ComputeStateHash(),
                _entities.PlayerCount + _entities.ProjectileCount + CountEnemies(),
                Array.Empty<byte>(),
                componentChunks);
        }

        private ShooterPackedComponentChunk[] ExportComponentChunks(bool isFullSnapshot)
        {
            // 每类实体查询 + 排序只计算一次，所有相关 chunk 共享
            var playerCollection = _context.EntitiesDB.QueryEntities<ShooterSveltoPlayerComponent>((ExclusiveGroupStruct)ShooterSveltoGroups.Players);
            playerCollection.Deconstruct(out NB<ShooterSveltoPlayerComponent> players, out _, out var playerCount);
            var playerOrder = playerCount > 0 ? _playerOrderBuffer.CreateSortedPlayerOrder(players, playerCount) : null;

            var projectileCollection = _context.EntitiesDB.QueryEntities<ShooterSveltoProjectileComponent>((ExclusiveGroupStruct)ShooterSveltoGroups.Projectiles);
            projectileCollection.Deconstruct(out NB<ShooterSveltoProjectileComponent> bullets, out _, out var projectileCount);
            var projectileOrder = projectileCount > 0 ? _projectileOrderBuffer.CreateSortedProjectileOrder(bullets, projectileCount) : null;

            var enemyCollection = _context.EntitiesDB.QueryEntities<ShooterSveltoTransformComponent, ShooterSveltoHealthComponent, ShooterSveltoNavigationComponent>((ExclusiveGroupStruct)ShooterSveltoGroups.GameplayTargets);
            enemyCollection.Deconstruct(
                out NB<ShooterSveltoTransformComponent> enemyTransforms,
                out NB<ShooterSveltoHealthComponent> enemyHealths,
                out NB<ShooterSveltoNavigationComponent> enemyNavigation,
                out NativeEntityIDs enemyIds,
                out var enemyCount);
            var enemyOrder = enemyCount > 0 ? _enemyOrderBuffer.CreateSortedEnemyOrder(enemyIds, enemyCount) : null;

            return new[]
            {
                ExportRuntimeMetadataChunk(),
                ExportPlayerLifecycleChunk(players, playerCount, playerOrder),
                ExportProjectileLifecycleChunk(bullets, projectileCount, projectileOrder, isFullSnapshot),
                ExportEnemyLifecycleChunk(enemyHealths, enemyIds, enemyCount, enemyOrder, isFullSnapshot),
                ExportPlayerTransformChunk(players, playerCount, playerOrder),
                ExportProjectileTransformChunk(bullets, projectileCount, projectileOrder),
                ExportEnemyTransformChunk(enemyTransforms, enemyNavigation, enemyIds, enemyCount, enemyOrder),
                ExportPlayerHealthChunk(players, playerCount, playerOrder),
                ExportEnemyHealthChunk(enemyHealths, enemyIds, enemyCount, enemyOrder),
                ExportPlayerScoreChunk(players, playerCount, playerOrder),
                ExportProjectileLifetimeChunk(bullets, projectileCount, projectileOrder)
            };
        }

        private ShooterPackedComponentChunk ExportRuntimeMetadataChunk()
        {
            var ints = Ensure(ref _runtimeMetadataInts, 6);
            ints[0] = (int)_state.MatchState;
            ints[1] = _state.MatchCompletedFrame;
            ints[2] = _state.DefeatedEnemies;
            ints[3] = _state.VictoryTargetDefeats;
            ints[4] = _state.TimeLimitFrames;
            ints[5] = _state.RemainingTimeFrames;

            return new ShooterPackedComponentChunk(
                ShooterPackedComponentKinds.RuntimeMetadata,
                0,
                6,
                Array.Empty<int>(),
                Array.Empty<float>(),
                Array.Empty<float>(),
                Array.Empty<float>(),
                Array.Empty<float>(),
                ints,
                Array.Empty<byte>(),
                Array.Empty<int>(),
                Array.Empty<int>());
        }

        private ShooterPackedComponentChunk ExportPlayerLifecycleChunk(
            NB<ShooterSveltoPlayerComponent> players, int count, int[] order)
        {
            if (count == 0)
            {
                return ShooterPackedComponentChunk.Empty(ShooterPackedComponentKinds.EntityLifecycle, ShooterPackedEntityKinds.Player);
            }

            var entityIds = Ensure(ref _playerLifecycleArrays.EntityIds, count);
            var flags = Ensure(ref _playerLifecycleArrays.Flags, count);
            var ownerIds = Ensure(ref _playerLifecycleArrays.OwnerIds, count);
            for (int i = 0; i < count; i++)
            {
                var player = players[order[i]];
                entityIds[i] = player.PlayerId;
                flags[i] = (byte)ShooterPackedEntityFlags.Player;
                if (player.Alive)
                {
                    flags[i] |= ShooterPackedEntityFlags.Alive;
                }

                ownerIds[i] = player.PlayerId;
            }

            return new ShooterPackedComponentChunk(
                ShooterPackedComponentKinds.EntityLifecycle,
                ShooterPackedEntityKinds.Player,
                count,
                entityIds,
                Array.Empty<float>(),
                Array.Empty<float>(),
                Array.Empty<float>(),
                Array.Empty<float>(),
                Array.Empty<int>(),
                flags,
                ownerIds,
                Array.Empty<int>());
        }

        private ShooterPackedComponentChunk ExportProjectileLifecycleChunk(
            NB<ShooterSveltoProjectileComponent> bullets, int count, int[] order, bool isFullSnapshot)
        {
            _currentProjectileIds.Clear();
            _despawnedProjectileIds.Clear();
            if (!isFullSnapshot)
            {
                CollectDespawnedProjectiles(bullets, order, count, _currentProjectileIds, _despawnedProjectileIds);
            }

            var totalCount = count + _despawnedProjectileIds.Count;
            if (totalCount == 0)
            {
                _lastExportedProjectileIds.Clear();
                return ShooterPackedComponentChunk.Empty(ShooterPackedComponentKinds.EntityLifecycle, ShooterPackedEntityKinds.Projectile);
            }

            var entityIds = Ensure(ref _projectileLifecycleArrays.EntityIds, totalCount);
            var flags = Ensure(ref _projectileLifecycleArrays.Flags, totalCount);
            var ownerIds = Ensure(ref _projectileLifecycleArrays.OwnerIds, totalCount);
            for (int i = 0; i < count; i++)
            {
                var bullet = bullets[order[i]];
                entityIds[i] = bullet.BulletId;
                flags[i] = (byte)(ShooterPackedEntityFlags.Alive | ShooterPackedEntityFlags.Projectile);
                ownerIds[i] = bullet.OwnerPlayerId;
                _currentProjectileIds.Add(bullet.BulletId);
            }

            for (int i = 0; i < _despawnedProjectileIds.Count; i++)
            {
                var targetIndex = count + i;
                entityIds[targetIndex] = _despawnedProjectileIds[i];
                flags[targetIndex] = (byte)(ShooterPackedEntityFlags.Projectile | ShooterPackedEntityFlags.Despawned);
            }

            ReplaceLastExportedProjectiles(_currentProjectileIds);

            return new ShooterPackedComponentChunk(
                ShooterPackedComponentKinds.EntityLifecycle,
                ShooterPackedEntityKinds.Projectile,
                totalCount,
                entityIds,
                Array.Empty<float>(),
                Array.Empty<float>(),
                Array.Empty<float>(),
                Array.Empty<float>(),
                Array.Empty<int>(),
                flags,
                ownerIds,
                Array.Empty<int>());
        }

        private void CollectDespawnedProjectiles(
            NB<ShooterSveltoProjectileComponent> bullets,
            int[] order,
            int count,
            HashSet<int> currentProjectileIds,
            List<int> despawnedProjectileIds)
        {
            for (int i = 0; i < count; i++)
            {
                currentProjectileIds.Add(bullets[order[i]].BulletId);
            }

            if (_lastExportedProjectileIds.Count == 0)
            {
                return;
            }

            foreach (var projectileId in _lastExportedProjectileIds)
            {
                if (!currentProjectileIds.Contains(projectileId))
                {
                    despawnedProjectileIds.Add(projectileId);
                }
            }

            despawnedProjectileIds.Sort();
        }

        private void ReplaceLastExportedProjectiles(HashSet<int> currentProjectileIds)
        {
            _lastExportedProjectileIds.Clear();
            foreach (var projectileId in currentProjectileIds)
            {
                _lastExportedProjectileIds.Add(projectileId);
            }
        }

        private ShooterPackedComponentChunk ExportEnemyLifecycleChunk(
            NB<ShooterSveltoHealthComponent> healths, NativeEntityIDs ids, int count, int[] order, bool isFullSnapshot)
        {
            _currentEnemyIds.Clear();
            _despawnedEnemyIds.Clear();
            if (!isFullSnapshot)
            {
                CollectDespawnedEnemies(ids, order, count, _currentEnemyIds, _despawnedEnemyIds);
            }

            var totalCount = count + _despawnedEnemyIds.Count;
            if (totalCount == 0)
            {
                _lastExportedEnemyIds.Clear();
                return ShooterPackedComponentChunk.Empty(ShooterPackedComponentKinds.EntityLifecycle, ShooterPackedEntityKinds.Enemy);
            }

            var entityIds = Ensure(ref _enemyLifecycleArrays.EntityIds, totalCount);
            var flags = Ensure(ref _enemyLifecycleArrays.Flags, totalCount);
            for (int i = 0; i < count; i++)
            {
                var sourceIndex = order[i];
                var enemyId = (int)ids[sourceIndex];
                entityIds[i] = enemyId;
                flags[i] = (byte)ShooterPackedEntityFlags.Enemy;
                if (healths[sourceIndex].Alive != 0)
                {
                    flags[i] |= ShooterPackedEntityFlags.Alive;
                }

                _currentEnemyIds.Add(enemyId);
            }

            for (int i = 0; i < _despawnedEnemyIds.Count; i++)
            {
                var targetIndex = count + i;
                entityIds[targetIndex] = _despawnedEnemyIds[i];
                flags[targetIndex] = (byte)(ShooterPackedEntityFlags.Enemy | ShooterPackedEntityFlags.Despawned);
            }

            ReplaceLastExportedEnemies(_currentEnemyIds);

            return new ShooterPackedComponentChunk(
                ShooterPackedComponentKinds.EntityLifecycle,
                ShooterPackedEntityKinds.Enemy,
                totalCount,
                entityIds,
                Array.Empty<float>(),
                Array.Empty<float>(),
                Array.Empty<float>(),
                Array.Empty<float>(),
                Array.Empty<int>(),
                flags,
                Array.Empty<int>(),
                Array.Empty<int>());
        }

        private void CollectDespawnedEnemies(
            NativeEntityIDs ids,
            int[] order,
            int count,
            HashSet<int> currentEnemyIds,
            List<int> despawnedEnemyIds)
        {
            for (int i = 0; i < count; i++)
            {
                currentEnemyIds.Add((int)ids[order[i]]);
            }

            if (_lastExportedEnemyIds.Count == 0)
            {
                return;
            }

            foreach (var enemyId in _lastExportedEnemyIds)
            {
                if (!currentEnemyIds.Contains(enemyId))
                {
                    despawnedEnemyIds.Add(enemyId);
                }
            }

            despawnedEnemyIds.Sort();
        }

        private void ReplaceLastExportedEnemies(HashSet<int> currentEnemyIds)
        {
            _lastExportedEnemyIds.Clear();
            foreach (var enemyId in currentEnemyIds)
            {
                _lastExportedEnemyIds.Add(enemyId);
            }
        }

        private ShooterPackedComponentChunk ExportPlayerTransformChunk(
            NB<ShooterSveltoPlayerComponent> players, int count, int[] order)
        {
            if (count == 0)
            {
                return ShooterPackedComponentChunk.Empty(ShooterPackedComponentKinds.Transform, ShooterPackedEntityKinds.Player);
            }

            var entityIds = Ensure(ref _playerTransformArrays.EntityIds, count);
            var posX = Ensure(ref _playerTransformArrays.F0, count);
            var posY = Ensure(ref _playerTransformArrays.F1, count);
            var facingX = Ensure(ref _playerTransformArrays.F2, count);
            var facingY = Ensure(ref _playerTransformArrays.F3, count);
            var packedVelocity = Ensure(ref _playerTransformArrays.Packed, count * 2);
            for (int i = 0; i < count; i++)
            {
                var player = players[order[i]];
                entityIds[i] = player.PlayerId;
                posX[i] = player.X;
                posY[i] = player.Y;
                facingX[i] = player.AimX;
                facingY[i] = player.AimY;

                var velocityX = 0f;
                var velocityY = 0f;
                if (_state.InputBuffer.TryGetLatestCommand(player.PlayerId, out var command))
                {
                    var moveX = command.MoveX;
                    var moveY = command.MoveY;
                    if (ShooterBattleMath.Normalize(ref moveX, ref moveY) > 0f)
                    {
                        velocityX = moveX * _rules.PlayerSpeed;
                        velocityY = moveY * _rules.PlayerSpeed;
                    }
                }

                ShooterPackedSnapshotChunkCodec.SetPackedPairValue(packedVelocity, i, velocityX, velocityY);
            }

            return new ShooterPackedComponentChunk(
                ShooterPackedComponentKinds.Transform,
                ShooterPackedEntityKinds.Player,
                count,
                entityIds,
                posX,
                posY,
                facingX,
                facingY,
                Array.Empty<int>(),
                Array.Empty<byte>(),
                Array.Empty<int>(),
                packedVelocity);
        }

        private ShooterPackedComponentChunk ExportProjectileTransformChunk(
            NB<ShooterSveltoProjectileComponent> bullets, int count, int[] order)
        {
            if (count == 0)
            {
                return ShooterPackedComponentChunk.Empty(ShooterPackedComponentKinds.Transform, ShooterPackedEntityKinds.Projectile);
            }

            var entityIds = Ensure(ref _projectileTransformArrays.EntityIds, count);
            var posX = Ensure(ref _projectileTransformArrays.F0, count);
            var posY = Ensure(ref _projectileTransformArrays.F1, count);
            var facingX = Ensure(ref _projectileTransformArrays.F2, count);
            var facingY = Ensure(ref _projectileTransformArrays.F3, count);
            var packedVelocity = Ensure(ref _projectileTransformArrays.Packed, count * 2);
            for (int i = 0; i < count; i++)
            {
                var bullet = bullets[order[i]];
                entityIds[i] = bullet.BulletId;
                posX[i] = bullet.X;
                posY[i] = bullet.Y;
                ShooterPackedSnapshotChunkCodec.SetPackedPairValue(packedVelocity, i, bullet.VelocityX, bullet.VelocityY);
                var dirX = bullet.VelocityX;
                var dirY = bullet.VelocityY;
                ShooterBattleMath.Normalize(ref dirX, ref dirY);
                facingX[i] = dirX;
                facingY[i] = dirY;
            }

            return new ShooterPackedComponentChunk(
                ShooterPackedComponentKinds.Transform,
                ShooterPackedEntityKinds.Projectile,
                count,
                entityIds,
                posX,
                posY,
                facingX,
                facingY,
                Array.Empty<int>(),
                Array.Empty<byte>(),
                Array.Empty<int>(),
                packedVelocity);
        }

        private ShooterPackedComponentChunk ExportEnemyTransformChunk(
            NB<ShooterSveltoTransformComponent> transforms,
            NB<ShooterSveltoNavigationComponent> navigation,
            NativeEntityIDs ids,
            int count,
            int[] order)
        {
            if (count == 0)
            {
                return ShooterPackedComponentChunk.Empty(ShooterPackedComponentKinds.Transform, ShooterPackedEntityKinds.Enemy);
            }

            var entityIds = Ensure(ref _enemyTransformArrays.EntityIds, count);
            var posX = Ensure(ref _enemyTransformArrays.F0, count);
            var posY = Ensure(ref _enemyTransformArrays.F1, count);
            var facingX = Ensure(ref _enemyTransformArrays.F2, count);
            var facingY = Ensure(ref _enemyTransformArrays.F3, count);
            var packedVelocity = Ensure(ref _enemyTransformArrays.Packed, count * 2);
            for (int i = 0; i < count; i++)
            {
                var sourceIndex = order[i];
                entityIds[i] = (int)ids[sourceIndex];
                posX[i] = transforms[sourceIndex].X;
                posY[i] = transforms[sourceIndex].Y;
                facingX[i] = transforms[sourceIndex].DirectionX;
                facingY[i] = transforms[sourceIndex].DirectionY;
                ShooterPackedSnapshotChunkCodec.SetPackedPairValue(
                    packedVelocity,
                    i,
                    navigation[sourceIndex].VelocityX,
                    navigation[sourceIndex].VelocityY);
            }

            return new ShooterPackedComponentChunk(
                ShooterPackedComponentKinds.Transform,
                ShooterPackedEntityKinds.Enemy,
                count,
                entityIds,
                posX,
                posY,
                facingX,
                facingY,
                Array.Empty<int>(),
                Array.Empty<byte>(),
                Array.Empty<int>(),
                packedVelocity);
        }

        private ShooterPackedComponentChunk ExportPlayerHealthChunk(
            NB<ShooterSveltoPlayerComponent> players, int count, int[] order)
        {
            if (count == 0)
            {
                return ShooterPackedComponentChunk.Empty(ShooterPackedComponentKinds.Health, ShooterPackedEntityKinds.Player);
            }

            var entityIds = Ensure(ref _playerHealthArrays.EntityIds, count);
            var hp = Ensure(ref _playerHealthArrays.I0, count);
            for (int i = 0; i < count; i++)
            {
                var player = players[order[i]];
                entityIds[i] = player.PlayerId;
                hp[i] = player.Hp;
            }

            return new ShooterPackedComponentChunk(
                ShooterPackedComponentKinds.Health,
                ShooterPackedEntityKinds.Player,
                count,
                entityIds,
                Array.Empty<float>(),
                Array.Empty<float>(),
                Array.Empty<float>(),
                Array.Empty<float>(),
                hp,
                Array.Empty<byte>(),
                Array.Empty<int>(),
                Array.Empty<int>());
        }

        private ShooterPackedComponentChunk ExportEnemyHealthChunk(
            NB<ShooterSveltoHealthComponent> healths, NativeEntityIDs ids, int count, int[] order)
        {
            if (count == 0)
            {
                return ShooterPackedComponentChunk.Empty(ShooterPackedComponentKinds.Health, ShooterPackedEntityKinds.Enemy);
            }

            var entityIds = Ensure(ref _enemyHealthArrays.EntityIds, count);
            var hp = Ensure(ref _enemyHealthArrays.I0, count);
            var maxHp = Ensure(ref _enemyHealthArrays.I1, count);
            for (int i = 0; i < count; i++)
            {
                var sourceIndex = order[i];
                entityIds[i] = (int)ids[sourceIndex];
                hp[i] = healths[sourceIndex].Current;
                maxHp[i] = healths[sourceIndex].Max;
            }

            return new ShooterPackedComponentChunk(
                ShooterPackedComponentKinds.Health,
                ShooterPackedEntityKinds.Enemy,
                count,
                entityIds,
                Array.Empty<float>(),
                Array.Empty<float>(),
                Array.Empty<float>(),
                Array.Empty<float>(),
                hp,
                Array.Empty<byte>(),
                Array.Empty<int>(),
                maxHp);
        }

        private ShooterPackedComponentChunk ExportPlayerScoreChunk(
            NB<ShooterSveltoPlayerComponent> players, int count, int[] order)
        {
            if (count == 0)
            {
                return ShooterPackedComponentChunk.Empty(ShooterPackedComponentKinds.Score, ShooterPackedEntityKinds.Player);
            }

            var entityIds = Ensure(ref _playerScoreArrays.EntityIds, count);
            var scores = Ensure(ref _playerScoreArrays.I0, count);
            for (int i = 0; i < count; i++)
            {
                var player = players[order[i]];
                entityIds[i] = player.PlayerId;
                scores[i] = player.Score;
            }

            return new ShooterPackedComponentChunk(
                ShooterPackedComponentKinds.Score,
                ShooterPackedEntityKinds.Player,
                count,
                entityIds,
                Array.Empty<float>(),
                Array.Empty<float>(),
                Array.Empty<float>(),
                Array.Empty<float>(),
                scores,
                Array.Empty<byte>(),
                Array.Empty<int>(),
                Array.Empty<int>());
        }

        private ShooterPackedComponentChunk ExportProjectileLifetimeChunk(
            NB<ShooterSveltoProjectileComponent> bullets, int count, int[] order)
        {
            if (count == 0)
            {
                return ShooterPackedComponentChunk.Empty(ShooterPackedComponentKinds.ProjectileLifetime, ShooterPackedEntityKinds.Projectile);
            }

            var entityIds = Ensure(ref _projectileLifetimeArrays.EntityIds, count);
            var remainingFrames = Ensure(ref _projectileLifetimeArrays.I0, count);
            var penetrationRemaining = Ensure(ref _projectileLifetimeArrays.I1, count);
            var explosionRadius = Ensure(ref _projectileLifetimeArrays.F0, count);
            var explosionDamage = Ensure(ref _projectileLifetimeArrays.F1, count);
            for (int i = 0; i < count; i++)
            {
                var bullet = bullets[order[i]];
                entityIds[i] = bullet.BulletId;
                remainingFrames[i] = bullet.RemainingFrames;
                penetrationRemaining[i] = bullet.PenetrationRemaining;
                explosionRadius[i] = bullet.ExplosionRadius;
                explosionDamage[i] = bullet.ExplosionDamage;
            }

            return new ShooterPackedComponentChunk(
                ShooterPackedComponentKinds.ProjectileLifetime,
                ShooterPackedEntityKinds.Projectile,
                count,
                entityIds,
                explosionRadius,
                explosionDamage,
                Array.Empty<float>(),
                Array.Empty<float>(),
                remainingFrames,
                Array.Empty<byte>(),
                Array.Empty<int>(),
                penetrationRemaining);
        }

        private int CountEnemies()
        {
            return _context.EntitiesDB.Count<ShooterSveltoHealthComponent>((ExclusiveGroupStruct)ShooterSveltoGroups.GameplayTargets);
        }

        private static uint CreateSnapshotFlags(bool isFullSnapshot, bool authorityOverride)
        {
            var flags = isFullSnapshot ? ShooterPackedSnapshotFlags.Full : ShooterPackedSnapshotFlags.Delta;
            if (isFullSnapshot)
            {
                flags |= ShooterPackedSnapshotFlags.KeyFrame;
            }

            if (authorityOverride)
            {
                flags |= ShooterPackedSnapshotFlags.AuthorityOverride;
            }

            return flags;
        }

        private static T[] Ensure<T>(ref T[] buffer, int count)
        {
            if (buffer.Length < count)
            {
                var capacity = buffer.Length == 0 ? 16 : buffer.Length;
                while (capacity < count)
                {
                    capacity = checked(capacity * 2);
                }

                buffer = new T[capacity];
            }

            return buffer;
        }

        private sealed class IdsFlagsOwnersArrays
        {
            public int[] EntityIds = Array.Empty<int>();
            public byte[] Flags = Array.Empty<byte>();
            public int[] OwnerIds = Array.Empty<int>();
        }

        private sealed class IdsFlagsArrays
        {
            public int[] EntityIds = Array.Empty<int>();
            public byte[] Flags = Array.Empty<byte>();
        }

        private sealed class TransformArrays
        {
            public int[] EntityIds = Array.Empty<int>();
            public float[] F0 = Array.Empty<float>();
            public float[] F1 = Array.Empty<float>();
            public float[] F2 = Array.Empty<float>();
            public float[] F3 = Array.Empty<float>();
            public int[] Packed = Array.Empty<int>();
        }

        private sealed class IdsIntsArrays
        {
            public int[] EntityIds = Array.Empty<int>();
            public int[] I0 = Array.Empty<int>();
        }

        private sealed class IdsInts2Arrays
        {
            public int[] EntityIds = Array.Empty<int>();
            public int[] I0 = Array.Empty<int>();
            public int[] I1 = Array.Empty<int>();
        }

        private sealed class LifetimeArrays
        {
            public int[] EntityIds = Array.Empty<int>();
            public int[] I0 = Array.Empty<int>();
            public int[] I1 = Array.Empty<int>();
            public float[] F0 = Array.Empty<float>();
            public float[] F1 = Array.Empty<float>();
        }
    }
}
