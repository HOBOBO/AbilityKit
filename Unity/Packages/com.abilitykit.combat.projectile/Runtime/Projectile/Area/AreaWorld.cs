using MemoryPack;
using System;
using System.Collections.Generic;
using AbilityKit.Combat.Collision;
using AbilityKit.Core.Pooling;
using AbilityKit.Core.Mathematics;

namespace AbilityKit.Combat.Projectile
{
    public sealed class AreaWorld
    {
        private static readonly ObjectPool<Area> Pool = Pools.GetPool(
            key: "Area",
            createFunc: () => new Area(),
            defaultCapacity: 16,
            maxSize: 2048);

        private static int CompareCollider(ColliderId a, ColliderId b) => a.Value.CompareTo(b.Value);

        private readonly ICollisionWorld _collision;
        private IProjectileTrackingTargetProvider? _trackingTargetProvider;
        private readonly List<Area> _active = new List<Area>(64);
        private int _nextId = 1;
        private int _nextGroupId = 1;
        private readonly Dictionary<int, AreaGroup> _groups = new Dictionary<int, AreaGroup>();
        private readonly Dictionary<AreaGroupIdentity, int> _groupKeys = new Dictionary<AreaGroupIdentity, int>();

        public AreaWorld(ICollisionWorld collision)
        {
            _collision = collision ?? throw new ArgumentNullException(nameof(collision));
        }

        public int ActiveCount => _active.Count;
        public void SetTrackingTargetProvider(IProjectileTrackingTargetProvider provider) => _trackingTargetProvider = provider;

        public byte[] ExportRollback(int frame)
        {
            var items = new AreaSnapshotItem[_active.Count];
            for (var i = 0; i < _active.Count; i++)
            {
                var a = _active[i];
                items[i] = new AreaSnapshotItem(a.Id.Value, a.OwnerId,
                    Bits(a.Center.X), Bits(a.Center.Y), Bits(a.Center.Z), Bits(a.Radius), Bits(a.MaxRadius),
                    Bits(a.RadiusGrowthPerFrame), (int)a.ShapeKind, Bits(a.Forward.X), Bits(a.Forward.Y), Bits(a.Forward.Z),
                    Bits(a.HalfAngleDeg), Bits(a.Width), Bits(a.Length), a.AnchorAtOrigin ? 1 : 0,
                    a.LifetimeFramesLeft, a.LayerMask, a.StayIntervalFrames, a.NextStayFrame, a.ArmFrame,
                    a.IsArmed ? 1 : 0, a.ConsumeOnFirstEnter ? 1 : 0, a.EnterConsumed ? 1 : 0, a.FollowOwner ? 1 : 0,
                    Bits(a.FollowOffset.X), Bits(a.FollowOffset.Y), Bits(a.FollowOffset.Z),
                    a.FollowActorId,
                    a.GetPrevList().ConvertAll(x => x.Value).ToArray(), a.GroupId);
            }
            var groups = new AreaGroupSnapshotItem[_groups.Count];
            var groupIndex = 0;
            foreach (var pair in _groups)
            {
                var targets = new int[pair.Value.ClaimedTargets.Count];
                pair.Value.ClaimedTargets.CopyTo(targets);
                Array.Sort(targets);
                groups[groupIndex++] = new AreaGroupSnapshotItem(pair.Key, pair.Value.RootContextId, pair.Value.GroupKey, targets);
            }
            Array.Sort(groups, (a, b) => a.GroupId.CompareTo(b.GroupId));
            return MemoryPackSerializer.Serialize(new AreaWorldSnapshot(3, frame, _nextId, items, _nextGroupId, groups));
        }

        public void ImportRollback(byte[] payload)
        {
            Clear();
            if (payload == null || payload.Length == 0) return;
            var snapshot = MemoryPackSerializer.Deserialize<AreaWorldSnapshot>(payload);
            if (snapshot.Version != 2 && snapshot.Version != 3) throw new InvalidOperationException($"Unsupported Area snapshot version {snapshot.Version}.");
            _nextId = snapshot.NextId > 0 ? snapshot.NextId : 1;
            _nextGroupId = snapshot.NextGroupId > 0 ? snapshot.NextGroupId : 1;
            if (snapshot.Groups != null)
            {
                foreach (var item in snapshot.Groups)
                {
                    if (item.GroupId <= 0) continue;
                    var group = new AreaGroup(item.RootContextId, item.GroupKey);
                    if (item.ClaimedTargets != null)
                        foreach (var targetId in item.ClaimedTargets) if (targetId > 0) group.ClaimedTargets.Add(targetId);
                    _groups[item.GroupId] = group;
                    _groupKeys[new AreaGroupIdentity(item.RootContextId, item.GroupKey)] = item.GroupId;
                }
            }
            if (snapshot.Items == null) return;
            foreach (var item in snapshot.Items)
            {
                if (item.Id <= 0) continue;
                var a = Pool.Get();
                a.Id = new AreaId(item.Id); a.OwnerId = item.OwnerId;
                a.Center = new Vec3(Float(item.CenterX), Float(item.CenterY), Float(item.CenterZ));
                a.Radius = Float(item.Radius); a.MaxRadius = Float(item.MaxRadius); a.RadiusGrowthPerFrame = Float(item.RadiusGrowth);
                a.ShapeKind = (OverlapShapeKind)item.ShapeKind;
                a.Forward = new Vec3(Float(item.ForwardX), Float(item.ForwardY), Float(item.ForwardZ));
                a.HalfAngleDeg = Float(item.HalfAngle); a.Width = Float(item.Width); a.Length = Float(item.Length);
                a.AnchorAtOrigin = item.AnchorAtOrigin != 0; a.LifetimeFramesLeft = item.LifetimeFramesLeft; a.LayerMask = item.LayerMask;
                a.StayIntervalFrames = item.StayIntervalFrames; a.NextStayFrame = item.NextStayFrame; a.ArmFrame = item.ArmFrame;
                a.IsArmed = item.IsArmed != 0; a.ConsumeOnFirstEnter = item.ConsumeOnFirstEnter != 0; a.EnterConsumed = item.EnterConsumed != 0;
                a.FollowOwner = item.FollowOwner != 0;
                a.FollowOffset = new Vec3(Float(item.FollowOffsetX), Float(item.FollowOffsetY), Float(item.FollowOffsetZ));
                a.FollowActorId = item.FollowActorId;
                a.GroupId = item.GroupId;
                var prev = a.GetPrevList();
                if (item.Overlaps != null) foreach (var id in item.Overlaps) if (id > 0) prev.Add(new ColliderId(id));
                prev.Sort(CompareCollider);
                _active.Add(a);
            }
        }

        private void Clear()
        {
            for (var i = _active.Count - 1; i >= 0; i--) RemoveAtSwapBack(i);
            _nextId = 1;
            _nextGroupId = 1;
            _groups.Clear();
            _groupKeys.Clear();
        }

        private static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        private static float Float(int bits) => BitConverter.Int32BitsToSingle(bits);

        public AreaId Spawn(in AreaSpawnParams p, int frame, List<AreaSpawnEvent> spawnEvents)
        {
            var groupId = p.ClaimTargetsWithinGroup ? GetOrCreateGroup(p.RootContextId, p.GroupKey) : 0;
            var a = Pool.Get();
            a.Id = new AreaId(_nextId++);
            a.OwnerId = p.OwnerId;
            a.Center = p.Center;
            a.Radius = p.InitialRadius > 0f ? Math.Min(p.InitialRadius, p.Radius) : p.Radius;
            a.MaxRadius = p.Radius;
            a.ShapeKind = p.ShapeKind == OverlapShapeKind.Sector || p.ShapeKind == OverlapShapeKind.Rectangle
                ? p.ShapeKind
                : OverlapShapeKind.Circle;
            a.Forward = p.Forward.SqrMagnitude > 0.000001f ? p.Forward.Normalized : Vec3.Forward;
            a.HalfAngleDeg = p.HalfAngleDeg;
            a.Width = p.Width;
            a.Length = p.Length;
            a.AnchorAtOrigin = p.AnchorAtOrigin;
            a.RadiusGrowthPerFrame = Math.Max(0f, p.RadiusGrowthPerFrame);
            a.LifetimeFramesLeft = p.LifetimeFrames;
            a.LayerMask = p.CollisionLayerMask;
            a.StayIntervalFrames = p.StayIntervalFrames;
            a.NextStayFrame = p.StayIntervalFrames > 0 ? frame : 0;
            a.ArmFrame = frame + p.ArmDelayFrames;
            a.IsArmed = p.ArmDelayFrames == 0;
            a.ConsumeOnFirstEnter = p.ConsumeOnFirstEnter;
            a.FollowOwner = p.FollowOwner;
            a.FollowActorId = p.FollowActorId > 0 ? p.FollowActorId : p.OwnerId;
            a.GroupId = groupId;
            if (a.FollowOwner && _trackingTargetProvider != null && _trackingTargetProvider.TryGetTrackingTargetPosition(a.FollowActorId, out var ownerPosition))
                a.FollowOffset = p.Center - ownerPosition;
            else
                a.FollowOffset = Vec3.Zero;

            _active.Add(a);
            spawnEvents?.Add(new AreaSpawnEvent(a.Id, a.OwnerId, frame, a.Center, a.Radius));
            return a.Id;
        }

        public bool Despawn(AreaId id, int frame, List<AreaExpireEvent> expireEvents)
        {
            for (int i = 0; i < _active.Count; i++)
            {
                var a = _active[i];
                if (a == null) continue;
                if (a.Id.Value != id.Value) continue;

                expireEvents?.Add(new AreaExpireEvent(a.Id, a.OwnerId, frame));
                RemoveAtSwapBack(i);
                return true;
            }
            return false;
        }

        /// <summary>Atomically accepts at most one Enter event for an opt-in one-shot Area.</summary>
        public bool TryConsumeEnter(AreaId id)
        {
            for (int i = 0; i < _active.Count; i++)
            {
                var a = _active[i];
                if (a == null || a.Id.Value != id.Value) continue;
                if (a.EnterConsumed) return false;
                if (a.ConsumeOnFirstEnter) a.EnterConsumed = true;
                return true;
            }
            return false;
        }

        public bool TryAcceptEnter(AreaId id, int targetActorId)
        {
            if (targetActorId <= 0) return false;
            for (var i = 0; i < _active.Count; i++)
            {
                var area = _active[i];
                if (area == null || area.Id.Value != id.Value || area.EnterConsumed) continue;
                if (area.GroupId > 0)
                {
                    // A grouped Area must always resolve to a live group. Silently
                    // accepting an orphan would bypass the cross-Area claim gate.
                    if (!_groups.TryGetValue(area.GroupId, out var group)) return false;
                    if (group.ClaimedTargets.Contains(targetActorId)) return false;
                    group.ClaimedTargets.Add(targetActorId);
                }
                if (area.ConsumeOnFirstEnter) area.EnterConsumed = true;
                return true;
            }
            return false;
        }

        private int GetOrCreateGroup(long rootContextId, int groupKey)
        {
            if (rootContextId == 0L || groupKey <= 0) throw new ArgumentOutOfRangeException(nameof(groupKey), "Grouped Area claims require a root context and positive group key.");
            var key = new AreaGroupIdentity(rootContextId, groupKey);
            if (_groupKeys.TryGetValue(key, out var existing)) return existing;
            var id = _nextGroupId++;
            if (id <= 0) throw new InvalidOperationException("Area group id space exhausted.");
            _groups.Add(id, new AreaGroup(rootContextId, groupKey));
            _groupKeys.Add(key, id);
            return id;
        }

        private readonly struct AreaGroupIdentity : IEquatable<AreaGroupIdentity>
        {
            private readonly long _rootContextId;
            private readonly int _groupKey;

            public AreaGroupIdentity(long rootContextId, int groupKey)
            {
                _rootContextId = rootContextId;
                _groupKey = groupKey;
            }

            public bool Equals(AreaGroupIdentity other) => _rootContextId == other._rootContextId && _groupKey == other._groupKey;
            public override bool Equals(object obj) => obj is AreaGroupIdentity other && Equals(other);
            public override int GetHashCode() => unchecked((_rootContextId.GetHashCode() * 397) ^ _groupKey);
        }

        /// <summary>
        /// Removes an area without producing an expire event. This is reserved for
        /// failed spawn compensation; gameplay removal must use <see cref="Despawn"/>.
        /// </summary>
        public bool Cancel(AreaId id)
        {
            for (int i = 0; i < _active.Count; i++)
            {
                var a = _active[i];
                if (a == null || a.Id.Value != id.Value) continue;
                RemoveAtSwapBack(i);
                return true;
            }

            return false;
        }

        public void Tick(
            int frame,
            List<AreaEnterEvent> enterEvents,
            List<AreaStayEvent> stayEvents,
            List<AreaExitEvent> exitEvents,
            List<AreaExpireEvent> expireEvents)
        {
            if (_active.Count == 0) return;

            for (int i = 0; i < _active.Count; i++)
            {
                var a = _active[i];
                if (a == null)
                {
                    RemoveAtSwapBack(i);
                    i--;
                    continue;
                }

                if (a.LifetimeFramesLeft <= 0)
                {
                    // 为当前仍在范围内的目标发送退出事件。
                    var prev = a.GetPrevList();
                    for (int k = 0; k < prev.Count; k++)
                    {
                        exitEvents?.Add(new AreaExitEvent(a.Id, a.OwnerId, prev[k], frame));
                    }

                    expireEvents?.Add(new AreaExpireEvent(a.Id, a.OwnerId, frame));
                    RemoveAtSwapBack(i);
                    i--;
                    continue;
                }

                // Keep the previous overlap set empty until arming. The first armed
                // query therefore produces Enter for targets already standing inside.
                if (!a.IsArmed && frame >= a.ArmFrame)
                {
                    a.IsArmed = true;
                    a.GetPrevList().Clear();
                }

                if (!a.IsArmed)
                {
                    a.GetPrevList().Clear();
                    if (a.RadiusGrowthPerFrame > 0f)
                        a.Radius = Math.Min(a.Radius + a.RadiusGrowthPerFrame, a.MaxRadius);
                    a.LifetimeFramesLeft--;
                    continue;
                }

                if (a.FollowOwner)
                {
                    if (_trackingTargetProvider == null || !_trackingTargetProvider.TryGetTrackingTargetPosition(a.FollowActorId, out var ownerPosition))
                    {
                        expireEvents?.Add(new AreaExpireEvent(a.Id, a.OwnerId, frame));
                        RemoveAtSwapBack(i);
                        i--;
                        continue;
                    }
                    a.Center = ownerPosition + a.FollowOffset;
                }

                // 查询当前重叠目标。
                var curr = a.GetCurrListAndClear();
                var filter = new LayerFilter(a.LayerMask);
                int count;
                if (a.ShapeKind != OverlapShapeKind.Circle && _collision is IShapeOverlapCollisionWorld shapeWorld)
                {
                    var shape = new OverlapShapeQuery(
                        a.ShapeKind,
                        in a.Center,
                        in a.Forward,
                        a.Radius,
                        a.HalfAngleDeg,
                        a.Width,
                        a.Length,
                        a.AnchorAtOrigin);
                    count = shapeWorld.OverlapShape(in shape, in filter, curr);
                }
                else
                {
                    count = _collision.OverlapSphere(new Sphere(a.Center, a.Radius), in filter, curr);
                }
                if (count > 1)
                {
                    curr.Sort(CompareCollider);
                }

                // 对比上一帧和当前帧，生成进入/退出事件。
                var prevList = a.GetPrevList();
                var pi = 0;
                var ci = 0;

                while (pi < prevList.Count || ci < curr.Count)
                {
                    if (pi >= prevList.Count)
                    {
                        // 剩余当前项都是进入目标。
                        for (; ci < curr.Count; ci++)
                        {
                            if (!a.EnterConsumed) enterEvents?.Add(new AreaEnterEvent(a.Id, a.OwnerId, curr[ci], frame));
                        }
                        break;
                    }

                    if (ci >= curr.Count)
                    {
                        // 剩余上一帧项都是退出目标。
                        for (; pi < prevList.Count; pi++)
                        {
                            exitEvents?.Add(new AreaExitEvent(a.Id, a.OwnerId, prevList[pi], frame));
                        }
                        break;
                    }

                    var pv = prevList[pi];
                    var cv = curr[ci];
                    if (pv.Value == cv.Value)
                    {
                        pi++;
                        ci++;
                        continue;
                    }

                    if (pv.Value < cv.Value)
                    {
                        exitEvents?.Add(new AreaExitEvent(a.Id, a.OwnerId, pv, frame));
                        pi++;
                        continue;
                    }

                    if (!a.EnterConsumed) enterEvents?.Add(new AreaEnterEvent(a.Id, a.OwnerId, cv, frame));
                    ci++;
                }

                // 按间隔发送 Stay 事件。
                if (a.StayIntervalFrames > 0 && frame >= a.NextStayFrame)
                {
                    for (int k = 0; k < curr.Count; k++)
                    {
                        stayEvents?.Add(new AreaStayEvent(a.Id, a.OwnerId, curr[k], frame));
                    }

                    a.NextStayFrame = frame + a.StayIntervalFrames;
                }

                // 交换上一帧和当前帧列表，供下一帧使用。
                a.SwapLists();

                if (a.RadiusGrowthPerFrame > 0f)
                    a.Radius = Math.Min(a.Radius + a.RadiusGrowthPerFrame, a.MaxRadius);
                a.LifetimeFramesLeft--;
            }
        }

        private void RemoveAtSwapBack(int index)
        {
            var last = _active.Count - 1;
            var a = _active[index];
            if (index != last)
            {
                _active[index] = _active[last];
            }
            _active.RemoveAt(last);

            if (a != null)
            {
                if (a.GroupId > 0 && _groups.TryGetValue(a.GroupId, out var group))
                {
                    var hasMember = false;
                    for (var i = 0; i < _active.Count; i++)
                    {
                        if (_active[i].GroupId == a.GroupId) { hasMember = true; break; }
                    }
                    if (!hasMember)
                    {
                        _groups.Remove(a.GroupId);
                        _groupKeys.Remove(new AreaGroupIdentity(group.RootContextId, group.GroupKey));
                    }
                }
                Pool.Release(a);
            }
        }

        private sealed class Area : IPoolable
        {
            public AreaId Id;
            public int OwnerId;
            public Vec3 Center;
            public float Radius;
            public float MaxRadius;
            public float RadiusGrowthPerFrame;
            public OverlapShapeKind ShapeKind;
            public Vec3 Forward;
            public float HalfAngleDeg;
            public float Width;
            public float Length;
            public bool AnchorAtOrigin;
            public int LifetimeFramesLeft;
            public int LayerMask;

            public int StayIntervalFrames;
            public int NextStayFrame;
            public int ArmFrame;
            public bool IsArmed;
            public bool ConsumeOnFirstEnter;
            public bool EnterConsumed;
            public bool FollowOwner;
            public Vec3 FollowOffset;
            public int FollowActorId;
            public int GroupId;

            private readonly List<ColliderId> _hitsA = new List<ColliderId>(8);
            private readonly List<ColliderId> _hitsB = new List<ColliderId>(8);
            private bool _aIsPrev = true;

            public List<ColliderId> GetPrevList() => _aIsPrev ? _hitsA : _hitsB;
            public List<ColliderId> GetCurrListAndClear()
            {
                var curr = _aIsPrev ? _hitsB : _hitsA;
                curr.Clear();
                return curr;
            }

            public void SwapLists()
            {
                _aIsPrev = !_aIsPrev;
            }

            void IPoolable.OnPoolGet() { }

            void IPoolable.OnPoolRelease()
            {
                Id = default;
                OwnerId = 0;
                Center = Vec3.Zero;
                Radius = 0f;
                MaxRadius = 0f;
                RadiusGrowthPerFrame = 0f;
                ShapeKind = OverlapShapeKind.Circle;
                Forward = Vec3.Forward;
                HalfAngleDeg = 0f;
                Width = 0f;
                Length = 0f;
                AnchorAtOrigin = true;
                LifetimeFramesLeft = 0;
                LayerMask = 0;
                StayIntervalFrames = 0;
                NextStayFrame = 0;
                ArmFrame = 0;
                IsArmed = false;
                ConsumeOnFirstEnter = false;
                EnterConsumed = false;
                FollowOwner = false;
                FollowOffset = Vec3.Zero;
                FollowActorId = 0;
                GroupId = 0;
                _hitsA.Clear();
                _hitsB.Clear();
                _aIsPrev = true;
            }

            void IPoolable.OnPoolDestroy()
            {
                ((IPoolable)this).OnPoolRelease();
            }
        }
    }

    [MemoryPackable]
    public readonly partial struct AreaWorldSnapshot
    {
        [MemoryPackOrder(0)] public readonly int Version;
        [MemoryPackOrder(1)] public readonly int Frame;
        [MemoryPackOrder(2)] public readonly int NextId;
        [MemoryPackOrder(3)] public readonly AreaSnapshotItem[] Items;
        [MemoryPackOrder(4)] public readonly int NextGroupId;
        [MemoryPackOrder(5)] public readonly AreaGroupSnapshotItem[] Groups;
        public AreaWorldSnapshot(int version, int frame, int nextId, AreaSnapshotItem[] items, int nextGroupId = 1, AreaGroupSnapshotItem[] groups = null)
        { Version = version; Frame = frame; NextId = nextId; Items = items; NextGroupId = nextGroupId; Groups = groups; }
    }

    [MemoryPackable]
    public readonly partial struct AreaSnapshotItem
    {
        [MemoryPackOrder(0)] public readonly int Id; [MemoryPackOrder(1)] public readonly int OwnerId;
        [MemoryPackOrder(2)] public readonly int CenterX; [MemoryPackOrder(3)] public readonly int CenterY; [MemoryPackOrder(4)] public readonly int CenterZ;
        [MemoryPackOrder(5)] public readonly int Radius; [MemoryPackOrder(6)] public readonly int MaxRadius; [MemoryPackOrder(7)] public readonly int RadiusGrowth;
        [MemoryPackOrder(8)] public readonly int ShapeKind; [MemoryPackOrder(9)] public readonly int ForwardX; [MemoryPackOrder(10)] public readonly int ForwardY; [MemoryPackOrder(11)] public readonly int ForwardZ;
        [MemoryPackOrder(12)] public readonly int HalfAngle; [MemoryPackOrder(13)] public readonly int Width; [MemoryPackOrder(14)] public readonly int Length;
        [MemoryPackOrder(15)] public readonly int AnchorAtOrigin; [MemoryPackOrder(16)] public readonly int LifetimeFramesLeft; [MemoryPackOrder(17)] public readonly int LayerMask;
        [MemoryPackOrder(18)] public readonly int StayIntervalFrames; [MemoryPackOrder(19)] public readonly int NextStayFrame; [MemoryPackOrder(20)] public readonly int ArmFrame;
        [MemoryPackOrder(21)] public readonly int IsArmed; [MemoryPackOrder(22)] public readonly int ConsumeOnFirstEnter; [MemoryPackOrder(23)] public readonly int EnterConsumed;
        [MemoryPackOrder(24)] public readonly int FollowOwner;
        [MemoryPackOrder(25)] public readonly int FollowOffsetX;
        [MemoryPackOrder(26)] public readonly int FollowOffsetY;
        [MemoryPackOrder(27)] public readonly int FollowOffsetZ;
        [MemoryPackOrder(28)] public readonly int[] Overlaps;
        [MemoryPackOrder(29)] public readonly int FollowActorId;
        [MemoryPackOrder(30)] public readonly int GroupId;
        public AreaSnapshotItem(int id, int ownerId, int centerX, int centerY, int centerZ, int radius, int maxRadius, int radiusGrowth,
            int shapeKind, int forwardX, int forwardY, int forwardZ, int halfAngle, int width, int length, int anchorAtOrigin,
            int lifetimeFramesLeft, int layerMask, int stayIntervalFrames, int nextStayFrame, int armFrame, int isArmed,
            int consumeOnFirstEnter, int enterConsumed, int followOwner, int followOffsetX, int followOffsetY, int followOffsetZ, int followActorId, int[] overlaps, int groupId = 0)
        { Id=id; OwnerId=ownerId; CenterX=centerX; CenterY=centerY; CenterZ=centerZ; Radius=radius; MaxRadius=maxRadius; RadiusGrowth=radiusGrowth;
          ShapeKind=shapeKind; ForwardX=forwardX; ForwardY=forwardY; ForwardZ=forwardZ; HalfAngle=halfAngle; Width=width; Length=length;
          AnchorAtOrigin=anchorAtOrigin; LifetimeFramesLeft=lifetimeFramesLeft; LayerMask=layerMask; StayIntervalFrames=stayIntervalFrames;
          NextStayFrame=nextStayFrame; ArmFrame=armFrame; IsArmed=isArmed; ConsumeOnFirstEnter=consumeOnFirstEnter; EnterConsumed=enterConsumed;
          FollowOwner=followOwner; FollowOffsetX=followOffsetX; FollowOffsetY=followOffsetY; FollowOffsetZ=followOffsetZ; FollowActorId=followActorId; Overlaps=overlaps; GroupId=groupId; }
    }

    internal sealed class AreaGroup
    {
        public readonly long RootContextId;
        public readonly int GroupKey;
        public readonly HashSet<int> ClaimedTargets = new HashSet<int>();
        public AreaGroup(long rootContextId, int groupKey) { RootContextId = rootContextId; GroupKey = groupKey; }
    }

    [MemoryPackable]
    public readonly partial struct AreaGroupSnapshotItem
    {
        [MemoryPackOrder(0)] public readonly int GroupId;
        [MemoryPackOrder(1)] public readonly long RootContextId;
        [MemoryPackOrder(2)] public readonly int GroupKey;
        [MemoryPackOrder(3)] public readonly int[] ClaimedTargets;
        public AreaGroupSnapshotItem(int groupId, long rootContextId, int groupKey, int[] claimedTargets)
        { GroupId = groupId; RootContextId = rootContextId; GroupKey = groupKey; ClaimedTargets = claimedTargets; }
    }
}
