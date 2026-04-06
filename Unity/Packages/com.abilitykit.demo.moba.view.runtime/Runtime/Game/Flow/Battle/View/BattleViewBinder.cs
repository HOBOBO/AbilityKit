using System;
using System.Collections.Generic;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Vfx;
using AbilityKit.World.ECS;
using UnityEngine;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleViewBinder : IMonoViewHandleRegistry
    {
        public interface IBattleViewShellLoader
        {
            GameObject CreateShellGameObject(int actorId, int modelId);
        }

        private sealed class ResourceBattleViewShellLoader : IBattleViewShellLoader
        {
            public GameObject CreateShellGameObject(int actorId, int modelId)
            {
                return BattleViewFactory.CreateShellGameObject(actorId, modelId);
            }
        }

        public void Sync(EC.IEntity entity)
        {
            Sync(entity, ctx: null);
        }

        private readonly BattleVfxManager _vfx;
        private readonly EC.IEntity _vfxNode;

        private readonly IBattleViewShellLoader _shellLoader;

        public BattleViewBinder(BattleVfxManager vfx, in EC.IEntity vfxNode, IBattleViewShellLoader shellLoader = null)
        {
            _vfx = vfx;
            _vfxNode = vfxNode;
            _shellLoader = shellLoader ?? new ResourceBattleViewShellLoader();
        }

        private sealed class Handle
        {
            public int Version;
            public bool Destroyed;
            public int ActorId;
            public int ModelId;
            public GameObject GameObject;
            public MonoViewHandle ViewHandle;
            public int VfxId;
            public EC.IEntityId VfxEntityId;
            public Vector3 PendingPos;
            public bool HasPendingPos;

            public SampleBuffer Pos;
        }

        private struct Sample
        {
            public double Time;
            public Vector3 Pos;
        }

        private struct SampleBuffer
        {
            private const int Capacity = 4;
            private Sample _s0;
            private Sample _s1;
            private Sample _s2;
            private Sample _s3;
            private int _count;

            private const double TimeEpsilon = 1e-6;

            public void Clear()
            {
                _s0 = default;
                _s1 = default;
                _s2 = default;
                _s3 = default;
                _count = 0;
            }

            private Sample Get(int index)
            {
                switch (index)
                {
                    case 0: return _s0;
                    case 1: return _s1;
                    case 2: return _s2;
                    case 3: return _s3;
                    default: return default;
                }
            }

            private void Set(int index, in Sample s)
            {
                switch (index)
                {
                    case 0: _s0 = s; break;
                    case 1: _s1 = s; break;
                    case 2: _s2 = s; break;
                    case 3: _s3 = s; break;
                }
            }

            public void Add(double time, in Vector3 pos)
            {
                var s = new Sample { Time = time, Pos = pos };

                for (var i = 0; i < _count; i++)
                {
                    var existing = Get(i);
                    if (Math.Abs(existing.Time - time) <= TimeEpsilon)
                    {
                        Set(i, in s);
                        return;
                    }
                }

                if (_count == 0)
                {
                    Set(0, in s);
                    _count = 1;
                    return;
                }

                var insertAt = _count;
                for (var i = 0; i < _count; i++)
                {
                    if (time < Get(i).Time)
                    {
                        insertAt = i;
                        break;
                    }
                }

                if (_count < Capacity)
                {
                    for (var i = _count; i > insertAt; i--)
                    {
                        var prev = Get(i - 1);
                        Set(i, in prev);
                    }
                    Set(insertAt, in s);
                    _count++;
                    return;
                }

                if (insertAt <= 0)
                {
                    return;
                }

                for (var i = 0; i < Capacity - 1; i++)
                {
                    var next = Get(i + 1);
                    Set(i, in next);
                }
                Set(Capacity - 1, in s);
            }

            public bool TryEvaluate(double time, out Vector3 pos)
            {
                if (_count <= 0)
                {
                    pos = default;
                    return false;
                }

                if (_count == 1)
                {
                    pos = Get(0).Pos;
                    return true;
                }

                var first = Get(0);
                if (time <= first.Time)
                {
                    pos = first.Pos;
                    return true;
                }

                var last = Get(_count - 1);
                if (time >= last.Time)
                {
                    pos = last.Pos;
                    return true;
                }

                for (var i = 0; i < _count - 1; i++)
                {
                    var a = Get(i);
                    var b = Get(i + 1);
                    if (time < a.Time) continue;
                    if (time > b.Time) continue;

                    var dt = b.Time - a.Time;
                    if (dt <= 0d)
                    {
                        pos = b.Pos;
                        return true;
                    }

                    var t = (float)((time - a.Time) / dt);
                    pos = Vector3.LerpUnclamped(a.Pos, b.Pos, t);
                    return true;
                }

                pos = last.Pos;
                return true;
            }
        }

        private readonly Dictionary<EC.IEntityId, Handle> _handles = new Dictionary<EC.IEntityId, Handle>();
        private readonly Dictionary<int, EC.IEntityId> _actorIdToEntityId = new Dictionary<int, EC.IEntityId>();

        private double _renderTime;
        private int _renderTimeLastFrame;
        private double _renderFrameAlpha;

        public bool InterpolationEnabled { get; set; } = true;

        public float BackTimeTicks { get; set; } = 1f;

        public float MaxLagTicks { get; set; } = 4f;

        public bool TryGetShellGameObject(EC.IEntityId id, out GameObject go)
        {
            go = null;
            if (!_handles.TryGetValue(id, out var h) || h == null) return false;
            if (h.Destroyed) return false;
            if (h.GameObject == null) return false;
            go = h.GameObject;
            return true;
        }

        public bool TryGetInterpolatedPos(EC.IEntityId id, out Vector3 pos)
        {
            pos = default;
            if (!_handles.TryGetValue(id, out var h) || h == null) return false;
            if (h.Destroyed) return false;

            if (!InterpolationEnabled)
            {
                if (h.HasPendingPos)
                {
                    pos = h.PendingPos;
                    return true;
                }

                return false;
            }

            if (h.Pos.TryEvaluate(_renderTime, out pos)) return true;
            if (h.HasPendingPos)
            {
                pos = h.PendingPos;
                return true;
            }

            return false;
        }

        public void ForEachShellGameObject(Action<int, EC.IEntityId, GameObject> visitor)
        {
            if (visitor == null) return;

            foreach (var kv in _handles)
            {
                var id = kv.Key;
                var h = kv.Value;
                if (h == null || h.Destroyed || h.GameObject == null) continue;
                visitor(h.ActorId, id, h.GameObject);
            }
        }

        public bool TryGetAttachRoot(BattleNetId netId, out Transform t)
        {
            t = null;
            if (netId.Value <= 0) return false;

            foreach (var kv in _handles)
            {
                var h = kv.Value;
                if (h == null || h.Destroyed || h.GameObject == null) continue;
                if (h.GameObject.name == $"Actor_{netId.Value}")
                {
                    var child = h.GameObject.transform.Find("AttachRoot");
                    if (child != null)
                    {
                        t = child;
                        return true;
                    }
                    return false;
                }
            }

            return false;
        }

        public void Sync(EC.IEntity entity, BattleContext ctx)
        {
            if (!entity.TryGetRef(out BattleNetIdComponent netIdComp) || netIdComp == null) return;
            if (!entity.TryGetRef(out BattleTransformComponent t) || t == null) return;
            var meta = entity.TryGetRef(out BattleEntityMetaComponent metaComp) ? metaComp : null;

            var actorId = netIdComp.NetId.Value;
            if (actorId <= 0) return;

            var desiredModelId = BattleViewFactory.ResolveModelId(meta);

            if (!_handles.TryGetValue(entity.Id, out var h))
            {
                h = new Handle();
                _handles[entity.Id] = h;
            }

            if (h.Destroyed) return;

            if (h.ActorId != actorId)
            {
                if (h.ActorId > 0) _actorIdToEntityId.Remove(h.ActorId);
                h.ActorId = actorId;
            }
            _actorIdToEntityId[actorId] = entity.Id;

            var sampleTime = 0d;
            if (ctx != null)
            {
                var tickRate = ctx.Plan.TickRate;
                if (tickRate <= 0) tickRate = 30;
                sampleTime = (double)ctx.LastFrame / tickRate;
            }

            SampleEntity(entity, in t.Position, sampleTime);

            if (desiredModelId > 0 && (h.GameObject == null || h.ModelId != desiredModelId))
            {
                h.Version++;
                if (h.GameObject != null)
                {
                    if (h.ViewHandle != null) h.ViewHandle.Registry = null;
                    UnityEngine.Object.Destroy(h.GameObject);
                    h.GameObject = null;
                    h.ViewHandle = null;
                }

                h.ModelId = desiredModelId;

                var go = _shellLoader != null ? _shellLoader.CreateShellGameObject(actorId, desiredModelId) : null;
                h.GameObject = go;

                if (go != null)
                {
                    var vh = go.GetComponent<MonoViewHandle>();
                    if (vh == null) vh = go.AddComponent<MonoViewHandle>();
                    vh.ActorId = actorId;
                    vh.Registry = this;
                    h.ViewHandle = vh;
                }
            }

            var desiredVfxId = BattleViewFactory.ResolveProjectileVfxId(meta);
            if (desiredVfxId > 0 && _vfx != null && _vfxNode.IsValid)
            {
                if (h.VfxEntityId.Index == 0 || h.VfxId != desiredVfxId)
                {
                    if (h.VfxEntityId.Index != 0)
                    {
                        _vfx.DestroyVfxEntity(entity.World, h.VfxEntityId);
                        h.VfxEntityId = default;
                    }

                    if (_vfx.TryCreateVfxEntity(entity.World, _vfxNode, desiredVfxId, entity.Id, in h.PendingPos, out var vfxEntity))
                    {
                        h.VfxId = desiredVfxId;
                        h.VfxEntityId = vfxEntity.Id;
                    }
                }
            }
        }

        private void SampleEntity(in EC.IEntity entity, in Vector3 pos, double sampleTime)
        {
            if (!_handles.TryGetValue(entity.Id, out var h) || h == null)
            {
                h = new Handle();
                _handles[entity.Id] = h;
            }
            if (h.Destroyed) return;

            h.PendingPos = pos;
            h.HasPendingPos = true;
            h.Pos.Add(sampleTime, in h.PendingPos);

            if (entity.TryGetRef(out BattleNetIdComponent netIdComp) && netIdComp != null)
            {
                var actorId = netIdComp.NetId.Value;
                if (actorId > 0)
                {
                    if (h.ActorId != actorId)
                    {
                        if (h.ActorId > 0) _actorIdToEntityId.Remove(h.ActorId);
                        h.ActorId = actorId;
                    }
                    _actorIdToEntityId[actorId] = entity.Id;
                }
            }
        }

        public void TickInterpolation(BattleContext ctx, float deltaTime)
        {
            if (ctx == null) return;
            if (deltaTime <= 0f) return;
            if (ctx.EntityWorld == null) return;

            if (!InterpolationEnabled)
            {
                foreach (var kv in _handles)
                {
                    var h = kv.Value;
                    if (h == null || h.Destroyed) continue;
                    if (h.GameObject == null) continue;
                    if (!h.HasPendingPos) continue;

                    var pos = h.PendingPos;
                    h.GameObject.transform.position = pos;

                    if (h.VfxEntityId.Index != 0 && _vfx != null)
                    {
                        _vfx.SyncFollow(_vfxNode.World, h.VfxEntityId, in pos);
                    }
                }

                return;
            }

            var tickRate = ctx.Plan.TickRate;
            if (tickRate <= 0) tickRate = 30;
            var fixedDelta = 1d / tickRate;

            var logicTime = ctx.LogicTimeSeconds;
            if (logicTime <= 0d)
            {
                logicTime = ctx.LastFrame * fixedDelta;
            }

            var backTicks = BackTimeTicks;
            if (backTicks <= 0f) backTicks = 1f;
            var backTime = fixedDelta * backTicks;
            var target = logicTime - backTime;
            if (target < 0d) target = 0d;

            var frame = ctx.LastFrame;
            if (_renderTimeLastFrame != frame)
            {
                _renderTimeLastFrame = frame;

                // If logic advanced (possibly multiple frames in a single Unity frame),
                // keep renderTime not ahead of target.
                if (_renderTime > target) _renderTime = target;

                // Ensure we have a sample at this logic time for all alive entities.
                // This guarantees VFX-followed entities have interpolation data even if they never become Dirty.
                var sampleTime = frame * fixedDelta;
                ctx.EntityWorld.ForEachAlive(e =>
                {
                    if (!e.TryGetRef(out BattleNetIdComponent netIdComp) || netIdComp == null) return;
                    if (!e.TryGetRef(out BattleTransformComponent t) || t == null) return;
                    SampleEntity(e, in t.Position, sampleTime);
                });
            }

            // Continuous render clock: advance by render deltaTime but never run ahead of target.
            _renderTime += deltaTime;
            if (_renderTime > target) _renderTime = target;

            // Prevent excessive visual latency if the game stalls (optional safety clamp).
            var maxLagTicks = MaxLagTicks;
            if (maxLagTicks < 0f) maxLagTicks = 0f;
            var maxLag = backTime + fixedDelta * maxLagTicks;
            var minRenderTime = logicTime - maxLag;
            if (minRenderTime < 0d) minRenderTime = 0d;
            if (_renderTime < minRenderTime) _renderTime = minRenderTime;

            foreach (var kv in _handles)
            {
                var h = kv.Value;
                if (h == null || h.Destroyed) continue;
                if (h.GameObject == null) continue;

                Vector3 pos;
                if (!h.Pos.TryEvaluate(_renderTime, out pos))
                {
                    if (h.HasPendingPos) pos = h.PendingPos;
                    else continue;
                }

                h.GameObject.transform.position = pos;

                if (h.VfxEntityId.Index != 0 && _vfx != null)
                {
                    _vfx.SyncFollow(_vfxNode.World, h.VfxEntityId, in pos);
                }
            }
        }

        public void OnDestroyed(EC.IEntityId id)
        {
            if (!_handles.TryGetValue(id, out var h) || h == null) return;
            h.Destroyed = true;
            h.Version++;
            h.Pos.Clear();
            if (h.ActorId > 0) _actorIdToEntityId.Remove(h.ActorId);
            if (h.GameObject != null)
            {
                if (h.ViewHandle != null) h.ViewHandle.Registry = null;
                if (Application.isPlaying) UnityEngine.Object.Destroy(h.GameObject);
                else UnityEngine.Object.DestroyImmediate(h.GameObject);
                h.GameObject = null;
                h.ViewHandle = null;
            }

            if (h.VfxEntityId.Index != 0 && _vfx != null)
            {
                _vfx.DestroyVfxEntity(_vfxNode.World, h.VfxEntityId);
                h.VfxEntityId = default;
            }

            _handles.Remove(id);
        }

        public void Clear()
        {
            foreach (var kv in _handles)
            {
                var h = kv.Value;
                if (h?.GameObject != null)
                {
                    if (h.ViewHandle != null) h.ViewHandle.Registry = null;
                    if (Application.isPlaying) UnityEngine.Object.Destroy(h.GameObject);
                    else UnityEngine.Object.DestroyImmediate(h.GameObject);
                }

                if (h != null)
                {
                    h.Pos.Clear();
                }

                if (h != null && h.VfxEntityId.Index != 0 && _vfx != null)
                {
                    _vfx.DestroyVfxEntity(_vfxNode.World, h.VfxEntityId);
                    h.VfxEntityId = default;
                }
            }
            _handles.Clear();
            _actorIdToEntityId.Clear();

            _renderTime = 0d;
            _renderTimeLastFrame = 0;
            _renderFrameAlpha = 0d;
        }

        public void RebindAll(EC.IECWorld world)
        {
            if (world == null) return;
            world.ForEachAlive(e => Sync(e));
        }

        public void RebindAll(EC.IECWorld world, BattleContext ctx)
        {
            if (world == null) return;
            world.ForEachAlive(e => Sync(e, ctx));
        }

        void IMonoViewHandleRegistry.OnMonoViewHandleDestroyed(MonoViewHandle handle)
        {
            if (handle == null) return;
            if (handle.ActorId <= 0) return;
            if (!_actorIdToEntityId.TryGetValue(handle.ActorId, out var id)) return;
            if (!_handles.TryGetValue(id, out var h) || h == null) return;

            if (!ReferenceEquals(h.ViewHandle, handle)) return;

            h.GameObject = null;
            h.ViewHandle = null;
        }
    }
}
