using MemoryPack;
using System;
using System.Collections.Generic;
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

    public sealed class ProjectileWorld
    {
        private static readonly ObjectPool<Projectile> Pool = Pools.GetPool(
            key: "Projectile",
            createFunc: () => new Projectile(),
            defaultCapacity: 0,
            maxSize: 4096);

        // 命中后的穿透推进量与命中点跳步量，定点化后为 1/1000，与旧 float 0.001f 语义一致。
        private static readonly Fixed64 EpsilonAdvance = Fixed64.FromRatio(1, 1000);

        private readonly ICollisionWorld _collision;
        private readonly List<Projectile> _active = new List<Projectile>(128);
        private IProjectileReturnTargetProvider _returnTargetProvider;
        private IProjectileTrackingTargetProvider _trackingTargetProvider;

        private int _nextId = 1;

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

        public int ActiveCount => _active.Count;

        public ProjectileId Spawn(in ProjectileSpawnParams p)
        {
            var proj = Pool.Get();
            proj.Id = new ProjectileId(_nextId++);
            proj.OwnerId = p.OwnerId;
            proj.TemplateId = p.TemplateId;
            proj.LauncherActorId = p.LauncherActorId;
            proj.RootActorId = p.RootActorId;
            proj.SpawnFrame = p.SpawnFrame;
            proj.Position = p.Position.ToFixed();
            proj.Direction = p.Direction.ToFixed();
            proj.Speed = p.Speed.ToFixed();
            proj.TrackingTargetActorId = p.TrackingTargetActorId;
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
            proj.HitPolicy = p.HitPolicy ?? ProjectileHitPolicyFactory.Create(p.HitPolicyKind, p.HitPolicyParam);
            proj.HitsRemaining = p.HitsRemaining;
            proj.TickIntervalFrames = p.TickIntervalFrames;
            proj.NextTickFrame = 0;
            proj.HitFilter = p.HitFilter ?? DefaultProjectileHitFilter.Instance;
            proj.HitCooldownFrames = p.HitCooldownFrames;
            proj.LastHitCollider = default;
            proj.LastHitAllowedFrame = 0;
            proj.IsSuspended = p.StartSuspended;
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
                projectile.IsSuspended);
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
                    returnAfterFrames: p.ReturnAfterFrames,
                    returnSpeedRaw: p.ReturnSpeed.RawValue,
                    returnStopDistanceRaw: p.ReturnStopDistance.RawValue,
                    isReturning: p.IsReturning ? 1 : 0,
                    isSuspended: p.IsSuspended ? 1 : 0,
                    patternSlotIndex: p.PatternSlotIndex,
                    patternSlotCount: p.PatternSlotCount,
                    trackingTargetActorId: p.TrackingTargetActorId
                );
            }

            return MemoryPackSerializer.Serialize(new ProjectileWorldSnapshotPayload(
                version: 7,
                frame: frame,
                nextId: _nextId,
                items: items
            ));
        }

        public void ImportRollback(FrameIndex frame, byte[] payload)
        {
            Clear();
            if (payload == null || payload.Length == 0) return;

            var snap = MemoryPackSerializer.Deserialize<ProjectileWorldSnapshotPayload>(payload);
            _nextId = snap.NextId <= 0 ? 1 : snap.NextId;

            if (snap.Items == null || snap.Items.Length == 0) return;

            for (int i = 0; i < snap.Items.Length; i++)
            {
                var it = snap.Items[i];
                if (it.Id <= 0) continue;

                var p = Pool.Get();
                p.Id = new ProjectileId(it.Id);
                p.OwnerId = it.OwnerId;
                p.TemplateId = it.TemplateId;
                p.LauncherActorId = it.LauncherActorId;
                p.RootActorId = it.RootActorId;
                p.SpawnFrame = it.SpawnFrame;
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
                p.HitPolicyParam = it.HitPolicyParam;
                p.HitPolicy = ProjectileHitPolicyFactory.Create(it.HitPolicyKind, it.HitPolicyParam);
                p.TickIntervalFrames = it.TickIntervalFrames;
                p.NextTickFrame = it.NextTickFrame;
                p.HitFilter = DefaultProjectileHitFilter.Instance;
                p.HitCooldownFrames = 0;
                p.LastHitCollider = default;
                p.LastHitAllowedFrame = 0;
                p.IsSuspended = it.IsSuspended != 0;
                p.PatternSlotIndex = it.PatternSlotIndex;
                p.PatternSlotCount = it.PatternSlotCount;

                _active.Add(p);
            }
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

        public void Tick(int frame, float fixedDeltaSeconds, List<ProjectileHitEvent> hitEvents, List<ProjectileExitEvent> exitEvents, List<ProjectileTickEvent> tickEvents)
        {
            // float 重载仅为边界兼容；dt 的位与平台无关（FrameTime 用 IEEE 除法得到 fixedDelta），
            // FromSingle 是单次精确换算。新代码应优先使用 Fixed64 重载。
            Tick(frame, Fixed64.FromSingle(fixedDeltaSeconds), hitEvents, exitEvents, tickEvents);
        }

        public void Tick(int frame, Fixed64 fixedDeltaSeconds, List<ProjectileHitEvent> hitEvents, List<ProjectileExitEvent> exitEvents, List<ProjectileTickEvent> tickEvents)
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
                    if (to.SqrMagnitude > Fixed64.Zero)
                    {
                        p.Direction = to.Normalized;
                    }
                }

                var speed = (p.IsReturning && p.ReturnSpeed > Fixed64.Zero) ? p.ReturnSpeed : p.Speed;
                var move = speed * fixedDeltaSeconds;
                if (move <= Fixed64.Zero)
                {
                    p.LifetimeFramesLeft--;
                    continue;
                }

                if (p.DistanceLeft > Fixed64.Zero && move > p.DistanceLeft)
                {
                    move = p.DistanceLeft;
                }

                var dir = p.Direction;
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
                if (p.IgnoreCollider.Value != 0)
                {
                    hitColliderIdsThisTick[hitColliderCount++] = p.IgnoreCollider.Value;
                }

                while (remaining > Fixed64.Zero)
                {
                    if (!TrySweepSkippingIgnored(origin, dir, remaining, p.CollisionLayerMask, hitColliderIdsThisTick, hitColliderCount, p.CollisionHalfExtents, out var hit))
                    {
                        // 剩余线段内没有命中。
                        origin = origin + dir * remaining;
                        remaining = Fixed64.Zero;
                        break;
                    }

                    var hitEvt = new ProjectileHitEvent(p.Id, p.OwnerId, p.TemplateId, p.LauncherActorId, p.RootActorId, hit.Collider, hit.Distance, hit.Point, hit.Normal, frame, hitCount: 0);
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
                        if (hitColliderCount < hitColliderIdsThisTick.Length)
                        {
                            hitColliderIdsThisTick[hitColliderCount++] = hit.Collider.Value;
                        }

                        origin = hit.Point.ToFixed() + dir * EpsilonAdvance;
                        remaining -= Fixed64.FromSingle(hit.Distance) + EpsilonAdvance;
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
                        if (hitColliderCount < hitColliderIdsThisTick.Length)
                        {
                            hitColliderIdsThisTick[hitColliderCount++] = hit.Collider.Value;
                        }

                        origin = hit.Point.ToFixed() + dir * EpsilonAdvance;
                        remaining -= Fixed64.FromSingle(hit.Distance) + EpsilonAdvance;
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
                        origin = hit.Point.ToFixed() + dir * EpsilonAdvance;
                        remaining -= Fixed64.FromSingle(hit.Distance) + EpsilonAdvance;
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

                    p.TotalHitCount++;
                    hitEvents?.Add(new ProjectileHitEvent(p.Id, p.OwnerId, p.TemplateId, p.LauncherActorId, p.RootActorId, hit.Collider, hit.Distance, hit.Point, hit.Normal, frame, p.TotalHitCount));
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
                    origin = hit.Point.ToFixed() + dir * EpsilonAdvance;
                    remaining -= Fixed64.FromSingle(hit.Distance) + EpsilonAdvance;
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

                // 移动后发送周期性 Tick 事件。
                EmitTickIfDue(p, frame, tickEvents);

                if (p.DistanceLeft > Fixed64.Zero)
                {
                    p.DistanceLeft -= move;
                    if (p.DistanceLeft <= Fixed64.Zero)
                    {
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

        public ProjectileWorldSnapshotPayload(int version, FrameIndex frame, int nextId, ProjectileWorldSnapshotItem[] items)
        {
            Version = version;
            Frame = frame;
            NextId = nextId;
            Items = items;
        }
    }


    /// <summary>
    /// 回滚快照条目（schema v7）。定点字段以 Q32.32 raw long 存储，跨平台位一致；
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
            int trackingTargetActorId)
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
        }
    }

}
