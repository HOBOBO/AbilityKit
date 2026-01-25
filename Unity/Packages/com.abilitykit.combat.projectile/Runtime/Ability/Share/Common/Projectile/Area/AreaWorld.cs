using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.Common.Pool;
using AbilityKit.Ability.Share.Math;

namespace AbilityKit.Ability.Share.Common.Projectile
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
            a.Radius = p.Radius;
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
                    // Emit exit for all currently inside.
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

                // Query current overlaps.
                var curr = a.GetCurrListAndClear();
                var count = _collision.OverlapSphere(new Sphere(a.Center, a.Radius), a.LayerMask, curr);
                if (count > 1)
                {
                    curr.Sort(CompareCollider);
                }

                // Diff prev vs curr to produce enter/exit.
                var prevList = a.GetPrevList();
                var pi = 0;
                var ci = 0;

                while (pi < prevList.Count || ci < curr.Count)
                {
                    if (pi >= prevList.Count)
                    {
                        // Remaining curr are enters.
                        for (; ci < curr.Count; ci++)
                        {
                            enterEvents?.Add(new AreaEnterEvent(a.Id, a.OwnerId, curr[ci], frame));
                        }
                        break;
                    }

                    if (ci >= curr.Count)
                    {
                        // Remaining prev are exits.
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

                // Stay events (interval).
                if (a.StayIntervalFrames > 0 && frame >= a.NextStayFrame)
                {
                    for (int k = 0; k < curr.Count; k++)
                    {
                        stayEvents?.Add(new AreaStayEvent(a.Id, a.OwnerId, curr[k], frame));
                    }

                    a.NextStayFrame = frame + a.StayIntervalFrames;
                }

                // Swap prev/curr for next tick.
                a.SwapLists();

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
