using System.Collections.Generic;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Vfx;
using UnityEngine;
using EC = AbilityKit.Ability.EC;

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

        private readonly BattleVfxManager _vfx;
        private readonly EC.Entity _vfxNode;

        private readonly IBattleViewShellLoader _shellLoader;

        public BattleViewBinder(BattleVfxManager vfx, in EC.Entity vfxNode, IBattleViewShellLoader shellLoader = null)
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
            public EC.EntityId VfxEntityId;
            public Vector3 PendingPos;
            public bool HasPendingPos;
        }

        private readonly Dictionary<EC.EntityId, Handle> _handles = new Dictionary<EC.EntityId, Handle>();
        private readonly Dictionary<int, EC.EntityId> _actorIdToEntityId = new Dictionary<int, EC.EntityId>();

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

        public void Sync(EC.Entity entity)
        {
            if (!entity.TryGetComponent(out BattleNetIdComponent netIdComp) || netIdComp == null) return;
            if (!entity.TryGetComponent(out BattleTransformComponent t) || t == null) return;
            var meta = entity.TryGetComponent(out BattleEntityMetaComponent metaComp) ? metaComp : null;

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

            h.PendingPos = t.Position;
            h.HasPendingPos = true;

            if (desiredModelId > 0 && (h.GameObject == null || h.ModelId != desiredModelId))
            {
                h.Version++;
                if (h.GameObject != null)
                {
                    if (h.ViewHandle != null) h.ViewHandle.Registry = null;
                    Object.Destroy(h.GameObject);
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

            if (h.GameObject != null && h.HasPendingPos)
            {
                h.GameObject.transform.position = h.PendingPos;
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
                else
                {
                    _vfx.SyncFollow(entity.World, h.VfxEntityId, in h.PendingPos);
                }
            }
        }

        public void OnDestroyed(EC.EntityId id)
        {
            if (!_handles.TryGetValue(id, out var h) || h == null) return;
            h.Destroyed = true;
            h.Version++;
            if (h.ActorId > 0) _actorIdToEntityId.Remove(h.ActorId);
            if (h.GameObject != null)
            {
                if (h.ViewHandle != null) h.ViewHandle.Registry = null;
                Object.Destroy(h.GameObject);
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
                    Object.Destroy(h.GameObject);
                }

                if (h != null && h.VfxEntityId.Index != 0 && _vfx != null)
                {
                    _vfx.DestroyVfxEntity(_vfxNode.World, h.VfxEntityId);
                    h.VfxEntityId = default;
                }
            }
            _handles.Clear();
            _actorIdToEntityId.Clear();
        }

        public void RebindAll(EC.EntityWorld world)
        {
            if (world == null) return;
            world.ForEachAlive(Sync);
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
