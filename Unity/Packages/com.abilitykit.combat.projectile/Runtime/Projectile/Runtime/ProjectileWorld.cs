using MemoryPack;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Combat.Collision;
using AbilityKit.Core.Pooling;
using AbilityKit.Core.Mathematics;
using AbilityKit.Deterministic;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Combat.Projectile
{
    public interface IProjectileReturnTargetProvider : IService
    {
        bool TryGetReturnTargetPosition(int launcherActorId, out Vec3 position);
    }

    public interface IProjectileTrackingTargetProvider : IService
    {
        bool TryGetTrackingTargetPosition(int targetActorId, out Vec3 position);
    }

    public interface IProjectileActorIdentityProvider : IService
    {
        bool TryResolveActorId(ColliderId collider, out int actorId);
    }

    /// <summary>Supplies actor identity and deterministic retargeting for bouncing projectiles.</summary>
    public interface IProjectileBounceTargetProvider : IProjectileTrackingTargetProvider, IProjectileActorIdentityProvider
    {
        /// <remarks>Apply owner/team eligibility, exclude hitActorIds, and break ties stably (for example by actor id).</remarks>
        bool TrySelectNextTarget(int ownerId, int hitActorId, in Vec3 projectilePosition, IReadOnlyList<int> hitActorIds,
            float searchRadius, out int targetActorId, out Vec3 targetPosition);
    }

    public sealed partial class ProjectileWorld
    {
        private sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
        {
            public static readonly ReferenceComparer<T> Instance = new ReferenceComparer<T>();
            public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
        }

        private static readonly ObjectPool<Projectile> Pool = Pools.GetPool(
            key: "Projectile",
            createFunc: () => new Projectile(),
            defaultCapacity: 0,
            maxSize: 4096);

        // 命中后的穿透推进量与命中点跳步量，定点化后为 1/1000，与旧 float 0.001f 语义一致。
        private static readonly Fixed64 EpsilonAdvance = Fixed64.FromRatio(1, 1000);

        private readonly ProjectileCollisionQueries _collisionQueries;
        private readonly List<Projectile> _active = new List<Projectile>(128);
        // Filters may hold world references and therefore stay out of the binary snapshot.
        // Keys let a restored projectile reconnect to the same runtime filter instance.
        private readonly Dictionary<int, IProjectileHitFilter> _hitFilters = new Dictionary<int, IProjectileHitFilter>();
        private readonly Dictionary<IProjectileHitFilter, int> _hitFilterKeys =
            new Dictionary<IProjectileHitFilter, int>(ReferenceComparer<IProjectileHitFilter>.Instance);
        private IProjectileReturnTargetProvider _returnTargetProvider;
        private IProjectileTrackingTargetProvider _trackingTargetProvider;
        private IProjectileBounceTargetProvider _bounceTargetProvider;
        private IProjectileActorIdentityProvider _actorIdentityProvider;
        private IProjectileHitFilterResolver _hitFilterResolver;

        private int _nextId = 1;
        private int _nextHitFilterKey = 1;

        public ProjectileWorld(ICollisionWorld collision)
        {
            _collisionQueries = new ProjectileCollisionQueries(collision);
        }

        public void SetReturnTargetProvider(IProjectileReturnTargetProvider provider)
        {
            _returnTargetProvider = provider;
        }

        public void SetTrackingTargetProvider(IProjectileTrackingTargetProvider provider)
        {
            _trackingTargetProvider = provider;
        }

        public void SetBounceTargetProvider(IProjectileBounceTargetProvider provider)
        {
            _bounceTargetProvider = provider;
            _actorIdentityProvider = provider;
            // A bounce provider owns actor lookup and therefore must also keep the selected actor position current.
            if (provider != null && _trackingTargetProvider == null)
                _trackingTargetProvider = provider;
        }

        public void SetActorIdentityProvider(IProjectileActorIdentityProvider provider) => _actorIdentityProvider = provider;

        private void ValidateHitIdentity(ProjectileHitIdentityPolicy policy)
        {
            if (policy != ProjectileHitIdentityPolicy.Collider && policy != ProjectileHitIdentityPolicy.OncePerActor && policy != ProjectileHitIdentityPolicy.OncePerActorPerLeg)
                throw new ArgumentOutOfRangeException(nameof(policy));
            if (policy != ProjectileHitIdentityPolicy.Collider && _actorIdentityProvider == null)
                throw new InvalidOperationException("OncePerActor requires an actor identity provider.");
        }

        /// <summary>
        /// Supplies filters when a rollback snapshot is imported into a different world
        /// instance. The resolver is only called for non-zero filter keys not already
        /// registered by this world.
        /// </summary>
        public void SetHitFilterResolver(IProjectileHitFilterResolver resolver)
        {
            _hitFilterResolver = resolver;
        }

        public int ActiveCount => _active.Count;

        public ProjectileId Spawn(in ProjectileSpawnParams p)
        {
            ValidateHitIdentity(p.HitIdentityPolicy);
            if (p.MaxDistanceResponse != ProjectileMaxDistanceResponse.Exit &&
                p.MaxDistanceResponse != ProjectileMaxDistanceResponse.SuspendAtDestination &&
                p.MaxDistanceResponse != ProjectileMaxDistanceResponse.ReturnToSource)
                throw new ArgumentOutOfRangeException(nameof(p.MaxDistanceResponse));
            if (p.MaxDistanceResponse != ProjectileMaxDistanceResponse.Exit &&
                (!(p.MaxDistance > 0) || float.IsInfinity(p.MaxDistance)))
                throw new ArgumentOutOfRangeException(nameof(p.MaxDistance));
            if (p.MaxDistanceResponse == ProjectileMaxDistanceResponse.ReturnToSource && p.ReturnAfterFrames > 0)
                throw new ArgumentException("Timed and distance return are mutually exclusive.");
            // Validate/resolve behavior before acquiring a pooled instance so invalid config
            // cannot leak a projectile when the policy factory rejects it.
            var hitPolicy = p.HitPolicy ?? ProjectileHitPolicyFactory.Create(p.HitPolicyKind, p.HitPolicyParam);
            var hitFilter = p.HitFilter;
            var hitFilterKey = p.HitFilterKey;
            if (hitFilter == null && hitFilterKey > 0)
                hitFilter = ResolveHitFilter(hitFilterKey);
            hitFilter = hitFilter ?? DefaultProjectileHitFilter.Instance;
            hitFilterKey = BindHitFilter(hitFilter, hitFilterKey);
            var proj = Pool.Get();
            proj.Id = new ProjectileId(_nextId++);
            proj.OwnerId = p.OwnerId;
            proj.TemplateId = p.TemplateId;
            proj.LauncherActorId = p.LauncherActorId;
            proj.RootActorId = p.RootActorId;
            proj.SpawnFrame = p.SpawnFrame;
            proj.Position = p.Position.ToFixed();
            proj.SpawnPosition = proj.Position;
            proj.Direction = p.Direction.ToFixed();
            proj.Speed = p.Speed.ToFixed();
            proj.DistanceTravelled = Fixed64.Zero;
            proj.TrackingTargetActorId = p.TrackingTargetActorId;
            proj.TrackingArcHeightPerUnit = p.TrackingArcHeightPerUnit.ToFixed();
            proj.TrackingArcBaseHeight = p.TrackingArcHeight.ToFixed();
            proj.TrackingArcHeight = proj.TrackingArcBaseHeight;
            proj.TrackingArcElapsed = Fixed64.Zero;
            proj.TrackingArcStartY = proj.Position.Y;
            proj.TrackingArcEndY = proj.Position.Y;
            proj.BounceSearchRadius = p.BounceSearchRadius.ToFixed();
            proj.TrackingArcDuration = p.TrackingArcDurationSeconds.ToFixed();
            proj.TrackingArcDurationConfigured = p.TrackingArcDurationSeconds > 0f;
            if (proj.TrackingTargetActorId > 0 && _trackingTargetProvider != null &&
                _trackingTargetProvider.TryGetTrackingTargetPosition(proj.TrackingTargetActorId, out var arcTarget))
            {
                var planar = new FixedVec3(
                    arcTarget.X.ToFixed() - proj.Position.X,
                    Fixed64.Zero,
                    arcTarget.Z.ToFixed() - proj.Position.Z);
                proj.TrackingArcEndY = arcTarget.Y.ToFixed();
                proj.TrackingArcHeight = proj.TrackingArcBaseHeight + planar.Magnitude * proj.TrackingArcHeightPerUnit;
                if (!proj.TrackingArcDurationConfigured && proj.TrackingArcDuration <= Fixed64.Zero && proj.Speed > Fixed64.Zero)
                    proj.TrackingArcDuration = planar.Magnitude / proj.Speed;
            }
            proj.ReturnAfterFrames = p.ReturnAfterFrames;
            proj.ReturnSpeed = p.ReturnSpeed.ToFixed();
            proj.ReturnStopDistance = p.ReturnStopDistance.ToFixed();
            proj.IsReturning = false;
            proj.LifetimeFramesLeft = p.LifetimeFrames > 0 ? p.LifetimeFrames : int.MaxValue;
            proj.DistanceLeft = p.MaxDistance.ToFixed();
            proj.CollisionLayerMask = p.CollisionLayerMask;
            proj.IgnoreCollider = p.IgnoreCollider;
            proj.CollisionHalfExtents = p.CollisionHalfExtents.ToFixed();
            proj.HitPolicyKind = p.HitPolicyKind;
            proj.HitPolicyParam = p.HitPolicyParam;
            proj.HitPolicy = hitPolicy;
            proj.HitsRemaining = p.HitsRemaining;
            // Bounce's configured parameter is the authoritative distinct-hit budget. The
            // historical spawn default is 1, so normalize that default to the policy budget.
            if (p.HitPolicy == null && p.HitPolicyKind == ProjectileHitPolicyKind.Bounce &&
                p.HitPolicyParam > 0 && p.HitsRemaining == 1)
                proj.HitsRemaining = p.HitPolicyParam;
            proj.TickIntervalFrames = p.TickIntervalFrames;
            proj.NextTickFrame = 0;
            proj.HitFilter = hitFilter;
            proj.HitFilterKey = hitFilterKey;
            proj.HitCooldownFrames = p.HitCooldownFrames;
            proj.HitIdentityPolicy = p.HitIdentityPolicy;
            proj.LastHitCollider = default;
            proj.LastHitAllowedFrame = 0;
            proj.IsSuspended = p.StartSuspended;
            proj.MaxDistanceResponse = p.MaxDistanceResponse;
            proj.ArrivedFrame = -1;
            proj.PatternSlotIndex = p.PatternSlotIndex;
            proj.PatternSlotCount = p.PatternSlotCount;

            _active.Add(proj);
            return proj.Id;
        }

        public bool TryGetRuntimeState(ProjectileId id, out ProjectileRuntimeState state)
        {
            var projectile = Find(id);
            if (projectile == null)
            {
                state = default;
                return false;
            }

            state = new ProjectileRuntimeState(
                projectile.Id,
                projectile.Position.ToVec3(),
                projectile.Direction.ToVec3(),
                projectile.LauncherActorId,
                projectile.RootActorId,
                projectile.PatternSlotIndex,
                projectile.PatternSlotCount,
                projectile.IsSuspended,
                projectile.SpawnPosition.ToVec3(),
                projectile.DistanceTravelled.ToSingle(),
                projectile.SpawnFrame,
                projectile.ElapsedSeconds.ToSingle(), projectile.ArrivedFrame,
                projectile.TrackingTargetActorId == 0 && projectile.BounceSearchRadius == Fixed64.Zero &&
                projectile.MaxDistanceResponse == ProjectileMaxDistanceResponse.Exit && projectile.ReturnAfterFrames == 0 &&
                !projectile.IsReturning && projectile.DistanceLeft > Fixed64.Zero
                    ? (projectile.DistanceTravelled + projectile.DistanceLeft).ToSingle() : 0f);
            return true;
        }

        /// <summary>
        /// 定点运动学视图（内部/测试用）：不经 float 边界损耗，直接暴露 raw 位，
        /// 供确定性测试断言与诊断。
        /// </summary>
        internal bool TryGetFixedKinematics(ProjectileId id, out FixedVec3 position, out FixedVec3 direction)
        {
            var projectile = Find(id);
            position = projectile != null ? projectile.Position : FixedVec3.Zero;
            direction = projectile != null ? projectile.Direction : FixedVec3.Zero;
            return projectile != null;
        }

        public bool TrySetPosition(ProjectileId id, in Vec3 position)
        {
            var projectile = Find(id);
            if (projectile == null) return false;
            projectile.Position = position.ToFixed();
            return true;
        }

        public bool ResumeSimulation(ProjectileId id)
        {
            var projectile = Find(id);
            if (projectile == null) return false;
            if (projectile.ArrivedFrame >= 0) return false;
            projectile.IsSuspended = false;
            return true;
        }

        public byte[] ExportRollback(FrameIndex frame) => MemoryPackSerializer.Serialize(CaptureState(frame));

        internal ProjectileWorldSnapshotPayload CaptureState(FrameIndex frame, Predicate<IProjectileHitFilter>? validateLocalFilter = null)
        {
            var items = new ProjectileWorldSnapshotItem[_active.Count];
            for (int i = 0; i < _active.Count; i++)
            {
                var p = _active[i];
                if (p == null) continue;
                if (validateLocalFilter != null && (p.HitPolicy is not ExitOnHitPolicy ||
                    p.HitFilterKey != 0 && !validateLocalFilter(p.HitFilter)))
                    throw new InvalidOperationException("Unsupported local flight policy or filter.");
                items[i] = new ProjectileWorldSnapshotItem(
                    id: p.Id.Value,
                    ownerId: p.OwnerId,
                    positionX: p.Position.X.RawValue,
                    positionY: p.Position.Y.RawValue,
                    positionZ: p.Position.Z.RawValue,
                    directionX: p.Direction.X.RawValue,
                    directionY: p.Direction.Y.RawValue,
                    directionZ: p.Direction.Z.RawValue,
                    speedRaw: p.Speed.RawValue,
                    lifetimeFramesLeft: p.LifetimeFramesLeft,
                    distanceLeftRaw: p.DistanceLeft.RawValue,
                    collisionLayerMask: p.CollisionLayerMask,
                    ignoreCollider: p.IgnoreCollider.Value,
                    halfExtentsX: p.CollisionHalfExtents.X.RawValue,
                    halfExtentsY: p.CollisionHalfExtents.Y.RawValue,
                    halfExtentsZ: p.CollisionHalfExtents.Z.RawValue,
                    hitsRemaining: p.HitsRemaining,
                    hitPolicyKind: p.HitPolicyKind,
                    hitPolicyParam: p.HitPolicyParam,
                    tickIntervalFrames: p.TickIntervalFrames,
                    nextTickFrame: p.NextTickFrame,
                    templateId: p.TemplateId,
                    launcherActorId: p.LauncherActorId,
                    rootActorId: p.RootActorId,
                    spawnFrame: p.SpawnFrame,
                    spawnPositionX: p.SpawnPosition.X.RawValue,
                    spawnPositionY: p.SpawnPosition.Y.RawValue,
                    spawnPositionZ: p.SpawnPosition.Z.RawValue,
                    distanceTravelledRaw: p.DistanceTravelled.RawValue,
                    elapsedSecondsRaw: p.ElapsedSeconds.RawValue,
                    returnAfterFrames: p.ReturnAfterFrames,
                    returnSpeedRaw: p.ReturnSpeed.RawValue,
                    returnStopDistanceRaw: p.ReturnStopDistance.RawValue,
                    isReturning: p.IsReturning ? 1 : 0,
                    isSuspended: p.IsSuspended ? 1 : 0,
                    patternSlotIndex: p.PatternSlotIndex,
                    patternSlotCount: p.PatternSlotCount,
                    trackingTargetActorId: p.TrackingTargetActorId,
                    trackingArcHeightRaw: p.TrackingArcHeight.RawValue,
                    trackingArcDurationRaw: p.TrackingArcDuration.RawValue,
                    trackingArcElapsedRaw: p.TrackingArcElapsed.RawValue,
                    trackingArcStartYRaw: p.TrackingArcStartY.RawValue,
                    trackingArcEndYRaw: p.TrackingArcEndY.RawValue,
                    hitActorIds: p.HitActorIds.ToArray(),
                    trackingArcDurationConfigured: p.TrackingArcDurationConfigured ? 1 : 0,
                    bounceSearchRadiusRaw: p.BounceSearchRadius.RawValue,
                    trackingArcHeightPerUnitRaw: p.TrackingArcHeightPerUnit.RawValue,
                    trackingArcBaseHeightRaw: p.TrackingArcBaseHeight.RawValue,
                    totalHitCount: p.TotalHitCount,
                    hitFilterKey: p.HitFilterKey,
                    hitCooldownFrames: p.HitCooldownFrames,
                    lastHitCollider: p.LastHitCollider.Value,
                    lastHitAllowedFrame: p.LastHitAllowedFrame,
                    hitIdentityPolicy: p.HitIdentityPolicy,
                    maxDistanceResponse: p.MaxDistanceResponse, arrivedFrame: p.ArrivedFrame, returnHitActorIds: p.ReturnHitActorIds.ToArray(),
                    hitFilterSnapshot: validateLocalFilter == null
                        ? (p.HitFilter as IProjectileHitFilterSnapshotSource)?.CaptureSnapshot() : null
                );
            }

            return new ProjectileWorldSnapshotPayload(
                version: 18,
                frame: frame,
                nextId: _nextId,
                items: items,
                nextHitFilterKey: _nextHitFilterKey
            );
        }

        internal static ProjectileWorldSnapshotPayload ReadRollbackSnapshot(byte[] payload, FrameIndex? frame = null)
        {
            if (payload == null || payload.Length == 0)
                throw new InvalidOperationException("Missing projectile world rollback payload.");
            var snap = MemoryPackSerializer.Deserialize<ProjectileWorldSnapshotPayload>(payload);
            ValidateState(in snap, frame);
            return snap;
        }

        internal static void ValidateState(in ProjectileWorldSnapshotPayload snap, FrameIndex? frame)
        {
            if (snap.Version != 18) throw new InvalidOperationException("Unsupported projectile rollback version.");
            if ((frame.HasValue && snap.Frame.Value != frame.Value.Value) || snap.NextId <= 0 ||
                snap.NextHitFilterKey <= 0 || snap.Items == null)
                throw new InvalidOperationException("Invalid projectile rollback frame or allocator.");
            var ids = new HashSet<int>();
            foreach (var item in snap.Items ?? Array.Empty<ProjectileWorldSnapshotItem>())
            {
                if (item.Id <= 0 || item.Id >= snap.NextId || !ids.Add(item.Id) ||
                    item.HitFilterKey < 0 || item.HitFilterKey >= snap.NextHitFilterKey ||
                    item.HitFilterSnapshot != null && item.HitFilterSnapshot.Length > 0 && item.HitFilterKey == 0)
                    throw new InvalidOperationException("Invalid projectile rollback identity or filter allocator.");
                if (item.HitIdentityPolicy != ProjectileHitIdentityPolicy.Collider &&
                    item.HitIdentityPolicy != ProjectileHitIdentityPolicy.OncePerActor &&
                    item.HitIdentityPolicy != ProjectileHitIdentityPolicy.OncePerActorPerLeg)
                    throw new InvalidOperationException("Invalid projectile hit identity snapshot.");
                ProjectileHitPolicyFactory.ValidateRegisteredKind(item.HitPolicyKind);
                if ((item.MaxDistanceResponse != ProjectileMaxDistanceResponse.Exit &&
                     item.MaxDistanceResponse != ProjectileMaxDistanceResponse.SuspendAtDestination &&
                     item.MaxDistanceResponse != ProjectileMaxDistanceResponse.ReturnToSource) ||
                    item.ArrivedFrame < -1 || (item.ArrivedFrame >= 0 &&
                    !((item.MaxDistanceResponse == ProjectileMaxDistanceResponse.SuspendAtDestination && item.IsSuspended != 0) ||
                      (item.MaxDistanceResponse == ProjectileMaxDistanceResponse.ReturnToSource && item.IsReturning != 0))))
                    throw new InvalidOperationException("Invalid projectile destination snapshot.");
            }
        }

        internal void ValidateRollbackDependencies(in ProjectileWorldSnapshotPayload snapshot)
        {
            foreach (var item in snapshot.Items ?? Array.Empty<ProjectileWorldSnapshotItem>())
            {
                ValidateHitIdentity(item.HitIdentityPolicy);
                if (item.HitFilterSnapshot != null && item.HitFilterSnapshot.Length > 0)
                {
                    if (!(_hitFilterResolver is IProjectileHitFilterSnapshotResolver))
                        throw new InvalidOperationException("Projectile filter snapshot requires a snapshot resolver.");
                }
                else if (item.HitFilterKey > 0 && !_hitFilters.ContainsKey(item.HitFilterKey) && _hitFilterResolver == null)
                    throw new InvalidOperationException("Projectile filter key has no registered instance or resolver.");
            }
        }

        /// <summary>
        /// 还原投射物帧状态；先校验全部条目并准备策略与过滤器，再替换当前对象。
        /// 空载荷保留清空语义，外部构造器或解析器失败不会先清空当前投射物。
        /// </summary>
        public void ImportRollback(FrameIndex frame, byte[] payload)
        {
            if (payload == null || payload.Length == 0) { Clear(); return; }
            var snap = ReadRollbackSnapshot(payload, frame);
            ImportState(in snap);
        }

        internal void ImportState(in ProjectileWorldSnapshotPayload snap, IProjectileHitFilter? localFilter = null,
            bool exactLocalLifetime = false)
        {
            if (localFilter == null) ValidateRollbackDependencies(in snap);
            var items = snap.Items ?? Array.Empty<ProjectileWorldSnapshotItem>();
            var policies = new IProjectileHitPolicy[items.Length];
            var filters = new IProjectileHitFilter[items.Length];
            var preparedFilters = new Dictionary<int, IProjectileHitFilter>();
            /*
             * 外部扩展的构造和解析可能失败，因此准备阶段不写入活动对象或过滤器索引。
             * 预检仅检查数据和依赖是否具备，不调用扩展工厂或过滤器解析器。
             */
            for (var i = 0; i < items.Length; i++)
            {
                policies[i] = ProjectileHitPolicyFactory.Create(items[i].HitPolicyKind, items[i].HitPolicyParam);
                var item = items[i];
                if (item.HitFilterKey > 0 && localFilter != null)
                    filters[i] = localFilter;
                else if ((item.HitFilterSnapshot == null || item.HitFilterSnapshot.Length == 0) &&
                    preparedFilters.TryGetValue(item.HitFilterKey, out var prepared))
                    filters[i] = prepared;
                else
                    filters[i] = PrepareRollbackHitFilter(item.HitFilterKey, item.HitFilterSnapshot);
                preparedFilters[item.HitFilterKey] = filters[i];
            }
            Clear();
            _nextId = snap.NextId;
            _nextHitFilterKey = snap.NextHitFilterKey;
            TrimHitFilters(_nextHitFilterKey);

            if (snap.Items == null || snap.Items.Length == 0) return;

            for (int i = 0; i < snap.Items.Length; i++)
            {
                var it = snap.Items[i];
                if (it.HitFilterKey > 0)
                {
                    if (_hitFilters.TryGetValue(it.HitFilterKey, out var previous)) _hitFilterKeys.Remove(previous);
                    _hitFilters[it.HitFilterKey] = filters[i];
                    _hitFilterKeys[filters[i]] = it.HitFilterKey;
                }

                var p = Pool.Get();
                p.Id = new ProjectileId(it.Id);
                p.OwnerId = it.OwnerId;
                p.TemplateId = it.TemplateId;
                p.LauncherActorId = it.LauncherActorId;
                p.RootActorId = it.RootActorId;
                p.SpawnFrame = it.SpawnFrame;
                p.SpawnPosition = new FixedVec3(
                    Fixed64.FromRaw(it.SpawnPositionX),
                    Fixed64.FromRaw(it.SpawnPositionY),
                    Fixed64.FromRaw(it.SpawnPositionZ));
                p.DistanceTravelled = Fixed64.FromRaw(it.DistanceTravelledRaw);
                p.ElapsedSeconds = Fixed64.FromRaw(it.ElapsedSecondsRaw);
                p.Position = new FixedVec3(
                    Fixed64.FromRaw(it.PositionX),
                    Fixed64.FromRaw(it.PositionY),
                    Fixed64.FromRaw(it.PositionZ));
                p.Direction = new FixedVec3(
                    Fixed64.FromRaw(it.DirectionX),
                    Fixed64.FromRaw(it.DirectionY),
                    Fixed64.FromRaw(it.DirectionZ));
                p.Speed = Fixed64.FromRaw(it.SpeedRaw);
                p.TrackingTargetActorId = it.TrackingTargetActorId;
                p.TrackingArcHeight = Fixed64.FromRaw(it.TrackingArcHeightRaw);
                p.TrackingArcBaseHeight = Fixed64.FromRaw(it.TrackingArcBaseHeightRaw);
                p.TrackingArcHeightPerUnit = Fixed64.FromRaw(it.TrackingArcHeightPerUnitRaw);
                p.TrackingArcDuration = Fixed64.FromRaw(it.TrackingArcDurationRaw);
                p.TrackingArcDurationConfigured = it.TrackingArcDurationConfigured != 0;
                p.TrackingArcElapsed = Fixed64.FromRaw(it.TrackingArcElapsedRaw);
                p.TrackingArcStartY = Fixed64.FromRaw(it.TrackingArcStartYRaw);
                p.TrackingArcEndY = Fixed64.FromRaw(it.TrackingArcEndYRaw);
                p.BounceSearchRadius = Fixed64.FromRaw(it.BounceSearchRadiusRaw);
                if (it.HitActorIds != null) p.HitActorIds.AddRange(it.HitActorIds);
                if (it.ReturnHitActorIds != null) p.ReturnHitActorIds.AddRange(it.ReturnHitActorIds);
                p.ReturnAfterFrames = it.ReturnAfterFrames;
                p.ReturnSpeed = Fixed64.FromRaw(it.ReturnSpeedRaw);
                p.ReturnStopDistance = Fixed64.FromRaw(it.ReturnStopDistanceRaw);
                p.IsReturning = it.IsReturning != 0;
                p.LifetimeFramesLeft = exactLocalLifetime || it.LifetimeFramesLeft > 0 ? it.LifetimeFramesLeft : int.MaxValue;
                p.DistanceLeft = Fixed64.FromRaw(it.DistanceLeftRaw);
                p.CollisionLayerMask = it.CollisionLayerMask;
                p.IgnoreCollider = new ColliderId(it.IgnoreCollider);
                p.CollisionHalfExtents = new FixedVec3(
                    Fixed64.FromRaw(it.HalfExtentsX),
                    Fixed64.FromRaw(it.HalfExtentsY),
                    Fixed64.FromRaw(it.HalfExtentsZ));
                p.HitsRemaining = it.HitsRemaining;
                p.HitPolicyKind = it.HitPolicyKind;
                p.HitIdentityPolicy = it.HitIdentityPolicy;
                p.HitPolicyParam = it.HitPolicyParam;
                p.HitPolicy = policies[i];
                p.TickIntervalFrames = it.TickIntervalFrames;
                p.NextTickFrame = it.NextTickFrame;
                p.TotalHitCount = it.TotalHitCount;
                p.HitFilterKey = it.HitFilterKey;
                p.HitFilter = filters[i];
                p.HitCooldownFrames = it.HitCooldownFrames;
                p.LastHitCollider = new ColliderId(it.LastHitCollider);
                p.LastHitAllowedFrame = it.LastHitAllowedFrame;
                p.IsSuspended = it.IsSuspended != 0;
                p.MaxDistanceResponse = it.MaxDistanceResponse;
                p.ArrivedFrame = it.ArrivedFrame;
                p.PatternSlotIndex = it.PatternSlotIndex;
                p.PatternSlotCount = it.PatternSlotCount;

                _active.Add(p);
            }
        }

        private int BindHitFilter(IProjectileHitFilter filter, int requestedKey)
        {
            if (filter == null || ReferenceEquals(filter, DefaultProjectileHitFilter.Instance)) return 0;

            if (requestedKey > 0)
            {
                if (_hitFilters.TryGetValue(requestedKey, out var existing) &&
                    !ReferenceEquals(existing, filter))
                    throw new InvalidOperationException($"Projectile hit filter key {requestedKey} is already bound to another instance.");

                _hitFilters[requestedKey] = filter;
                _hitFilterKeys[filter] = requestedKey;
                if (_nextHitFilterKey <= requestedKey) _nextHitFilterKey = requestedKey + 1;
                return requestedKey;
            }

            if (_hitFilterKeys.TryGetValue(filter, out var existingKey)) return existingKey;
            while (_nextHitFilterKey <= 0 || _hitFilters.ContainsKey(_nextHitFilterKey))
                _nextHitFilterKey++;
            var key = _nextHitFilterKey++;
            _hitFilters[key] = filter;
            _hitFilterKeys[filter] = key;
            return key;
        }

        private IProjectileHitFilter ResolveHitFilter(int key)
        {
            if (key <= 0) return DefaultProjectileHitFilter.Instance;
            if (_hitFilters.TryGetValue(key, out var filter) && filter != null) return filter;

            filter = _hitFilterResolver?.Resolve(key);
            if (filter == null)
                throw new InvalidOperationException($"Projectile hit filter key {key} is not registered and no resolver returned an instance.");
            BindHitFilter(filter, key);
            return filter;
        }

        internal void ValidateScheduleHitIdentity(ProjectileHitIdentityPolicy policy) => ValidateHitIdentity(policy);

        internal int FindRollbackHitFilterKey(IProjectileHitFilter filter)
        {
            return filter != null && _hitFilterKeys.TryGetValue(filter, out var key) ? key : 0;
        }

        internal IProjectileHitFilter FindRollbackHitFilter(int key) =>
            _hitFilters.TryGetValue(key, out var filter) ? filter : null;

        internal void ValidateScheduleHitFilter(int key, byte[] snapshot)
        {
            if (key < 0) throw new InvalidOperationException("Invalid schedule filter key.");
            if (snapshot != null && snapshot.Length > 0)
            {
                if (!(_hitFilterResolver is IProjectileHitFilterSnapshotResolver))
                    throw new InvalidOperationException("Schedule filter requires a snapshot resolver.");
            }
            else if (key <= 0 || !_hitFilters.ContainsKey(key) && _hitFilterResolver == null)
                throw new InvalidOperationException("Schedule filter has no stable data or dependency.");
        }

        internal IProjectileHitFilter RestoreScheduleHitFilter(int key, byte[] snapshot, bool hasPhysicalReference)
        {
            if (hasPhysicalReference) return _hitFilters[key];
            var filter = snapshot != null && snapshot.Length > 0
                ? RestoreHitFilterSnapshot(snapshot) : PrepareRollbackHitFilter(key, snapshot);
            /*
             * 已分配但不再被弹体引用的过滤器仍可能属于未来任务。
             * 只替换已有绑定，不提前分配首次发射才应分配的身份。
             */
            if (key > 0 && _hitFilters.TryGetValue(key, out var previous))
            {
                _hitFilterKeys.Remove(previous);
                _hitFilters[key] = filter;
                _hitFilterKeys[filter] = key;
            }
            return filter;
        }

        private IProjectileHitFilter PrepareRollbackHitFilter(int key, byte[]? snapshot)
        {
            if (snapshot == null || snapshot.Length == 0)
            {
                if (key <= 0) return DefaultProjectileHitFilter.Instance;
                if (_hitFilters.TryGetValue(key, out var existing) && existing != null) return existing;
                var resolved = _hitFilterResolver?.Resolve(key);
                if (resolved == null)
                    throw new InvalidOperationException($"Projectile hit filter key {key} could not be resolved.");
                return resolved;
            }
            if (key <= 0 || !(_hitFilterResolver is IProjectileHitFilterSnapshotResolver))
                throw new InvalidOperationException("Projectile filter snapshot requires a snapshot resolver and a positive filter key.");
            return RestoreHitFilterSnapshot(snapshot);
        }

        private IProjectileHitFilter RestoreHitFilterSnapshot(byte[] snapshot)
        {
            if (!(_hitFilterResolver is IProjectileHitFilterSnapshotResolver resolver))
                throw new InvalidOperationException("Projectile filter snapshot requires a snapshot resolver.");
            var filter = resolver.ResolveSnapshot(snapshot);
            if (filter == null) throw new InvalidOperationException("Projectile filter snapshot resolver returned no filter.");
            return filter;
        }

        private void TrimHitFilters(int nextKey)
        {
            if (_hitFilters.Count == 0) return;
            var removed = new List<IProjectileHitFilter>();
            var keys = new List<int>();
            foreach (var pair in _hitFilters)
            {
                if (pair.Key >= nextKey)
                {
                    keys.Add(pair.Key);
                    removed.Add(pair.Value);
                }
            }
            for (var i = 0; i < keys.Count; i++) _hitFilters.Remove(keys[i]);
            for (var i = 0; i < removed.Count; i++) _hitFilterKeys.Remove(removed[i]);
        }

        public void Clear()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var p = _active[i];
                if (p != null) Pool.Release(p);
            }
            _active.Clear();
        }

        public bool Despawn(ProjectileId id)
        {
            return Despawn(id, frame: 0, ProjectileExitReason.Manual, out _);
        }

        public bool Despawn(
            ProjectileId id,
            int frame,
            ProjectileExitReason reason,
            out ProjectileExitEvent exitEvent)
        {
            for (int i = 0; i < _active.Count; i++)
            {
                var p = _active[i];
                if (p == null) continue;
                if (p.Id.Value != id.Value) continue;

                exitEvent = new ProjectileExitEvent(
                    p.Id,
                    p.OwnerId,
                    p.TemplateId,
                    p.LauncherActorId,
                    p.RootActorId,
                    reason,
                    frame,
                    p.Position.ToVec3());
                RemoveAtSwapBack(i);
                return true;
            }

            exitEvent = default;
            return false;
        }

        public void Tick(int frame, float fixedDeltaSeconds, List<ProjectileHitEvent> hitEvents, List<ProjectileExitEvent> exitEvents, List<ProjectileTickEvent> tickEvents, List<ProjectileArrivalEvent> arrivalEvents = null)
        {
            // float 重载仅为边界兼容；dt 的位与平台无关（FrameTime 用 IEEE 除法得到 fixedDelta），
            // FromSingle 是单次精确换算。新代码应优先使用 Fixed64 重载。
            Tick(frame, Fixed64.FromSingle(fixedDeltaSeconds), hitEvents, exitEvents, tickEvents, arrivalEvents);
        }

        public void Tick(int frame, Fixed64 fixedDeltaSeconds, List<ProjectileHitEvent> hitEvents, List<ProjectileExitEvent> exitEvents, List<ProjectileTickEvent> tickEvents, List<ProjectileArrivalEvent> arrivalEvents = null)
        {
            if (_active.Count == 0) return;

            for (int i = 0; i < _active.Count; i++)
            {
                if (TickProjectile(i, frame, fixedDeltaSeconds, hitEvents, exitEvents, tickEvents, arrivalEvents))
                {
                    RemoveAtSwapBack(i);
                    i--;
                }
            }
        }

        private static void EmitTickIfDue(Projectile p, int frame, List<ProjectileTickEvent> tickEvents)
        {
            if (p.TickIntervalFrames <= 0) return;
            if (p.NextTickFrame <= 0) p.NextTickFrame = frame;
            if (frame < p.NextTickFrame) return;

            tickEvents?.Add(new ProjectileTickEvent(p.Id, p.OwnerId, p.TemplateId, p.LauncherActorId, p.RootActorId, frame, p.Position.ToVec3()));
            p.NextTickFrame = frame + p.TickIntervalFrames;
        }

        private bool TryResolveReturnTarget(int launcherActorId, int rootActorId, out Vec3 position)
        {
            position = Vec3.Zero;
            if (_returnTargetProvider == null) return false;

            if (launcherActorId > 0 && _returnTargetProvider.TryGetReturnTargetPosition(launcherActorId, out position))
            {
                return true;
            }

            return rootActorId > 0 && rootActorId != launcherActorId && _returnTargetProvider.TryGetReturnTargetPosition(rootActorId, out position);
        }

        private Projectile Find(ProjectileId id)
        {
            if (id.Value <= 0) return null;
            for (var i = 0; i < _active.Count; i++)
            {
                var projectile = _active[i];
                if (projectile != null && projectile.Id.Equals(id)) return projectile;
            }

            return null;
        }

        private void RemoveAtSwapBack(int index)
        {
            var last = _active.Count - 1;
            var p = _active[index];

            if (index != last)
            {
                _active[index] = _active[last];
            }
            _active.RemoveAt(last);

            if (p != null)
            {
                Pool.Release(p);
            }
        }

        private static ProjectileCollisionResponse ResolveCollisionResponse(Projectile projectile, ColliderId collider, int frame)
        {
            if (projectile.HitFilter is IProjectileCollisionResponseResolver resolver)
            {
                return resolver.ResolveCollision(projectile.OwnerId, collider, frame);
            }

            return projectile.HitFilter == null || projectile.HitFilter.ShouldHit(projectile.OwnerId, collider, frame)
                ? ProjectileCollisionResponse.Hit
                : ProjectileCollisionResponse.Ignore;
        }


    }



    [MemoryPackable]
    public readonly partial struct ProjectileWorldSnapshotPayload
    {
        [MemoryPackOrder(0)] public readonly int Version;
        [MemoryPackOrder(1)] public readonly FrameIndex Frame;
        [MemoryPackOrder(2)] public readonly int NextId;
        [MemoryPackOrder(3)] public readonly ProjectileWorldSnapshotItem[] Items;
        [MemoryPackOrder(4)] public readonly int NextHitFilterKey;

        public ProjectileWorldSnapshotPayload(int version, FrameIndex frame, int nextId, ProjectileWorldSnapshotItem[] items, int nextHitFilterKey = 1)
        {
            Version = version;
            Frame = frame;
            NextId = nextId;
            Items = items;
            NextHitFilterKey = nextHitFilterKey;
        }
    }


    /// <summary>
    /// 回滚快照条目（schema v14）。定点字段以 Q32.32 raw long 存储，跨平台位一致；
    /// Vec3/float 不再直接进入快照。
    /// </summary>
    [MemoryPackable]
    public readonly partial struct ProjectileWorldSnapshotItem
    {
        [MemoryPackOrder(0)] public readonly int Id;
        [MemoryPackOrder(1)] public readonly int OwnerId;
        [MemoryPackOrder(2)] public readonly long PositionX;
        [MemoryPackOrder(3)] public readonly long PositionY;
        [MemoryPackOrder(4)] public readonly long PositionZ;
        [MemoryPackOrder(5)] public readonly long DirectionX;
        [MemoryPackOrder(6)] public readonly long DirectionY;
        [MemoryPackOrder(7)] public readonly long DirectionZ;
        [MemoryPackOrder(8)] public readonly long SpeedRaw;
        [MemoryPackOrder(9)] public readonly int LifetimeFramesLeft;
        [MemoryPackOrder(10)] public readonly long DistanceLeftRaw;
        [MemoryPackOrder(11)] public readonly int CollisionLayerMask;
        [MemoryPackOrder(12)] public readonly int IgnoreCollider;
        [MemoryPackOrder(13)] public readonly int HitsRemaining;
        [MemoryPackOrder(14)] public readonly ProjectileHitPolicyKind HitPolicyKind;
        [MemoryPackOrder(15)] public readonly int HitPolicyParam;
        [MemoryPackOrder(16)] public readonly int TickIntervalFrames;
        [MemoryPackOrder(17)] public readonly int NextTickFrame;

        [MemoryPackOrder(18)] public readonly int TemplateId;
        [MemoryPackOrder(19)] public readonly int LauncherActorId;
        [MemoryPackOrder(20)] public readonly int RootActorId;
        [MemoryPackOrder(21)] public readonly int SpawnFrame;
        [MemoryPackOrder(22)] public readonly int ReturnAfterFrames;
        [MemoryPackOrder(23)] public readonly long ReturnSpeedRaw;
        [MemoryPackOrder(24)] public readonly long ReturnStopDistanceRaw;
        [MemoryPackOrder(25)] public readonly int IsReturning;
        [MemoryPackOrder(26)] public readonly int IsSuspended;
        [MemoryPackOrder(27)] public readonly int PatternSlotIndex;
        [MemoryPackOrder(28)] public readonly int PatternSlotCount;
        [MemoryPackOrder(29)] public readonly int TrackingTargetActorId;
        [MemoryPackOrder(30)] public readonly long HalfExtentsX;
        [MemoryPackOrder(31)] public readonly long HalfExtentsY;
        [MemoryPackOrder(32)] public readonly long HalfExtentsZ;
        [MemoryPackOrder(33)] public readonly long TrackingArcHeightRaw;
        [MemoryPackOrder(34)] public readonly long TrackingArcDurationRaw;
        [MemoryPackOrder(35)] public readonly long TrackingArcElapsedRaw;
        [MemoryPackOrder(36)] public readonly long TrackingArcStartYRaw;
        [MemoryPackOrder(37)] public readonly long TrackingArcEndYRaw;
        [MemoryPackOrder(38)] public readonly int[] HitActorIds;
        [MemoryPackOrder(39)] public readonly int TrackingArcDurationConfigured;
        [MemoryPackOrder(40)] public readonly long BounceSearchRadiusRaw;
        [MemoryPackOrder(41)] public readonly long TrackingArcHeightPerUnitRaw;
        [MemoryPackOrder(42)] public readonly long TrackingArcBaseHeightRaw;
        [MemoryPackOrder(43)] public readonly int TotalHitCount;
        [MemoryPackOrder(44)] public readonly int HitFilterKey;
        [MemoryPackOrder(45)] public readonly int HitCooldownFrames;
        [MemoryPackOrder(46)] public readonly int LastHitCollider;
        [MemoryPackOrder(47)] public readonly int LastHitAllowedFrame;
        [MemoryPackOrder(48)] public readonly long SpawnPositionX;
        [MemoryPackOrder(49)] public readonly long SpawnPositionY;
        [MemoryPackOrder(50)] public readonly long SpawnPositionZ;
        [MemoryPackOrder(51)] public readonly long DistanceTravelledRaw;
        [MemoryPackOrder(52)] public readonly long ElapsedSecondsRaw;
        [MemoryPackOrder(53)] public readonly ProjectileHitIdentityPolicy HitIdentityPolicy;
        [MemoryPackOrder(54)] public readonly byte[] HitFilterSnapshot;
        [MemoryPackOrder(55)] public readonly ProjectileMaxDistanceResponse MaxDistanceResponse;
        [MemoryPackOrder(56)] public readonly int ArrivedFrame;
        [MemoryPackOrder(57)] public readonly int[] ReturnHitActorIds;

        public ProjectileWorldSnapshotItem(
            int id,
            int ownerId,
            long positionX,
            long positionY,
            long positionZ,
            long directionX,
            long directionY,
            long directionZ,
            long speedRaw,
            int lifetimeFramesLeft,
            long distanceLeftRaw,
            int collisionLayerMask,
            int ignoreCollider,
            long halfExtentsX,
            long halfExtentsY,
            long halfExtentsZ,
            int hitsRemaining,
            ProjectileHitPolicyKind hitPolicyKind,
            int hitPolicyParam,
            int tickIntervalFrames,
            int nextTickFrame,
            int templateId,
            int launcherActorId,
            int rootActorId,
            int spawnFrame,
            int returnAfterFrames,
            long returnSpeedRaw,
            long returnStopDistanceRaw,
            int isReturning,
            int isSuspended,
            int patternSlotIndex,
            int patternSlotCount,
            int trackingTargetActorId,
            long trackingArcHeightRaw = 0,
            long trackingArcDurationRaw = 0,
            long trackingArcElapsedRaw = 0,
            long trackingArcStartYRaw = 0,
            long trackingArcEndYRaw = 0,
            int[] hitActorIds = null,
            int trackingArcDurationConfigured = 0,
            long bounceSearchRadiusRaw = 0,
            long trackingArcHeightPerUnitRaw = 0,
            long trackingArcBaseHeightRaw = 0,
            int totalHitCount = 0,
            int hitFilterKey = 0,
            int hitCooldownFrames = 0,
            int lastHitCollider = 0,
            int lastHitAllowedFrame = 0,
            long spawnPositionX = 0,
            long spawnPositionY = 0,
            long spawnPositionZ = 0,
            long distanceTravelledRaw = 0,
            long elapsedSecondsRaw = 0,
            ProjectileHitIdentityPolicy hitIdentityPolicy = ProjectileHitIdentityPolicy.Collider,
            byte[] hitFilterSnapshot = null,
            ProjectileMaxDistanceResponse maxDistanceResponse = ProjectileMaxDistanceResponse.Exit, int arrivedFrame = -1, int[] returnHitActorIds = null)
        {
            Id = id;
            OwnerId = ownerId;
            PositionX = positionX;
            PositionY = positionY;
            PositionZ = positionZ;
            DirectionX = directionX;
            DirectionY = directionY;
            DirectionZ = directionZ;
            SpeedRaw = speedRaw;
            LifetimeFramesLeft = lifetimeFramesLeft;
            DistanceLeftRaw = distanceLeftRaw;
            CollisionLayerMask = collisionLayerMask;
            IgnoreCollider = ignoreCollider;
            HalfExtentsX = halfExtentsX;
            HalfExtentsY = halfExtentsY;
            HalfExtentsZ = halfExtentsZ;
            HitsRemaining = hitsRemaining;
            HitPolicyKind = hitPolicyKind;
            HitPolicyParam = hitPolicyParam;
            TickIntervalFrames = tickIntervalFrames;
            NextTickFrame = nextTickFrame;
            TemplateId = templateId;
            LauncherActorId = launcherActorId;
            RootActorId = rootActorId;
            SpawnFrame = spawnFrame;
            ReturnAfterFrames = returnAfterFrames;
            ReturnSpeedRaw = returnSpeedRaw;
            ReturnStopDistanceRaw = returnStopDistanceRaw;
            IsReturning = isReturning;
            IsSuspended = isSuspended;
            PatternSlotIndex = patternSlotIndex;
            PatternSlotCount = patternSlotCount;
            TrackingTargetActorId = trackingTargetActorId;
            TrackingArcHeightRaw = trackingArcHeightRaw;
            TrackingArcDurationRaw = trackingArcDurationRaw;
            TrackingArcElapsedRaw = trackingArcElapsedRaw;
            TrackingArcStartYRaw = trackingArcStartYRaw;
            TrackingArcEndYRaw = trackingArcEndYRaw;
            HitActorIds = hitActorIds;
            TrackingArcDurationConfigured = trackingArcDurationConfigured;
            BounceSearchRadiusRaw = bounceSearchRadiusRaw;
            TrackingArcHeightPerUnitRaw = trackingArcHeightPerUnitRaw;
            TrackingArcBaseHeightRaw = trackingArcBaseHeightRaw;
            TotalHitCount = totalHitCount;
            HitFilterKey = hitFilterKey;
            HitCooldownFrames = hitCooldownFrames;
            LastHitCollider = lastHitCollider;
            LastHitAllowedFrame = lastHitAllowedFrame;
            SpawnPositionX = spawnPositionX;
            SpawnPositionY = spawnPositionY;
            SpawnPositionZ = spawnPositionZ;
            DistanceTravelledRaw = distanceTravelledRaw;
            ElapsedSecondsRaw = elapsedSecondsRaw;
            HitIdentityPolicy = hitIdentityPolicy;
            HitFilterSnapshot = hitFilterSnapshot;
            MaxDistanceResponse = maxDistanceResponse; ArrivedFrame = arrivedFrame; ReturnHitActorIds = returnHitActorIds;
        }
    }

}
