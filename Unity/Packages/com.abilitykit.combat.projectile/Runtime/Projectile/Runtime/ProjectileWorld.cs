using MemoryPack;
using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Combat.Collision;
using AbilityKit.Core.Pooling;
using AbilityKit.Core.Mathematics;
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
            proj.Position = p.Position;
            proj.Direction = p.Direction;
            proj.Speed = p.Speed;
            proj.TrackingTargetActorId = p.TrackingTargetActorId;
            proj.ReturnAfterFrames = p.ReturnAfterFrames;
            proj.ReturnSpeed = p.ReturnSpeed;
            proj.ReturnStopDistance = p.ReturnStopDistance;
            proj.IsReturning = false;
            proj.LifetimeFramesLeft = p.LifetimeFrames > 0 ? p.LifetimeFrames : int.MaxValue;
            proj.DistanceLeft = p.MaxDistance;
            proj.CollisionLayerMask = p.CollisionLayerMask;
            proj.IgnoreCollider = p.IgnoreCollider;
            proj.CollisionHalfExtents = p.CollisionHalfExtents;
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
                in projectile.Position,
                in projectile.Direction,
                projectile.LauncherActorId,
                projectile.RootActorId,
                projectile.PatternSlotIndex,
                projectile.PatternSlotCount,
                projectile.IsSuspended);
            return true;
        }

        public bool TrySetPosition(ProjectileId id, in Vec3 position)
        {
            var projectile = Find(id);
            if (projectile == null) return false;
            projectile.Position = position;
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
                    templateId: p.TemplateId,
                    launcherActorId: p.LauncherActorId,
                    rootActorId: p.RootActorId,
                    spawnFrame: p.SpawnFrame,
                    position: p.Position,
                    direction: p.Direction,
                    speed: p.Speed,
                    trackingTargetActorId: p.TrackingTargetActorId,
                    returnAfterFrames: p.ReturnAfterFrames,
                    returnSpeed: p.ReturnSpeed,
                    returnStopDistance: p.ReturnStopDistance,
                    isReturning: p.IsReturning ? 1 : 0,
                    lifetimeFramesLeft: p.LifetimeFramesLeft,
                    distanceLeft: p.DistanceLeft,
                    collisionLayerMask: p.CollisionLayerMask,
                    ignoreCollider: p.IgnoreCollider.Value,
                    collisionHalfExtents: p.CollisionHalfExtents,
                    hitsRemaining: p.HitsRemaining,
                    hitPolicyKind: p.HitPolicyKind,
                    hitPolicyParam: p.HitPolicyParam,
                    tickIntervalFrames: p.TickIntervalFrames,
                    nextTickFrame: p.NextTickFrame,
                    isSuspended: p.IsSuspended ? 1 : 0,
                    patternSlotIndex: p.PatternSlotIndex,
                    patternSlotCount: p.PatternSlotCount
                );
            }

            return MemoryPackSerializer.Serialize(new ProjectileWorldSnapshotPayload(
                version: 6,
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
                p.Position = it.Position;
                p.Direction = it.Direction;
                p.Speed = it.Speed;
                p.TrackingTargetActorId = it.TrackingTargetActorId;
                p.ReturnAfterFrames = it.ReturnAfterFrames;
                p.ReturnSpeed = it.ReturnSpeed;
                p.ReturnStopDistance = it.ReturnStopDistance;
                p.IsReturning = it.IsReturning != 0;
                p.LifetimeFramesLeft = it.LifetimeFramesLeft > 0 ? it.LifetimeFramesLeft : int.MaxValue;
                p.DistanceLeft = it.DistanceLeft;
                p.CollisionLayerMask = it.CollisionLayerMask;
                p.IgnoreCollider = new ColliderId(it.IgnoreCollider);
                p.CollisionHalfExtents = it.CollisionHalfExtents;
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
                    p.Position);
                RemoveAtSwapBack(i);
                return true;
            }

            exitEvent = default;
            return false;
        }

        public void Tick(int frame, float fixedDeltaSeconds, List<ProjectileHitEvent> hitEvents, List<ProjectileExitEvent> exitEvents, List<ProjectileTickEvent> tickEvents)
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
                    exitEvents?.Add(new ProjectileExitEvent(p.Id, p.OwnerId, p.TemplateId, p.LauncherActorId, p.RootActorId, ProjectileExitReason.Lifetime, frame, p.Position));
                    RemoveAtSwapBack(i);
                    i--;
                    continue;
                }

                // 返回发射者逻辑（服务器权威）。MaxDistance 只约束出程，回程由 ReturnArrived/Lifetime 结束。
                if (!p.IsReturning && p.ReturnAfterFrames > 0 && frame - p.SpawnFrame >= p.ReturnAfterFrames)
                {
                    p.IsReturning = true;
                    p.DistanceLeft = 0f;
                }

                if (p.IsReturning)
                {
                    if (_returnTargetProvider == null ||
                        !TryResolveReturnTarget(p.LauncherActorId, p.RootActorId, out var targetPos))
                    {
                        exitEvents?.Add(new ProjectileExitEvent(p.Id, p.OwnerId, p.TemplateId, p.LauncherActorId, p.RootActorId, ProjectileExitReason.ReturnTargetLost, frame, p.Position));
                        RemoveAtSwapBack(i);
                        i--;
                        continue;
                    }

                    if (p.ReturnStopDistance > 0f)
                    {
                        var dx = targetPos.X - p.Position.X;
                        var dy = targetPos.Y - p.Position.Y;
                        var dz = targetPos.Z - p.Position.Z;
                        var sqr = dx * dx + dy * dy + dz * dz;
                        var stopSqr = p.ReturnStopDistance * p.ReturnStopDistance;
                        if (sqr <= stopSqr)
                        {
                            exitEvents?.Add(new ProjectileExitEvent(p.Id, p.OwnerId, p.TemplateId, p.LauncherActorId, p.RootActorId, ProjectileExitReason.ReturnArrived, frame, p.Position));
                            RemoveAtSwapBack(i);
                            i--;
                            continue;
                        }
                    }

                    var to = targetPos - p.Position;
                    if (to.SqrMagnitude > 0f)
                    {
                        p.Direction = to.Normalized;
                    }
                }

                else if (p.TrackingTargetActorId > 0)
                {
                    if (_trackingTargetProvider == null ||
                        !_trackingTargetProvider.TryGetTrackingTargetPosition(p.TrackingTargetActorId, out var targetPos))
                    {
                        exitEvents?.Add(new ProjectileExitEvent(p.Id, p.OwnerId, p.TemplateId, p.LauncherActorId, p.RootActorId, ProjectileExitReason.TrackingTargetLost, frame, p.Position));
                        RemoveAtSwapBack(i);
                        i--;
                        continue;
                    }

                    var to = targetPos - p.Position;
                    if (to.SqrMagnitude > 0f)
                    {
                        p.Direction = to.Normalized;
                    }
                }

                var speed = (p.IsReturning && p.ReturnSpeed > 0f) ? p.ReturnSpeed : p.Speed;
                var move = speed * fixedDeltaSeconds;
                if (move <= 0f)
                {
                    p.LifetimeFramesLeft--;
                    continue;
                }

                if (p.DistanceLeft > 0f && move > p.DistanceLeft)
                {
                    move = p.DistanceLeft;
                }

                var dir = p.Direction;
                var prev = p.Position;
                var remaining = move;

                // 单帧内允许多次命中（穿透），同时保持确定性上限。
                const int maxHitsPerStep = 8;
                const float epsilonAdvance = 0.001f;
                var hitCount = 0;
                var origin = prev;

                // 防止同一帧内对同一个碰撞体重复触发命中回调。
                // 这样可保留“返回过程可跨帧多次命中同一目标”的行为，
                // 同时避免单帧内多段射线检测造成重复触发。
                var hitCollidersThisTick = p.HitCollidersThisTick;
                var hitColliderCount = 0;
                if (p.IgnoreCollider.Value != 0)
                {
                    hitCollidersThisTick[hitColliderCount++] = p.IgnoreCollider;
                }

                while (remaining > 0f)
                {
                    if (!TrySweepSkippingIgnored(origin, dir, remaining, p.CollisionLayerMask, hitCollidersThisTick, hitColliderCount, p.CollisionHalfExtents, out var hit))
                    {
                        // 剩余线段内没有命中。
                        origin = origin + dir * remaining;
                        remaining = 0f;
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
                        if (hitColliderCount < hitCollidersThisTick.Length)
                        {
                            hitCollidersThisTick[hitColliderCount++] = hit.Collider;
                        }

                        origin = hit.Point + dir * epsilonAdvance;
                        remaining -= hit.Distance + epsilonAdvance;
                        hitCount++;
                        if (hitCount >= maxHitsPerStep || remaining <= 0f)
                        {
                            remaining = 0f;
                            break;
                        }
                        continue;
                    }

                    if (p.HitCooldownFrames > 0 && hit.Collider.Equals(p.LastHitCollider) && frame < p.LastHitAllowedFrame)
                    {
                        origin = hit.Point + dir * epsilonAdvance;
                        remaining -= hit.Distance + epsilonAdvance;
                        hitCount++;
                        if (hitCount >= maxHitsPerStep || remaining <= 0f)
                        {
                            remaining = 0f;
                            break;
                        }
                        continue;
                    }

                    var alreadyHitThisTick = false;
                    for (int hc = 0; hc < hitColliderCount; hc++)
                    {
                        if (hitCollidersThisTick[hc].Equals(hit.Collider))
                        {
                            alreadyHitThisTick = true;
                            break;
                        }
                    }

                    if (alreadyHitThisTick)
                    {
                        origin = hit.Point + dir * epsilonAdvance;
                        remaining -= hit.Distance + epsilonAdvance;
                        hitCount++;
                        if (hitCount >= maxHitsPerStep || remaining <= 0f)
                        {
                            remaining = 0f;
                            break;
                        }
                        continue;
                    }

                    if (hitColliderCount < hitCollidersThisTick.Length)
                    {
                        hitCollidersThisTick[hitColliderCount++] = hit.Collider;
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
                    origin = hit.Point + dir * epsilonAdvance;
                    remaining -= hit.Distance + epsilonAdvance;
                    hitCount++;
                    if (hitCount >= maxHitsPerStep || remaining <= 0f)
                    {
                        // 避免无限循环，本帧停止处理。
                        remaining = 0f;
                        break;
                    }
                }

                p.Position = origin;
                p.LifetimeFramesLeft--;

                // 移动后发送周期性 Tick 事件。
                EmitTickIfDue(p, frame, tickEvents);

                if (p.DistanceLeft > 0f)
                {
                    p.DistanceLeft -= move;
                    if (p.DistanceLeft <= 0f)
                    {
                        exitEvents?.Add(new ProjectileExitEvent(p.Id, p.OwnerId, p.TemplateId, p.LauncherActorId, p.RootActorId, ProjectileExitReason.MaxDistance, frame, p.Position));
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

            tickEvents?.Add(new ProjectileTickEvent(p.Id, p.OwnerId, p.TemplateId, p.LauncherActorId, p.RootActorId, frame, p.Position));
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

        private bool TrySweepSkippingIgnored(in Vec3 origin, in Vec3 dir, float maxDistance, int layerMask, ColliderId[] ignoredColliders, int ignoredColliderCount, in Vec3 halfExtents, out RaycastHit hit)
        {
            // 使用固定重试次数，保持确定性并避免无限循环。
            const int maxAttempts = 4;
            const float epsilonAdvance = 0.001f;

            var o = origin;
            var remaining = maxDistance;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (!TrySweep(o, dir, remaining, layerMask, ignoredColliders, ignoredColliderCount, halfExtents, out hit))
                {
                    hit = default;
                    return false;
                }

                if (!ContainsCollider(ignoredColliders, ignoredColliderCount, hit.Collider))
                {
                    return true;
                }

                // 跳过被忽略的命中点，并从命中点稍后位置继续尝试。
                o = hit.Point + dir * epsilonAdvance;
                remaining -= hit.Distance + epsilonAdvance;
                if (remaining <= 0f)
                {
                    hit = default;
                    return false;
                }
            }

            hit = default;
            return false;
        }

        private bool TrySweep(in Vec3 origin, in Vec3 dir, float maxDistance, int layerMask, ColliderId[] ignoredColliders, int ignoredColliderCount, in Vec3 halfExtents, out RaycastHit hit)
        {
            if (halfExtents.SqrMagnitude > 0f && _collision is IOrientedBoxSweepCollisionWorld boxSweepWorld)
            {
                var forward = dir.Normalized;
                var right = Vec3.Cross(Vec3.Up, forward).Normalized;
                if (right.SqrMagnitude <= 0f) right = Vec3.Right;
                var up = Vec3.Cross(forward, right).Normalized;
                var box = new OrientedBoxSweep(origin, right, up, forward, halfExtents);
                var filter = new LayerFilter(layerMask);
                return boxSweepWorld.SweepOrientedBox(in box, in forward, maxDistance, in filter, out hit);
            }

            var ray = new Ray3(origin, dir);
            var filter2 = new LayerFilter(layerMask);
            return _collision.Raycast(ray, maxDistance, in filter2, out hit);
        }

        private static bool ContainsCollider(ColliderId[] colliders, int count, ColliderId collider)
        {
            if (colliders == null || count <= 0) return false;
            var limit = count < colliders.Length ? count : colliders.Length;
            for (var i = 0; i < limit; i++)
            {
                if (colliders[i].Equals(collider)) return true;
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


    [MemoryPackable]
    public readonly partial struct ProjectileWorldSnapshotItem
    {
        [MemoryPackOrder(0)] public readonly int Id;
        [MemoryPackOrder(1)] public readonly int OwnerId;
        [MemoryPackOrder(2)] public readonly Vec3 Position;
        [MemoryPackOrder(3)] public readonly Vec3 Direction;
        [MemoryPackOrder(4)] public readonly float Speed;
        [MemoryPackOrder(5)] public readonly int LifetimeFramesLeft;
        [MemoryPackOrder(6)] public readonly float DistanceLeft;
        [MemoryPackOrder(7)] public readonly int CollisionLayerMask;
        [MemoryPackOrder(8)] public readonly int IgnoreCollider;
        [MemoryPackOrder(9)] public readonly int HitsRemaining;
        [MemoryPackOrder(10)] public readonly ProjectileHitPolicyKind HitPolicyKind;
        [MemoryPackOrder(11)] public readonly int HitPolicyParam;
        [MemoryPackOrder(12)] public readonly int TickIntervalFrames;
        [MemoryPackOrder(13)] public readonly int NextTickFrame;

        [MemoryPackOrder(14)] public readonly int TemplateId;
        [MemoryPackOrder(15)] public readonly int LauncherActorId;
        [MemoryPackOrder(16)] public readonly int RootActorId;
        [MemoryPackOrder(17)] public readonly int SpawnFrame;
        [MemoryPackOrder(18)] public readonly int ReturnAfterFrames;
        [MemoryPackOrder(19)] public readonly float ReturnSpeed;
        [MemoryPackOrder(20)] public readonly float ReturnStopDistance;
        [MemoryPackOrder(21)] public readonly int IsReturning;
        [MemoryPackOrder(22)] public readonly int IsSuspended;
        [MemoryPackOrder(23)] public readonly int PatternSlotIndex;
        [MemoryPackOrder(24)] public readonly int PatternSlotCount;
        [MemoryPackOrder(25)] public readonly int TrackingTargetActorId;
        [MemoryPackOrder(26)] public readonly Vec3 CollisionHalfExtents;

        public ProjectileWorldSnapshotItem(
            int id,
            int ownerId,
            int templateId,
            int launcherActorId,
            int rootActorId,
            int spawnFrame,
            in Vec3 position,
            in Vec3 direction,
            float speed,
            int trackingTargetActorId,
            int returnAfterFrames,
            float returnSpeed,
            float returnStopDistance,
            int isReturning,
            int lifetimeFramesLeft,
            float distanceLeft,
            int collisionLayerMask,
            int ignoreCollider,
            in Vec3 collisionHalfExtents,
            int hitsRemaining,
            ProjectileHitPolicyKind hitPolicyKind,
            int hitPolicyParam,
            int tickIntervalFrames,
            int nextTickFrame,
            int isSuspended,
            int patternSlotIndex,
            int patternSlotCount)
        {
            Id = id;
            OwnerId = ownerId;
            TemplateId = templateId;
            LauncherActorId = launcherActorId;
            RootActorId = rootActorId;
            SpawnFrame = spawnFrame;
            Position = position;
            Direction = direction;
            Speed = speed;
            TrackingTargetActorId = trackingTargetActorId;
            ReturnAfterFrames = returnAfterFrames;
            ReturnSpeed = returnSpeed;
            ReturnStopDistance = returnStopDistance;
            IsReturning = isReturning;
            LifetimeFramesLeft = lifetimeFramesLeft;
            DistanceLeft = distanceLeft;
            CollisionLayerMask = collisionLayerMask;
            IgnoreCollider = ignoreCollider;
            CollisionHalfExtents = collisionHalfExtents;
            HitsRemaining = hitsRemaining;
            HitPolicyKind = hitPolicyKind;
            HitPolicyParam = hitPolicyParam;
            TickIntervalFrames = tickIntervalFrames;
            NextTickFrame = nextTickFrame;
            IsSuspended = isSuspended;
            PatternSlotIndex = patternSlotIndex;
            PatternSlotCount = patternSlotCount;
        }
    }

}
