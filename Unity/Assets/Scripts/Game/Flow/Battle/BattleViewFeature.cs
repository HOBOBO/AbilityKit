using System;
using System.Collections.Generic;
using AbilityKit.Ability.Server;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Game.Battle.Moba.Config;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Flow.Snapshot;
using UnityEngine;
using EC = AbilityKit.Ability.EC;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleViewFeature : IGamePhaseFeature
    {
        private static MobaConfigDatabase _configs;

        private BattleContext _ctx;
        private IBattleEntityQuery _query;
        private BattleViewBinder _binder;

        private IDisposable _subEnterGame;
        private IDisposable _subActorTransform;

        public void OnAttach(in GamePhaseContext ctx)
        {
            ctx.Root.TryGetComponent(out _ctx);
            _query = _ctx != null ? _ctx.EntityQuery : null;
            _binder = new BattleViewBinder();

            if (_ctx?.EntityWorld != null)
            {
                _ctx.EntityWorld.EntityDestroyed += OnEntityDestroyed;
            }

            if (_ctx?.FrameSnapshots != null)
            {
                _subEnterGame = _ctx.FrameSnapshots.Subscribe<EnterMobaGameRes>((int)MobaOpCode.EnterGameSnapshot, OnEnterGameSnapshot);
                _subActorTransform = _ctx.FrameSnapshots.Subscribe<(int actorId, float x, float y, float z)[]>((int)MobaOpCode.ActorTransformSnapshot, OnActorTransformSnapshot);
            }
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            if (_ctx?.FrameSnapshots != null)
            {
                _subEnterGame?.Dispose();
                _subActorTransform?.Dispose();
            }

            _subEnterGame = null;
            _subActorTransform = null;

            if (_ctx?.EntityWorld != null)
            {
                _ctx.EntityWorld.EntityDestroyed -= OnEntityDestroyed;
            }

            _binder?.Clear();
            _binder = null;
            _ctx = null;
            _query = null;
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
        }

        private void OnEnterGameSnapshot(FramePacket packet, EnterMobaGameRes res)
        {
            RefreshDirtyViews();
        }

        private void OnActorTransformSnapshot(FramePacket packet, (int actorId, float x, float y, float z)[] entries)
        {
            RefreshDirtyViews();
        }

        private void RefreshDirtyViews()
        {
            if (_query?.World == null) return;

            var dirty = _ctx != null ? _ctx.DirtyEntities : null;
            if (dirty == null || dirty.Count == 0) return;

            for (int i = 0; i < dirty.Count; i++)
            {
                var id = dirty[i];
                if (!_query.World.IsAlive(id)) continue;
                _binder?.Sync(_query.World.Wrap(id));
            }

            dirty.Clear();
        }

        private void OnEntityDestroyed(EC.EntityId id)
        {
            _binder?.OnDestroyed(id);
        }

        private static GameObject CreateShellGameObject(int actorId, int modelId)
        {
            _configs ??= MobaConfigLoader.LoadDefault();

            GameObject prefab = null;
            if (_configs != null && modelId > 0)
            {
                try
                {
                    var model = _configs.GetModel(modelId);
                    if (model != null && !string.IsNullOrEmpty(model.PrefabPath))
                    {
                        prefab = Resources.Load<GameObject>(model.PrefabPath);
                    }
                }
                catch
                {
                }
            }

            GameObject go;
            if (prefab != null)
            {
                go = UnityEngine.Object.Instantiate(prefab);
                if (_configs != null && modelId > 0)
                {
                    try
                    {
                        var model = _configs.GetModel(modelId);
                        if (model != null)
                        {
                            var s = model.Scale <= 0f ? 1f : model.Scale;
                            go.transform.localScale = new Vector3(s, s, s);
                        }
                    }
                    catch
                    {
                    }
                }
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.localScale = new Vector3(1f, 2f, 1f);
            }

            go.name = $"Actor_{actorId}";
            return go;
        }

        private static int ResolveModelId(BattleEntityMetaComponent meta)
        {
            if (meta == null) return 0;

            _configs ??= MobaConfigLoader.LoadDefault();
            if (_configs == null) return 0;

            try
            {
                if (meta.Kind == BattleEntityKind.Character)
                {
                    var ch = _configs.GetCharacter(meta.EntityCode);
                    return ch != null ? ch.ModelId : 0;
                }

                // TODO: projectile/summon/trap - resolve via their tables.
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        private sealed class BattleViewBinder
        {
            private sealed class Handle
            {
                public int Version;
                public bool Destroyed;
                public int ModelId;
                public GameObject GameObject;
                public Vector3 PendingPos;
                public bool HasPendingPos;
            }

            private readonly Dictionary<EC.EntityId, Handle> _handles = new Dictionary<EC.EntityId, Handle>();

            public void Sync(EC.Entity entity)
            {
                if (!entity.TryGetComponent(out BattleNetIdComponent netIdComp) || netIdComp == null) return;
                if (!entity.TryGetComponent(out BattleTransformComponent t) || t == null) return;
                var meta = entity.TryGetComponent(out BattleEntityMetaComponent metaComp) ? metaComp : null;

                var desiredModelId = ResolveModelId(meta);
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
                        UnityEngine.Object.Destroy(h.GameObject);
                        h.GameObject = null;
                    }

                    h.ModelId = desiredModelId;

                    var go = CreateShellGameObject(netIdComp.NetId.Value, desiredModelId);
                    h.GameObject = go;
                }

                if (h.GameObject != null && h.HasPendingPos)
                {
                    h.GameObject.transform.position = h.PendingPos;
                }
            }

            public void OnDestroyed(EC.EntityId id)
            {
                if (!_handles.TryGetValue(id, out var h) || h == null) return;
                h.Destroyed = true;
                h.Version++;
                if (h.GameObject != null)
                {
                    UnityEngine.Object.Destroy(h.GameObject);
                    h.GameObject = null;
                }
                _handles.Remove(id);
            }

            public void Clear()
            {
                foreach (var kv in _handles)
                {
                    var h = kv.Value;
                    if (h?.GameObject != null) UnityEngine.Object.Destroy(h.GameObject);
                }
                _handles.Clear();
            }
        }
    }
}
