using System;
using System.Collections.Generic;
using AbilityKit.Ability.Server;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Game.Battle.Moba.Config;
using AbilityKit.Game.Battle.Vfx;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Flow.Snapshot;
using UnityEngine;
using EC = AbilityKit.Ability.EC;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleViewFeature : IGamePhaseFeature
    {
        private sealed class FloatingText
        {
            public GameObject Go;
            public TextMesh Text;
            public float Age;
            public float Lifetime;
            public Vector3 Velocity;
            public Color BaseColor;
        }

        private sealed class AoeViewHandle
        {
            public int AreaId;
            public int TemplateId;
            public GameObject ModelGo;
            public GameObject VfxGo;
        }

        private readonly Dictionary<int, AoeViewHandle> _aoeViews = new Dictionary<int, AoeViewHandle>(128);

        private void OnAreaEventSnapshot(FramePacket packet, MobaAreaEventSnapshotCodec.Entry[] entries)
        {
            if (entries == null || entries.Length == 0) return;
            if (_ctx?.EntityWorld == null) return;
            if (_query == null) return;

            _configs ??= MobaConfigLoader.LoadDefault();
            if (_configs == null) return;

            for (int i = 0; i < entries.Length; i++)
            {
                var evt = entries[i];
                if (evt.AreaId <= 0) continue;

                var kind = evt.Kind;
                if (kind == (int)MobaAreaEventSnapshotCodec.EventKind.Spawn)
                {
                    if (_aoeViews.ContainsKey(evt.AreaId)) continue;

                    AoeMO aoe = null;
                    try { aoe = _configs.GetAoe(evt.TemplateId); }
                    catch { aoe = null; }
                    if (aoe == null) continue;

                    var pos = new Vector3(evt.X, evt.Y, evt.Z);
                    pos += new Vector3(aoe.OffsetX, aoe.OffsetY, aoe.OffsetZ);

                    Transform attach = null;
                    if (aoe.AttachMode == 1 && evt.OwnerActorId > 0)
                    {
                        if (_binder != null && _binder.TryGetAttachRoot(new BattleNetId(evt.OwnerActorId), out var t) && t != null)
                        {
                            attach = t;
                        }
                    }

                    var h = new AoeViewHandle { AreaId = evt.AreaId, TemplateId = evt.TemplateId };

                    if (aoe.ModelId > 0)
                    {
                        h.ModelGo = CreateModelGo(aoe.ModelId);
                        if (h.ModelGo != null)
                        {
                            if (attach != null)
                            {
                                h.ModelGo.transform.SetParent(attach, worldPositionStays: false);
                                h.ModelGo.transform.localPosition = Vector3.zero;
                            }
                            else
                            {
                                h.ModelGo.transform.position = pos;
                            }
                        }
                    }

                    if (aoe.VfxId > 0)
                    {
                        h.VfxGo = CreateVfxGo(aoe.VfxId);
                        if (h.VfxGo != null)
                        {
                            if (attach != null)
                            {
                                h.VfxGo.transform.SetParent(attach, worldPositionStays: false);
                                h.VfxGo.transform.localPosition = Vector3.zero;
                            }
                            else
                            {
                                h.VfxGo.transform.position = pos;
                            }
                        }
                    }

                    _aoeViews[evt.AreaId] = h;
                }
                else if (kind == (int)MobaAreaEventSnapshotCodec.EventKind.Expire)
                {
                    if (_aoeViews.TryGetValue(evt.AreaId, out var h) && h != null)
                    {
                        if (h.ModelGo != null) UnityEngine.Object.Destroy(h.ModelGo);
                        if (h.VfxGo != null) UnityEngine.Object.Destroy(h.VfxGo);
                        _aoeViews.Remove(evt.AreaId);
                    }
                }
            }
        }

        private static MobaConfigDatabase _configs;
        private static VfxDatabase _vfxDb;

        private BattleContext _ctx;
        private IBattleEntityQuery _query;
        private BattleViewBinder _binder;
        private BattleVfxManager _vfx;
        private EC.Entity _vfxNode;

        private IDisposable _subEnterGame;
        private IDisposable _subActorTransform;
        private IDisposable _subProjectileEvents;
        private IDisposable _subAreaEvents;
        private IDisposable _subDamageEvents;

        private readonly List<FloatingText> _floatingTexts = new List<FloatingText>(64);

        public void OnAttach(in GamePhaseContext ctx)
        {
            ctx.Root.TryGetComponent(out _ctx);
            _query = _ctx != null ? _ctx.EntityQuery : null;
            _vfxDb ??= VfxDatabase.LoadFromResources("vfx/vfx");
            _vfx = new BattleVfxManager(_vfxDb);

            if (_ctx != null && _ctx.EntityNode.IsValid)
            {
                _vfxNode = _ctx.EntityNode.AddChild("BattleVfx");
            }

            _binder = new BattleViewBinder(_vfx, _vfxNode);

            if (_ctx?.EntityWorld != null)
            {
                _ctx.EntityWorld.EntityDestroyed += OnEntityDestroyed;
            }

            if (_ctx?.FrameSnapshots != null)
            {
                _subEnterGame = _ctx.FrameSnapshots.Subscribe<EnterMobaGameRes>((int)MobaOpCode.EnterGameSnapshot, OnEnterGameSnapshot);
                _subActorTransform = _ctx.FrameSnapshots.Subscribe<(int actorId, float x, float y, float z)[]>((int)MobaOpCode.ActorTransformSnapshot, OnActorTransformSnapshot);
                _subProjectileEvents = _ctx.FrameSnapshots.Subscribe<MobaProjectileEventSnapshotCodec.Entry[]>((int)MobaOpCode.ProjectileEventSnapshot, OnProjectileEventSnapshot);
                _subAreaEvents = _ctx.FrameSnapshots.Subscribe<MobaAreaEventSnapshotCodec.Entry[]>((int)MobaOpCode.AreaEventSnapshot, OnAreaEventSnapshot);
                _subDamageEvents = _ctx.FrameSnapshots.Subscribe<MobaDamageEventSnapshotCodec.Entry[]>((int)MobaOpCode.DamageEventSnapshot, OnDamageEventSnapshot);
            }
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            if (_ctx?.FrameSnapshots != null)
            {
                _subEnterGame?.Dispose();
                _subActorTransform?.Dispose();
                _subProjectileEvents?.Dispose();
                _subAreaEvents?.Dispose();
                _subDamageEvents?.Dispose();
            }

            _subEnterGame = null;
            _subActorTransform = null;
            _subProjectileEvents = null;
            _subAreaEvents = null;
            _subDamageEvents = null;

            if (_ctx?.EntityWorld != null)
            {
                _ctx.EntityWorld.EntityDestroyed -= OnEntityDestroyed;
            }

            ClearFloatingTexts();

            _binder?.Clear();
            _binder = null;
            _vfx = null;
            _vfxNode = default;
            _ctx = null;
            _query = null;
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
            if (_ctx?.EntityWorld == null) return;
            if (_vfxNode.IsValid) _vfx?.Tick(_vfxNode);
            TickFloatingTexts(deltaTime);
        }

        private void OnEnterGameSnapshot(FramePacket packet, EnterMobaGameRes res)
        {
            RefreshDirtyViews();
        }

        private void OnActorTransformSnapshot(FramePacket packet, (int actorId, float x, float y, float z)[] entries)
        {
            RefreshDirtyViews();
        }

        private void OnProjectileEventSnapshot(FramePacket packet, MobaProjectileEventSnapshotCodec.Entry[] entries)
        {
            if (entries == null || entries.Length == 0) return;
            if (_ctx?.EntityWorld == null) return;
            if (_query == null) return;
            if (_vfx == null) return;
            if (!_vfxNode.IsValid) return;

            _configs ??= MobaConfigLoader.LoadDefault();
            if (_configs == null) return;

            for (int i = 0; i < entries.Length; i++)
            {
                var evt = entries[i];
                if (evt.TemplateId <= 0) continue;

                ProjectileMO proj = null;
                try { proj = _configs.GetProjectile(evt.TemplateId); }
                catch { }
                if (proj == null) continue;

                var vfxId = 0;
                if (evt.Kind == (int)MobaProjectileEventSnapshotCodec.EventKind.Spawn)
                {
                    vfxId = proj.OnSpawnVfxId;
                }
                else if (evt.Kind == (int)MobaProjectileEventSnapshotCodec.EventKind.Hit)
                {
                    vfxId = proj.OnHitVfxId;
                }
                else if (evt.Kind == (int)MobaProjectileEventSnapshotCodec.EventKind.Exit)
                {
                    vfxId = proj.OnExpireVfxId;
                }

                if (vfxId <= 0) continue;

                var pos = new Vector3(evt.X, evt.Y, evt.Z);

                var followId = default(EC.EntityId);
                if (evt.ProjectileActorId > 0 && _query.TryResolve(new BattleNetId(evt.ProjectileActorId), out var projEntity))
                {
                    followId = projEntity.Id;
                }

                _vfx.TryCreateVfxEntity(_ctx.EntityWorld, _vfxNode, vfxId, followId, in pos, out _);
            }
        }

        private void OnDamageEventSnapshot(FramePacket packet, MobaDamageEventSnapshotCodec.Entry[] entries)
        {
            if (entries == null || entries.Length == 0) return;
            if (_ctx?.EntityWorld == null) return;
            if (_query == null) return;
            if (_vfxNode.IsValid == false) return;

            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                if (e.TargetActorId <= 0) continue;
                if (e.Value == 0f) continue;

                var pos = Vector3.zero;
                if (_query.TryGetTransform(new BattleNetId(e.TargetActorId), out var transform) && transform != null)
                {
                    pos = transform.Position;
                }
                pos += Vector3.up * 2f;

                var isHeal = e.Kind == (int)MobaDamageEventSnapshotCodec.EventKind.Heal;
                var color = isHeal ? new Color(0.2f, 1f, 0.2f, 1f) : new Color(1f, 0.2f, 0.2f, 1f);
                var text = Mathf.Abs(e.Value) >= 1f ? Mathf.RoundToInt(Mathf.Abs(e.Value)).ToString() : Mathf.Abs(e.Value).ToString("0.0");
                if (isHeal) text = $"+{text}";

                SpawnFloatingText(text, in pos, color);
            }
        }

        private void SpawnFloatingText(string text, in Vector3 worldPos, Color color)
        {
            if (!_vfxNode.IsValid) return;

            var go = new GameObject("DamageText");
            go.transform.position = worldPos;

            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.color = color;
            tm.fontSize = 42;
            tm.characterSize = 0.06f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;

            var ft = new FloatingText
            {
                Go = go,
                Text = tm,
                Age = 0f,
                Lifetime = 0.9f,
                Velocity = new Vector3(0f, 1.5f, 0f),
                BaseColor = color,
            };
            _floatingTexts.Add(ft);
        }

        private void TickFloatingTexts(float deltaTime)
        {
            if (_floatingTexts.Count == 0) return;

            for (int i = _floatingTexts.Count - 1; i >= 0; i--)
            {
                var ft = _floatingTexts[i];
                if (ft == null || ft.Go == null || ft.Text == null)
                {
                    _floatingTexts.RemoveAt(i);
                    continue;
                }

                ft.Age += deltaTime;
                ft.Go.transform.position += ft.Velocity * deltaTime;

                var t = ft.Lifetime > 0f ? Mathf.Clamp01(ft.Age / ft.Lifetime) : 1f;
                var c = ft.BaseColor;
                c.a = 1f - t;
                ft.Text.color = c;

                if (ft.Age >= ft.Lifetime)
                {
                    UnityEngine.Object.Destroy(ft.Go);
                    _floatingTexts.RemoveAt(i);
                }
            }
        }

        private void ClearFloatingTexts()
        {
            for (int i = 0; i < _floatingTexts.Count; i++)
            {
                var ft = _floatingTexts[i];
                if (ft?.Go != null)
                {
                    UnityEngine.Object.Destroy(ft.Go);
                }
            }
            _floatingTexts.Clear();
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
            _ctx?.EntityLookup?.UnbindByEntityId(id);
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

            var attachRoot = new GameObject("AttachRoot");
            attachRoot.transform.SetParent(go.transform, worldPositionStays: false);
            attachRoot.transform.localPosition = Vector3.zero;
            return go;
        }

        private static GameObject CreateModelGo(int modelId)
        {
            if (modelId <= 0) return null;

            _configs ??= MobaConfigLoader.LoadDefault();
            GameObject prefab = null;
            if (_configs != null)
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
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.transform.localScale = Vector3.one * 0.5f;
            }

            go.name = $"AoeModel_{modelId}";
            return go;
        }

        private GameObject CreateVfxGo(int vfxId)
        {
            if (vfxId <= 0) return null;
            _vfxDb ??= VfxDatabase.LoadFromResources("vfx/vfx");
            if (_vfxDb == null) return null;

            if (!_vfxDb.TryGet(vfxId, out var dto) || dto == null || string.IsNullOrEmpty(dto.Resource))
            {
                return null;
            }

            var prefab = Resources.Load<GameObject>(dto.Resource);
            GameObject go;
            if (prefab != null)
            {
                go = UnityEngine.Object.Instantiate(prefab);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.transform.localScale = Vector3.one * 0.5f;
            }

            go.name = $"AoeVfx_{vfxId}";
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

        private static int ResolveProjectileVfxId(BattleEntityMetaComponent meta)
        {
            if (meta == null) return 0;
            if (meta.Kind != BattleEntityKind.Projectile) return 0;

            _configs ??= MobaConfigLoader.LoadDefault();
            if (_configs == null) return 0;

            try
            {
                var proj = _configs.GetProjectile(meta.EntityCode);
                return proj != null ? proj.VfxId : 0;
            }
            catch
            {
                return 0;
            }
        }

        private sealed class BattleViewBinder
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

                var desiredVfxId = ResolveProjectileVfxId(meta);
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
                    UnityEngine.Object.Destroy(h.GameObject);
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
                    if (h?.GameObject != null) UnityEngine.Object.Destroy(h.GameObject);

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
}
