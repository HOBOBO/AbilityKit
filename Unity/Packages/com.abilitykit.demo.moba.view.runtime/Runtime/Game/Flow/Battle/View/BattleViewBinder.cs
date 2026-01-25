using System.Collections.Generic;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Vfx;
using UnityEngine;
using EC = AbilityKit.Ability.EC;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleViewBinder
    {
        private readonly BattleVfxManager _vfx;
        private readonly EC.Entity _vfxNode;

        public BattleViewBinder(BattleVfxManager vfx, in EC.Entity vfxNode)
        {
            _vfx = vfx;
            _vfxNode = vfxNode;
        }

        private sealed class Handle
        {
            public int Version;
            public bool Destroyed;
            public int ModelId;
            public GameObject GameObject;
            public int VfxId;
            public EC.EntityId VfxEntityId;
            public Vector3 PendingPos;
            public bool HasPendingPos;
        }

        private readonly Dictionary<EC.EntityId, Handle> _handles = new Dictionary<EC.EntityId, Handle>();

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

            var desiredModelId = BattleViewFactory.ResolveModelId(meta);
            if (!_handles.TryGetValue(entity.Id, out var h))
            {
                h = new Handle();
                _handles[entity.Id] = h;
            }

            if (h.Destroyed) return;

            h.PendingPos = t.Position;
            h.HasPendingPos = true;

            if (desiredModelId > 0 && (h.GameObject == null || h.ModelId != desiredModelId))
            {
                h.Version++;
                if (h.GameObject != null)
                {
                    Object.Destroy(h.GameObject);
                    h.GameObject = null;
                }

                h.ModelId = desiredModelId;

                var go = BattleViewFactory.CreateShellGameObject(netIdComp.NetId.Value, desiredModelId);
                h.GameObject = go;
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
            if (h.GameObject != null)
            {
                Object.Destroy(h.GameObject);
                h.GameObject = null;
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
                if (h?.GameObject != null) Object.Destroy(h.GameObject);

                if (h != null && h.VfxEntityId.Index != 0 && _vfx != null)
                {
                    _vfx.DestroyVfxEntity(_vfxNode.World, h.VfxEntityId);
                    h.VfxEntityId = default;
                }
            }
            _handles.Clear();
        }
    }
}
