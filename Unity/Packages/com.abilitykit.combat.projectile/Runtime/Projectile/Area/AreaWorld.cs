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
        private readonly List<Area> _active = new List<Area>(64);
        private int _nextId = 1;

        public AreaWorld(ICollisionWorld collision)
        {
            _collision = collision ?? throw new ArgumentNullException(nameof(collision));
        }

        public int ActiveCount => _active.Count;

        public AreaId Spawn(in AreaSpawnParams p, int frame, List<AreaSpawnEvent> spawnEvents)
        {
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
                            enterEvents?.Add(new AreaEnterEvent(a.Id, a.OwnerId, curr[ci], frame));
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

                    enterEvents?.Add(new AreaEnterEvent(a.Id, a.OwnerId, cv, frame));
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
}
