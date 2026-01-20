using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Share.Common.Pool;
using AbilityKit.Ability.Share;
using AbilityKit.Ability.Share.Math;

namespace AbilityKit.Ability.Share.Common.Projectile
{
    public sealed class ProjectileWorld
    {
        private static readonly ObjectPool<Projectile> Pool = Pools.GetPool(
            key: "Projectile",
            createFunc: () => new Projectile(),
            defaultCapacity: 32,
            maxSize: 4096);

        private readonly ICollisionWorld _collision;
        private readonly List<Projectile> _active = new List<Projectile>(128);

        private int _nextId = 1;

        public ProjectileWorld(ICollisionWorld collision)
        {
            _collision = collision ?? throw new ArgumentNullException(nameof(collision));
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
            proj.Position = p.Position;
            proj.Direction = p.Direction;
            proj.Speed = p.Speed;
            proj.LifetimeFramesLeft = p.LifetimeFrames;
            proj.DistanceLeft = p.MaxDistance;
            proj.CollisionLayerMask = p.CollisionLayerMask;
            proj.IgnoreCollider = p.IgnoreCollider;
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

            _active.Add(proj);
            return proj.Id;
        }

        public byte[] ExportRollback(FrameIndex frame)
        {
            var items = new SnapshotItem[_active.Count];
            for (int i = 0; i < _active.Count; i++)
            {
                var p = _active[i];
                if (p == null) continue;
                items[i] = new SnapshotItem(
                    id: p.Id.Value,
                    ownerId: p.OwnerId,
                    position: p.Position,
                    direction: p.Direction,
                    speed: p.Speed,
                    lifetimeFramesLeft: p.LifetimeFramesLeft,
                    distanceLeft: p.DistanceLeft,
                    collisionLayerMask: p.CollisionLayerMask,
                    ignoreCollider: p.IgnoreCollider.Value,
                    hitsRemaining: p.HitsRemaining,
                    hitPolicyKind: p.HitPolicyKind,
                    hitPolicyParam: p.HitPolicyParam,
                    tickIntervalFrames: p.TickIntervalFrames,
                    nextTickFrame: p.NextTickFrame
                );
            }

            return BinaryObjectCodec.Encode(new SnapshotPayload(
                version: 1,
                frame: frame,
                nextId: _nextId,
                items: items
            ));
        }

        public void ImportRollback(FrameIndex frame, byte[] payload)
        {
            Clear();
            if (payload == null || payload.Length == 0) return;

            var snap = BinaryObjectCodec.Decode<SnapshotPayload>(payload);
            _nextId = snap.NextId <= 0 ? 1 : snap.NextId;

            if (snap.Items == null || snap.Items.Length == 0) return;

            for (int i = 0; i < snap.Items.Length; i++)
            {
                var it = snap.Items[i];
                if (it.Id <= 0) continue;

                var p = Pool.Get();
                p.Id = new ProjectileId(it.Id);
                p.OwnerId = it.OwnerId;
                p.Position = it.Position;
                p.Direction = it.Direction;
                p.Speed = it.Speed;
                p.LifetimeFramesLeft = it.LifetimeFramesLeft;
                p.DistanceLeft = it.DistanceLeft;
                p.CollisionLayerMask = it.CollisionLayerMask;
                p.IgnoreCollider = new ColliderId(it.IgnoreCollider);
                p.HitsRemaining = it.HitsRemaining;
                p.HitPolicyKind = it.HitPolicyKind;
                p.HitPolicyParam = it.HitPolicyParam;
                p.HitPolicy = ProjectileHitPolicyFactory.Create(it.HitPolicyKind, it.HitPolicyParam);
                p.TickIntervalFrames = it.TickIntervalFrames;
                p.NextTickFrame = it.NextTickFrame;

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
            for (int i = 0; i < _active.Count; i++)
            {
                var p = _active[i];
                if (p == null) continue;
                if (p.Id.Value != id.Value) continue;

                RemoveAtSwapBack(i);
                return true;
            }

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

                if (p.LifetimeFramesLeft <= 0)
                {
                    exitEvents?.Add(new ProjectileExitEvent(p.Id, p.OwnerId, p.TemplateId, p.LauncherActorId, p.RootActorId, ProjectileExitReason.Lifetime, frame, p.Position));
                    RemoveAtSwapBack(i);
                    i--;
                    continue;
                }

                var move = p.Speed * fixedDeltaSeconds;
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

                // Within a single tick, allow multiple hits (pierce). Keep deterministic upper bound.
                const int maxHitsPerStep = 8;
                const float epsilonAdvance = 0.001f;
                var hitCount = 0;
                var origin = prev;

                while (remaining > 0f)
                {
                    if (!TryRaycastSkippingIgnored(origin, dir, remaining, p.CollisionLayerMask, p.IgnoreCollider, out var hit))
                    {
                        // No hit in remaining segment.
                        origin = origin + dir * remaining;
                        remaining = 0f;
                        break;
                    }

                    var hitEvt = new ProjectileHitEvent(p.Id, p.OwnerId, p.TemplateId, p.LauncherActorId, p.RootActorId, hit.Collider, hit.Distance, hit.Point, hit.Normal, frame);

                    // Hit filter + per-collider cooldown.
                    if (p.HitFilter != null && !p.HitFilter.ShouldHit(p.OwnerId, hit.Collider, frame))
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

                    hitEvents?.Add(hitEvt);
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

                    // Continue after hit: advance to just past hit point.
                    origin = hit.Point + dir * epsilonAdvance;
                    remaining -= hit.Distance + epsilonAdvance;
                    hitCount++;
                    if (hitCount >= maxHitsPerStep || remaining <= 0f)
                    {
                        // Avoid infinite loops; stop for this frame.
                        remaining = 0f;
                        break;
                    }
                }

                p.Position = origin;
                p.LifetimeFramesLeft--;

                // Periodic tick event (after movement).
                if (p.TickIntervalFrames > 0)
                {
                    if (p.NextTickFrame <= 0) p.NextTickFrame = frame;
                    if (frame >= p.NextTickFrame)
                    {
                        tickEvents?.Add(new ProjectileTickEvent(p.Id, p.OwnerId, p.TemplateId, p.LauncherActorId, p.RootActorId, frame, p.Position));
                        p.NextTickFrame = frame + p.TickIntervalFrames;
                    }
                }

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

        private bool TryRaycastSkippingIgnored(in Vec3 origin, in Vec3 dir, float maxDistance, int layerMask, ColliderId ignored, out RaycastHit hit)
        {
            // Keep it deterministic and avoid infinite loops: fixed number of retries.
            const int maxAttempts = 4;
            const float epsilonAdvance = 0.001f;

            var o = origin;
            var remaining = maxDistance;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                var ray = new Ray3(o, dir);
                if (!_collision.Raycast(ray, remaining, layerMask, out hit))
                {
                    hit = default;
                    return false;
                }

                if (!hit.Collider.Equals(ignored))
                {
                    return true;
                }

                // Skip ignored hit and try again from slightly past the hit point.
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

        public readonly struct SnapshotPayload
        {
            [BinaryMember(0)] public readonly int Version;
            [BinaryMember(1)] public readonly FrameIndex Frame;
            [BinaryMember(2)] public readonly int NextId;
            [BinaryMember(3)] public readonly SnapshotItem[] Items;

            public SnapshotPayload(int version, FrameIndex frame, int nextId, SnapshotItem[] items)
            {
                Version = version;
                Frame = frame;
                NextId = nextId;
                Items = items;
            }
        }

        public readonly struct SnapshotItem
        {
            [BinaryMember(0)] public readonly int Id;
            [BinaryMember(1)] public readonly int OwnerId;
            [BinaryMember(2)] public readonly Vec3 Position;
            [BinaryMember(3)] public readonly Vec3 Direction;
            [BinaryMember(4)] public readonly float Speed;
            [BinaryMember(5)] public readonly int LifetimeFramesLeft;
            [BinaryMember(6)] public readonly float DistanceLeft;
            [BinaryMember(7)] public readonly int CollisionLayerMask;
            [BinaryMember(8)] public readonly int IgnoreCollider;
            [BinaryMember(9)] public readonly int HitsRemaining;
            [BinaryMember(10)] public readonly ProjectileHitPolicyKind HitPolicyKind;
            [BinaryMember(11)] public readonly int HitPolicyParam;
            [BinaryMember(12)] public readonly int TickIntervalFrames;
            [BinaryMember(13)] public readonly int NextTickFrame;

            public SnapshotItem(
                int id,
                int ownerId,
                in Vec3 position,
                in Vec3 direction,
                float speed,
                int lifetimeFramesLeft,
                float distanceLeft,
                int collisionLayerMask,
                int ignoreCollider,
                int hitsRemaining,
                ProjectileHitPolicyKind hitPolicyKind,
                int hitPolicyParam,
                int tickIntervalFrames,
                int nextTickFrame)
            {
                Id = id;
                OwnerId = ownerId;
                Position = position;
                Direction = direction;
                Speed = speed;
                LifetimeFramesLeft = lifetimeFramesLeft;
                DistanceLeft = distanceLeft;
                CollisionLayerMask = collisionLayerMask;
                IgnoreCollider = ignoreCollider;
                HitsRemaining = hitsRemaining;
                HitPolicyKind = hitPolicyKind;
                HitPolicyParam = hitPolicyParam;
                TickIntervalFrames = tickIntervalFrames;
                NextTickFrame = nextTickFrame;
            }
        }
    }
}
