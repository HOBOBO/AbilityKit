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

    public sealed class ProjectileWorld
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

        private readonly ICollisionWorld _collision;
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
            _collision = collision ?? throw new ArgumentNullException(nameof(collision));
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
            if (policy != ProjectileHitIdentityPolicy.Collider && policy != ProjectileHitIdentityPolicy.OncePerActor)
                throw new ArgumentOutOfRangeException(nameof(policy));
            if (policy == ProjectileHitIdentityPolicy.OncePerActor && _actorIdentityProvider == null)
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
                p.MaxDistanceResponse != ProjectileMaxDistanceResponse.SuspendAtDestination)
                throw new ArgumentOutOfRangeException(nameof(p.MaxDistanceResponse));
            if (p.MaxDistanceResponse == ProjectileMaxDistanceResponse.SuspendAtDestination &&
                (!(p.MaxDistance > 0) || float.IsInfinity(p.MaxDistance)))
                throw new ArgumentOutOfRangeException(nameof(p.MaxDistance));
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
                projectile.ElapsedSeconds.ToSingle(), projectile.ArrivedFrame);
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

        public byte[] ExportRollback(FrameIndex frame)
        {
            var items = new ProjectileWorldSnapshotItem[_active.Count];
            for (int i = 0; i < _active.Count; i++)
            {
                var p = _active[i];
                if (p == null) continue;
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
                    maxDistanceResponse: p.MaxDistanceResponse, arrivedFrame: p.ArrivedFrame,
                    hitFilterSnapshot: (p.HitFilter as IProjectileHitFilterSnapshotSource)?.CaptureSnapshot()
                );
            }

            return MemoryPackSerializer.Serialize(new ProjectileWorldSnapshotPayload(
                version: 17,
                frame: frame,
                nextId: _nextId,
                items: items,
                nextHitFilterKey: _nextHitFilterKey
            ));
        }

        public void ImportRollback(FrameIndex frame, byte[] payload)
        {
            if (payload == null || payload.Length == 0) { Clear(); return; }
            var snap = MemoryPackSerializer.Deserialize<ProjectileWorldSnapshotPayload>(payload);
            if (snap.Version != 17) throw new InvalidOperationException("Unsupported projectile rollback version.");
            foreach (var item in snap.Items ?? Array.Empty<ProjectileWorldSnapshotItem>())
            {
                if ((item.MaxDistanceResponse != ProjectileMaxDistanceResponse.Exit &&
                     item.MaxDistanceResponse != ProjectileMaxDistanceResponse.SuspendAtDestination) ||
                    item.ArrivedFrame < -1 || (item.ArrivedFrame >= 0 &&
                    (item.MaxDistanceResponse != ProjectileMaxDistanceResponse.SuspendAtDestination || item.IsSuspended == 0)))
                    throw new InvalidOperationException("Invalid projectile destination snapshot.");
            }
            Clear();
            _nextId = snap.NextId <= 0 ? 1 : snap.NextId;
            _nextHitFilterKey = snap.NextHitFilterKey <= 0 ? 1 : snap.NextHitFilterKey;
            TrimHitFilters(_nextHitFilterKey);

            if (snap.Items == null || snap.Items.Length == 0) return;

            for (int i = 0; i < snap.Items.Length; i++)
            {
                var it = snap.Items[i];
                if (it.Id <= 0) continue;
                ValidateHitIdentity(it.HitIdentityPolicy);
                var restoredFilter = RestoreHitFilter(it.HitFilterKey, it.HitFilterSnapshot);

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
                p.ReturnAfterFrames = it.ReturnAfterFrames;
                p.ReturnSpeed = Fixed64.FromRaw(it.ReturnSpeedRaw);
                p.ReturnStopDistance = Fixed64.FromRaw(it.ReturnStopDistanceRaw);
                p.IsReturning = it.IsReturning != 0;
                p.LifetimeFramesLeft = it.LifetimeFramesLeft > 0 ? it.LifetimeFramesLeft : int.MaxValue;
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
                p.HitPolicy = ProjectileHitPolicyFactory.Create(it.HitPolicyKind, it.HitPolicyParam);
                p.TickIntervalFrames = it.TickIntervalFrames;
                p.NextTickFrame = it.NextTickFrame;
                p.TotalHitCount = it.TotalHitCount;
                p.HitFilterKey = it.HitFilterKey;
                p.HitFilter = restoredFilter;
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

        private IProjectileHitFilter RestoreHitFilter(int key, byte[] snapshot)
        {
            if (snapshot == null || snapshot.Length == 0) return ResolveHitFilter(key);
            if (key <= 0 || !(_hitFilterResolver is IProjectileHitFilterSnapshotResolver resolver))
                throw new InvalidOperationException("Projectile filter snapshot requires a snapshot resolver and a positive filter key.");
            var filter = resolver.ResolveSnapshot(snapshot);
            if (filter == null) throw new InvalidOperationException("Projectile filter snapshot resolver returned no filter.");
            if (_hitFilters.TryGetValue(key, out var previous)) _hitFilterKeys.Remove(previous);
            _hitFilters[key] = filter;
            _hitFilterKeys[filter] = key;
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
                var p = _active[i];
                if (p == null)
                {
                    RemoveAtSwapBack(i);
                    i--;
                    continue;
                }

                if (p.IsSuspended)
                {
                    EmitTickIfDue(p, frame, tickEvents);
                    continue;
                }

                if (p.LifetimeFramesLeft <= 0)
                {
                    exitEvents?.Add(new ProjectileExitEvent(p.Id, p.OwnerId, p.TemplateId, p.LauncherActorId, p.RootActorId, ProjectileExitReason.Lifetime, frame, p.Position.ToVec3()));
                    RemoveAtSwapBack(i);
                    i--;
                    continue;
                }

                // 返回发射者逻辑（服务器权威）。MaxDistance 只约束出程，回程由 ReturnArrived/Lifetime 结束。
                if (!p.IsReturning && p.ReturnAfterFrames > 0 && frame - p.SpawnFrame >= p.ReturnAfterFrames)
                {
                    p.IsReturning = true;
                    p.DistanceLeft = Fixed64.Zero;
                }

                var hasTrackingTarget = false;
                if (p.IsReturning)
                {
                    if (_returnTargetProvider == null ||
                        !TryResolveReturnTarget(p.LauncherActorId, p.RootActorId, out var targetPos))
                    {
                        exitEvents?.Add(new ProjectileExitEvent(p.Id, p.OwnerId, p.TemplateId, p.LauncherActorId, p.RootActorId, ProjectileExitReason.ReturnTargetLost, frame, p.Position.ToVec3()));
                        RemoveAtSwapBack(i);
                        i--;
                        continue;
                    }

                    var returnTarget = targetPos.ToFixed();

                    if (p.ReturnStopDistance > Fixed64.Zero)
                    {
                        var delta = returnTarget - p.Position;
                        var sqr = delta.SqrMagnitude;
                        var stopSqr = p.ReturnStopDistance * p.ReturnStopDistance;
                        if (sqr <= stopSqr)
                        {
                            exitEvents?.Add(new ProjectileExitEvent(p.Id, p.OwnerId, p.TemplateId, p.LauncherActorId, p.RootActorId, ProjectileExitReason.ReturnArrived, frame, p.Position.ToVec3()));
                            RemoveAtSwapBack(i);
                            i--;
                            continue;
                        }
                    }

                    var to = returnTarget - p.Position;
                    if (to.SqrMagnitude > Fixed64.Zero)
                    {
                        p.Direction = to.Normalized;
                    }
                }

                else if (p.TrackingTargetActorId > 0)
                {
                    if (_trackingTargetProvider == null ||
                        !_trackingTargetProvider.TryGetTrackingTargetPosition(p.TrackingTargetActorId, out var targetPos))
                    {
                        exitEvents?.Add(new ProjectileExitEvent(p.Id, p.OwnerId, p.TemplateId, p.LauncherActorId, p.RootActorId, ProjectileExitReason.TrackingTargetLost, frame, p.Position.ToVec3()));
                        RemoveAtSwapBack(i);
                        i--;
                        continue;
                    }

                    var to = targetPos.ToFixed() - p.Position;
                    hasTrackingTarget = true;
                    to = new FixedVec3(to.X, Fixed64.Zero, to.Z);
                    if (p.TrackingArcHeightPerUnit > Fixed64.Zero || p.TrackingArcBaseHeight > Fixed64.Zero)
                    {
                        if (!p.TrackingArcDurationConfigured && p.TrackingArcDuration <= Fixed64.Zero && p.Speed > Fixed64.Zero)
                        {
                            p.TrackingArcStartY = p.Position.Y;
                            p.TrackingArcElapsed = Fixed64.Zero;
                            p.TrackingArcDuration = to.Magnitude / p.Speed;
                        }
                        p.TrackingArcEndY = targetPos.Y.ToFixed();
                        // Freeze one distance-scaled arc height for this segment. Target motion
                        // changes the endpoint, but does not make the arc height oscillate per tick.
                        if (p.TrackingArcElapsed <= Fixed64.Zero && p.TrackingArcHeightPerUnit > Fixed64.Zero)
                            p.TrackingArcHeight = p.TrackingArcBaseHeight + to.Magnitude * p.TrackingArcHeightPerUnit;
                    }
                    if (to.SqrMagnitude > Fixed64.Zero)
                    {
                        p.Direction = to.Normalized;
                    }
                }

                var speed = (p.IsReturning && p.ReturnSpeed > Fixed64.Zero) ? p.ReturnSpeed : p.Speed;
                var move = speed * fixedDeltaSeconds;
                var hasDistanceLimit = p.DistanceLeft > Fixed64.Zero;
                if (move <= Fixed64.Zero)
                {
                    p.LifetimeFramesLeft--;
                    continue;
                }

                var dir = p.Direction;
                var hasTrackingArc = hasTrackingTarget && p.TrackingArcHeight > Fixed64.Zero && p.TrackingArcDuration > Fixed64.Zero;
                var arcDeltaY = Fixed64.Zero;
                var arcProgressStart = Fixed64.Zero;
                var arcProgressEnd = Fixed64.Zero;
                if (hasTrackingArc)
                {
                    arcProgressStart = p.TrackingArcElapsed / p.TrackingArcDuration;
                    p.TrackingArcElapsed += fixedDeltaSeconds;
                    var progress = p.TrackingArcElapsed / p.TrackingArcDuration;
                    if (progress > Fixed64.One) progress = Fixed64.One;
                    arcProgressEnd = progress;
                    var currentArc = Fixed64.FromInt32(4) * p.TrackingArcHeight * progress * (Fixed64.One - progress);
                    var currentY = p.TrackingArcStartY + (p.TrackingArcEndY - p.TrackingArcStartY) * progress + currentArc;
                    arcDeltaY = currentY - p.Position.Y;
                    var horizontalDirection = new FixedVec3(dir.X, Fixed64.Zero, dir.Z);
                    if (horizontalDirection.SqrMagnitude > Fixed64.Zero)
                        dir = horizontalDirection.Normalized;
                }

                if (p.DistanceLeft > Fixed64.Zero && move > p.DistanceLeft)
                {
                    move = p.DistanceLeft;
                }

                if (hasTrackingArc && move > Fixed64.Zero)
                {
                    var horizontalStep = new FixedVec3(dir.X * move, Fixed64.Zero, dir.Z * move);
                    var arcStep = new FixedVec3(horizontalStep.X, arcDeltaY, horizontalStep.Z);
                    var arcDistance = arcStep.Magnitude;
                    if (p.DistanceLeft > Fixed64.Zero && arcDistance > p.DistanceLeft)
                    {
                        var budgetScale = p.DistanceLeft / arcDistance;
                        arcStep = arcStep * budgetScale;
                        arcDistance = p.DistanceLeft;
                    }
                    if (arcDistance > Fixed64.Zero)
                    {
                        dir = arcStep / arcDistance;
                        move = arcDistance;
                        p.Direction = dir;
                    }
                }

                var arcSweepPending = hasTrackingArc && move > Fixed64.Zero;
                var arcSweepStart = p.Position;
                var arcSweepEnd = p.Position + dir * move;

                var remaining = move;

                // 单帧内允许多次命中（穿透），同时保持确定性上限。
                const int maxHitsPerStep = 8;
                var hitCount = 0;
                var origin = p.Position;

                // 防止同一帧内对同一个碰撞体重复触发命中回调。
                // 这样可保留“返回过程可跨帧多次命中同一目标”的行为，
                // 同时避免单帧内多段射线检测造成重复触发。
                var hitColliderIdsThisTick = p.HitColliderIdsThisTick;
                Array.Clear(hitColliderIdsThisTick, 0, hitColliderIdsThisTick.Length);
                var hitColliderCount = 0;
                var skippedDistanceThisTick = Fixed64.Zero;
                if (p.IgnoreCollider.Value != 0)
                {
                    hitColliderIdsThisTick[hitColliderCount++] = p.IgnoreCollider.Value;
                }

                while (remaining > Fixed64.Zero)
                {
                    RaycastHit hit;
                    Fixed64 sweptDistance;
                    if (arcSweepPending)
                    {
                        arcSweepPending = false;
                        if (TrySweepTrackingArcSegments(
                                arcSweepStart,
                                arcSweepEnd,
                                arcProgressStart,
                                arcProgressEnd,
                                p.TrackingArcHeight,
                                p.TrackingArcStartY,
                                p.TrackingArcEndY,
                                p.CollisionLayerMask,
                                hitColliderIdsThisTick,
                                hitColliderCount,
                                p.CollisionHalfExtents,
                                out hit,
                                out sweptDistance,
                                out var segmentDirection))
                        {
                            dir = segmentDirection;
                        }
                        else
                        {
                            origin = arcSweepEnd;
                            remaining = Fixed64.Zero;
                            break;
                        }
                    }
                    else if (!TrySweepSkippingIgnored(origin, dir, remaining, p.CollisionLayerMask, hitColliderIdsThisTick, hitColliderCount, p.CollisionHalfExtents, out hit))
                    {
                        // 剩余线段内没有命中。
                        origin = origin + dir * remaining;
                        remaining = Fixed64.Zero;
                        break;
                    }
                    else
                    {
                        sweptDistance = Fixed64.FromSingle(hit.Distance);
                    }

                    var cumulativeHitDistance = p.DistanceTravelled + skippedDistanceThisTick + sweptDistance;
                    var elapsedAtHit = p.ElapsedSeconds + EstimateElapsedWithinTick(
                        fixedDeltaSeconds, skippedDistanceThisTick + sweptDistance, move);
                    var hitEvt = new ProjectileHitEvent(p.Id, p.OwnerId, p.TemplateId, p.LauncherActorId, p.RootActorId, hit.Collider, hit.Distance, hit.Point, hit.Normal, frame, hitCount: 0,
                        spawnPosition: p.SpawnPosition.ToVec3(), distanceTravelled: cumulativeHitDistance.ToSingle(), elapsedFrames: Math.Max(0, frame - p.SpawnFrame), spawnFrame: p.SpawnFrame, elapsedSeconds: elapsedAtHit.ToSingle());
                    var collisionResponse = ResolveCollisionResponse(p, hit.Collider, frame);

                    if (collisionResponse == ProjectileCollisionResponse.Block)
                    {
                        exitEvents?.Add(new ProjectileExitEvent(p.Id, p.OwnerId, p.TemplateId, p.LauncherActorId, p.RootActorId, ProjectileExitReason.Hit, frame, hit.Point));
                        RemoveAtSwapBack(i);
                        i--;
                        goto NextProjectile;
                    }

                    // 命中过滤和按碰撞体冷却。
                    if (collisionResponse == ProjectileCollisionResponse.Ignore)
                    {
                        skippedDistanceThisTick += sweptDistance + EpsilonAdvance;
                        if (hitColliderCount < hitColliderIdsThisTick.Length)
                        {
                            hitColliderIdsThisTick[hitColliderCount++] = hit.Collider.Value;
                        }

                        origin = hit.Point.ToFixed() + dir * EpsilonAdvance;
                        remaining -= sweptDistance + EpsilonAdvance;
                        hitCount++;
                        if (hitCount >= maxHitsPerStep || remaining <= Fixed64.Zero)
                        {
                            remaining = Fixed64.Zero;
                            break;
                        }
                        continue;
                    }

                    if (p.HitCooldownFrames > 0 && hit.Collider.Equals(p.LastHitCollider) && frame < p.LastHitAllowedFrame)
                    {
                        skippedDistanceThisTick += sweptDistance + EpsilonAdvance;
                        if (hitColliderCount < hitColliderIdsThisTick.Length)
                        {
                            hitColliderIdsThisTick[hitColliderCount++] = hit.Collider.Value;
                        }

                        origin = hit.Point.ToFixed() + dir * EpsilonAdvance;
                        remaining -= sweptDistance + EpsilonAdvance;
                        hitCount++;
                        if (hitCount >= maxHitsPerStep || remaining <= Fixed64.Zero)
                        {
                            remaining = Fixed64.Zero;
                            break;
                        }
                        continue;
                    }

                    // Bounce and opt-in identity policies exclude previously accepted actors.
                    // A target can own multiple colliders, and the next tracking arc starts at
                    // the previous hit point, so suppress every collider that resolves to an
                    // actor already in the bounce history before applying hit policy again.
                    var candidateActorId = 0;
                    var identityProvider = p.HitIdentityPolicy == ProjectileHitIdentityPolicy.OncePerActor
                        ? _actorIdentityProvider : p.HitPolicyKind == ProjectileHitPolicyKind.Bounce ? _bounceTargetProvider : null;
                    var hasActorIdentity = identityProvider != null &&
                        identityProvider.TryResolveActorId(hit.Collider, out candidateActorId) && candidateActorId > 0;
                    if (hasActorIdentity && p.HitActorIds.Contains(candidateActorId))
                    {
                        skippedDistanceThisTick += sweptDistance + EpsilonAdvance;
                        if (hitColliderCount < hitColliderIdsThisTick.Length)
                        {
                            hitColliderIdsThisTick[hitColliderCount++] = hit.Collider.Value;
                        }

                        origin = hit.Point.ToFixed() + dir * EpsilonAdvance;
                        remaining -= sweptDistance + EpsilonAdvance;
                        hitCount++;
                        if (hitCount >= maxHitsPerStep || remaining <= Fixed64.Zero)
                        {
                            remaining = Fixed64.Zero;
                            break;
                        }
                        continue;
                    }

                    var alreadyHitThisTick = false;
                    for (int hc = 0; hc < hitColliderCount; hc++)
                    {
                        if (hitColliderIdsThisTick[hc] == hit.Collider.Value)
                        {
                            alreadyHitThisTick = true;
                            break;
                        }
                    }

                    if (alreadyHitThisTick)
                    {
                        skippedDistanceThisTick += sweptDistance + EpsilonAdvance;
                        origin = hit.Point.ToFixed() + dir * EpsilonAdvance;
                        remaining -= sweptDistance + EpsilonAdvance;
                        hitCount++;
                        if (hitCount >= maxHitsPerStep || remaining <= Fixed64.Zero)
                        {
                            remaining = Fixed64.Zero;
                            break;
                        }
                        continue;
                    }

                    if (hitColliderCount < hitColliderIdsThisTick.Length)
                    {
                        hitColliderIdsThisTick[hitColliderCount++] = hit.Collider.Value;
                    }

                    if (hasActorIdentity) p.HitActorIds.Add(candidateActorId);
                    p.TotalHitCount++;
                    var hitDistance = p.DistanceTravelled + skippedDistanceThisTick + sweptDistance;
                    var elapsedAtAcceptedHit = p.ElapsedSeconds + EstimateElapsedWithinTick(
                        fixedDeltaSeconds, skippedDistanceThisTick + sweptDistance, move);
                    hitEvents?.Add(new ProjectileHitEvent(p.Id, p.OwnerId, p.TemplateId, p.LauncherActorId, p.RootActorId, hit.Collider, hit.Distance, hit.Point, hit.Normal, frame, p.TotalHitCount,
                        p.SpawnPosition.ToVec3(), hitDistance.ToSingle(), Math.Max(0, frame - p.SpawnFrame), p.SpawnFrame, elapsedAtAcceptedHit.ToSingle()));
                    if (p.HitCooldownFrames > 0)
                    {
                        p.LastHitCollider = hit.Collider;
                        p.LastHitAllowedFrame = frame + p.HitCooldownFrames;
                    }

                    var hitsRemaining = p.HitsRemaining;
                    var shouldExit = (p.HitPolicy ?? ExitOnHitPolicy.Instance).ShouldExitOnHit(in hitEvt, ref hitsRemaining);
                    p.HitsRemaining = hitsRemaining;

                    if (shouldExit)
                    {
                        exitEvents?.Add(new ProjectileExitEvent(p.Id, p.OwnerId, p.TemplateId, p.LauncherActorId, p.RootActorId, ProjectileExitReason.Hit, frame, hit.Point));
                        RemoveAtSwapBack(i);
                        i--;
                        goto NextProjectile;
                    }

                    // 命中后继续推进到命中点之后。
                    if (p.HitPolicyKind == ProjectileHitPolicyKind.Bounce)
                    {
                        if (_bounceTargetProvider == null ||
                            !_bounceTargetProvider.TryResolveActorId(hit.Collider, out var hitActorId) || hitActorId <= 0)
                        {
                            exitEvents?.Add(new ProjectileExitEvent(p.Id, p.OwnerId, p.TemplateId, p.LauncherActorId, p.RootActorId, ProjectileExitReason.Hit, frame, hit.Point));
                            RemoveAtSwapBack(i);
                            i--;
                            goto NextProjectile;
                        }

                        if (!p.HitActorIds.Contains(hitActorId)) p.HitActorIds.Add(hitActorId);
                        if (!_bounceTargetProvider.TrySelectNextTarget(p.OwnerId, hitActorId, hit.Point, p.HitActorIds,
                                p.BounceSearchRadius.ToSingle(),
                                out var nextActorId, out var nextTargetPosition) ||
                            nextActorId <= 0 || p.HitActorIds.Contains(nextActorId))
                        {
                            exitEvents?.Add(new ProjectileExitEvent(p.Id, p.OwnerId, p.TemplateId, p.LauncherActorId, p.RootActorId, ProjectileExitReason.Hit, frame, hit.Point));
                            RemoveAtSwapBack(i);
                            i--;
                            goto NextProjectile;
                        }

                        var nextDirection = nextTargetPosition.ToFixed() - hit.Point.ToFixed();
                        if (nextDirection.SqrMagnitude <= Fixed64.Zero)
                        {
                            exitEvents?.Add(new ProjectileExitEvent(p.Id, p.OwnerId, p.TemplateId, p.LauncherActorId, p.RootActorId, ProjectileExitReason.Hit, frame, hit.Point));
                            RemoveAtSwapBack(i);
                            i--;
                            goto NextProjectile;
                        }

                        var fixedArcDuration = p.TrackingArcDuration;
                        var arcDurationConfigured = p.TrackingArcDurationConfigured;
                        var nextPlanar = new FixedVec3(
                            nextTargetPosition.X.ToFixed() - hit.Point.ToFixed().X,
                            Fixed64.Zero,
                            nextTargetPosition.Z.ToFixed() - hit.Point.ToFixed().Z);
                        p.TrackingTargetActorId = nextActorId;
                        p.TrackingArcDurationConfigured = arcDurationConfigured;
                        if (arcDurationConfigured)
                        {
                            p.TrackingArcDuration = fixedArcDuration;
                        }
                        else if (p.Speed > Fixed64.Zero)
                        {
                            p.TrackingArcDuration = nextPlanar.Magnitude / p.Speed;
                        }
                        else
                        {
                            p.TrackingArcDuration = Fixed64.Zero;
                        }
                        p.TrackingArcElapsed = Fixed64.Zero;
                        p.TrackingArcStartY = hit.Point.ToFixed().Y;
                        p.TrackingArcEndY = nextTargetPosition.Y.ToFixed();
                        p.TrackingArcHeight = p.TrackingArcBaseHeight + nextPlanar.Magnitude * p.TrackingArcHeightPerUnit;
                        p.Direction = nextDirection.Normalized;
                        dir = p.Direction;
                        arcSweepPending = false;
                        if (p.TrackingArcHeight > Fixed64.Zero)
                        {
                            origin = hit.Point.ToFixed();
                            remaining = Fixed64.Zero;
                            break;
                        }
                    }

                    origin = hit.Point.ToFixed() + dir * EpsilonAdvance;
                    remaining -= sweptDistance + EpsilonAdvance;
                    hitCount++;
                    if (hitCount >= maxHitsPerStep || remaining <= Fixed64.Zero)
                    {
                        // 避免无限循环，本帧停止处理。
                        remaining = Fixed64.Zero;
                        break;
                    }
                }

                p.Position = origin;
                p.LifetimeFramesLeft--;
                p.ElapsedSeconds += fixedDeltaSeconds;

                // 移动后发送周期性 Tick 事件。
                EmitTickIfDue(p, frame, tickEvents);

                var travelled = (p.Position - arcSweepStart).Magnitude;
                p.DistanceTravelled += travelled;
                if (hasDistanceLimit)
                {
                    p.DistanceLeft -= travelled;
                    if (p.DistanceLeft <= EpsilonAdvance)
                    {
                        if (p.MaxDistanceResponse == ProjectileMaxDistanceResponse.SuspendAtDestination)
                        {
                            p.DistanceLeft = Fixed64.Zero;
                            p.ArrivedFrame = frame;
                            p.IsSuspended = true;
                            arrivalEvents?.Add(new ProjectileArrivalEvent(p.Id, p.OwnerId, p.TemplateId,
                                p.LauncherActorId, p.RootActorId, frame, p.Position.ToVec3(), p.Direction.ToVec3()));
                            continue;
                        }
                        exitEvents?.Add(new ProjectileExitEvent(p.Id, p.OwnerId, p.TemplateId, p.LauncherActorId, p.RootActorId, ProjectileExitReason.MaxDistance, frame, p.Position.ToVec3()));
                        RemoveAtSwapBack(i);
                        i--;
                        continue;
                    }
                }

            NextProjectile:
                ;
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

        private static Fixed64 EstimateElapsedWithinTick(Fixed64 fixedDeltaSeconds, Fixed64 travelled, Fixed64 stepDistance)
        {
            if (fixedDeltaSeconds <= Fixed64.Zero || travelled <= Fixed64.Zero || stepDistance <= Fixed64.Zero)
                return Fixed64.Zero;
            var ratio = travelled / stepDistance;
            if (ratio < Fixed64.Zero) ratio = Fixed64.Zero;
            if (ratio > Fixed64.One) ratio = Fixed64.One;
            return fixedDeltaSeconds * ratio;
        }

        private bool TrySweepTrackingArcSegments(
            in FixedVec3 start,
            in FixedVec3 end,
            Fixed64 progressStart,
            Fixed64 progressEnd,
            Fixed64 arcHeight,
            Fixed64 arcStartY,
            Fixed64 arcEndY,
            int layerMask,
            int[] ignoredColliderIds,
            int ignoredColliderCount,
            in FixedVec3 halfExtents,
            out RaycastHit hit,
            out Fixed64 distanceAlongPath,
            out FixedVec3 hitDirection)
        {
            // Fixed segment count keeps the collision path deterministic and prevents a high arc
            // from being tested as one long chord. The simulation path remains authoritative.
            const int segmentCount = 16;
            var previous = start;
            var travelled = Fixed64.Zero;
            var endCurveY = EvaluateTrackingArcY(progressEnd, arcHeight, arcStartY, arcEndY);
            var endCorrection = end.Y - endCurveY;

            for (var segment = 1; segment <= segmentCount; segment++)
            {
                var t = Fixed64.FromRatio(segment, segmentCount);
                var progress = progressStart + (progressEnd - progressStart) * t;
                var curveY = EvaluateTrackingArcY(progress, arcHeight, arcStartY, arcEndY);
                var correctedY = curveY + endCorrection * t;
                var point = new FixedVec3(
                    start.X + (end.X - start.X) * t,
                    correctedY,
                    start.Z + (end.Z - start.Z) * t);
                var delta = point - previous;
                var segmentLength = delta.Magnitude;
                if (segmentLength <= Fixed64.Zero)
                {
                    previous = point;
                    continue;
                }

                var direction = delta / segmentLength;
                if (TrySweepSkippingIgnored(previous, direction, segmentLength, layerMask, ignoredColliderIds, ignoredColliderCount, halfExtents, out hit))
                {
                    distanceAlongPath = travelled + Fixed64.FromSingle(hit.Distance);
                    hitDirection = direction;
                    return true;
                }

                travelled += segmentLength;
                previous = point;
            }

            hit = default;
            distanceAlongPath = travelled;
            hitDirection = FixedVec3.Zero;
            return false;
        }

        private static Fixed64 EvaluateTrackingArcY(
            Fixed64 progress,
            Fixed64 arcHeight,
            Fixed64 arcStartY,
            Fixed64 arcEndY)
        {
            if (progress < Fixed64.Zero) progress = Fixed64.Zero;
            if (progress > Fixed64.One) progress = Fixed64.One;
            var arc = Fixed64.FromInt32(4) * arcHeight * progress * (Fixed64.One - progress);
            return arcStartY + (arcEndY - arcStartY) * progress + arc;
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

        private bool TrySweepSkippingIgnored(in FixedVec3 origin, in FixedVec3 dir, Fixed64 maxDistance, int layerMask, int[] ignoredColliderIds, int ignoredColliderCount, in FixedVec3 halfExtents, out RaycastHit hit)
        {
            // 使用固定重试次数，保持确定性并避免无限循环。
            const int maxAttempts = 4;

            var o = origin;
            var remaining = maxDistance;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (!TrySweep(o, dir, remaining, layerMask, ignoredColliderIds, ignoredColliderCount, halfExtents, out hit))
                {
                    hit = default;
                    return false;
                }

                if (!ContainsCollider(ignoredColliderIds, ignoredColliderCount, hit.Collider))
                {
                    return true;
                }

                // 跳过被忽略的命中点，并从命中点稍后位置继续尝试。
                o = hit.Point.ToFixed() + dir * EpsilonAdvance;
                remaining -= Fixed64.FromSingle(hit.Distance) + EpsilonAdvance;
                if (remaining <= Fixed64.Zero)
                {
                    hit = default;
                    return false;
                }
            }

            hit = default;
            return false;
        }

        private bool TrySweep(in FixedVec3 origin, in FixedVec3 dir, Fixed64 maxDistance, int layerMask, int[] ignoredColliderIds, int ignoredColliderCount, in FixedVec3 halfExtents, out RaycastHit hit)
        {
            // 碰撞世界仍是 float 查询面（P2 迁移点）。在边界做一次性确定性换算：
            // 输入来自定点状态（换算位一致），输出（命中距离/点）由 float 查询内部决定，
            // 其确定性归属碰撞包 P2。
            var originVec = origin.ToVec3();
            var dirVec = dir.ToVec3();
            var maxDistanceFloat = maxDistance.ToSingle();

            if (halfExtents.SqrMagnitude > Fixed64.Zero && _collision is IOrientedBoxSweepCollisionWorld boxSweepWorld)
            {
                var forward = dirVec.Normalized;
                var right = Vec3.Cross(Vec3.Up, forward).Normalized;
                if (right.SqrMagnitude <= 0f) right = Vec3.Right;
                var up = Vec3.Cross(forward, right).Normalized;
                var box = new OrientedBoxSweep(originVec, right, up, forward, halfExtents.ToVec3());
                var filter = new LayerFilter(layerMask, ignoredColliderIds);
                return boxSweepWorld.SweepOrientedBox(in box, in forward, maxDistanceFloat, in filter, out hit);
            }

            var ray = new Ray3(originVec, dirVec);
            var filter2 = new LayerFilter(layerMask, ignoredColliderIds);
            return _collision.Raycast(ray, maxDistanceFloat, in filter2, out hit);
        }

        private static bool ContainsCollider(int[] colliderIds, int count, ColliderId collider)
        {
            if (colliderIds == null || count <= 0) return false;
            var limit = count < colliderIds.Length ? count : colliderIds.Length;
            for (var i = 0; i < limit; i++)
            {
                if (colliderIds[i] == collider.Value) return true;
            }
            return false;
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
            ProjectileMaxDistanceResponse maxDistanceResponse = ProjectileMaxDistanceResponse.Exit, int arrivedFrame = -1)
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
            MaxDistanceResponse = maxDistanceResponse; ArrivedFrame = arrivedFrame;
        }
    }

}
